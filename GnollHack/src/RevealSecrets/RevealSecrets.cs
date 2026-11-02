// Reveal secrets for GnollHack (Steam, Windows). Loaded into GnollHackM.exe through
// DOTNET_STARTUP_HOOKS by "Play GnollHack (mods).cmd".
//
// Ctrl+E in the game window finds every secret door and secret corridor on the current
// level, wherever it is, exactly as if you had found each one by searching: it becomes a
// real door or corridor and appears on your map.
//
// The finding is done by the engine's own findone() (src/detect.c), the routine a wand of
// secret door detection runs on each square in range. It has to run on the engine thread
// at a safe point, so the mod borrows a command: for one key press it points the wizard
// command #wizdetect (^E) at its own function and switches flags.debug on, so the engine
// accepts ^E and calls that function from its command loop. The function calls findone()
// for every secret square on the level, then both changes are undone.
//
// None of this is exported. Everything is found from two exports by following the code:
// LibGetCommandFunctionPointer -> the command table -> wiz_detect -> findit -> findone,
// and findone's own first instructions give the level map's address and cell layout.
// Every step checks the instruction bytes and the constants from the source (ROWNO 21,
// SDOOR 14, SCORR 15); on any mismatch the mod writes nothing and logs why.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

internal class StartupHook
{
    public static void Initialize()
    {
        try
        {
            string exe = Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? "");
            if (!string.Equals(exe, "GnollHackM", StringComparison.OrdinalIgnoreCase))
                return;
            new Thread(RevealSecrets.Hotkey.Run) { IsBackground = true, Name = "RevealSecretsHotkey" }.Start();
            new Thread(RevealSecrets.Hotkey.RunEventTrigger) { IsBackground = true, Name = "RevealSecretsEvent" }.Start();
        }
        catch (Exception e)
        {
            RevealSecrets.Log.Write("startup failed: " + e);
        }
    }
}

namespace RevealSecrets
{
    internal static class Log
    {
        private static readonly string PathName = System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(typeof(Log).Assembly.Location) ?? ".", "revealsecrets.log");

        public static void Write(string msg)
        {
            try { File.AppendAllText(PathName, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss ") + msg + Environment.NewLine); }
            catch { }
        }
    }

    /* ---------------- Ctrl+E hotkey ---------------- */

    internal static class Hotkey
    {
        private const int WH_KEYBOARD_LL = 13, WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101, WM_TIMER = 0x0113;
        private const int VK_E = 0x45, VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private static readonly LowLevelKeyboardProc Proc = Callback; /* kept alive for the native side */
        private static IntPtr _hook;
        private static IntPtr _gameHookSeen;
        private static bool _held;

        /* Low-level hooks run newest first, and the game's own hook (KeyboardHook in
           GnollHackM's MauiProgram) forwards Ctrl+E to the engine and swallows it. So ours
           goes in after the game's, and again whenever the game reinstalls its hook. */
        public static void Run()
        {
            try
            {
                DateTime deadline = DateTime.UtcNow.AddMinutes(2);
                while (GameHookId() == IntPtr.Zero && DateTime.UtcNow < deadline)
                    Thread.Sleep(250);
                Install();
                SetTimer(IntPtr.Zero, IntPtr.Zero, 2000, IntPtr.Zero);
                while (GetMessage(out MSG msg, IntPtr.Zero, 0, 0) > 0)
                {
                    if (msg.message == WM_TIMER && GameHookId() != _gameHookSeen)
                        Install();
                }
            }
            catch (Exception e)
            {
                Log.Write("hotkey thread failed: " + e);
            }
        }

        /* The same action without the keyboard, for scripts and macro pads:
           [System.Threading.EventWaitHandle]::OpenExisting('Local\GnollHackRevealSecrets').Set() */
        public static void RunEventTrigger()
        {
            try
            {
                using (var ev = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\GnollHackRevealSecrets"))
                    while (ev.WaitOne())
                        Revealer.Trigger();
            }
            catch (Exception e)
            {
                Log.Write("event trigger failed: " + e);
            }
        }

        private static void Install()
        {
            if (_hook != IntPtr.Zero)
                UnhookWindowsHookEx(_hook);
            _gameHookSeen = GameHookId();
            _hook = SetWindowsHookEx(WH_KEYBOARD_LL, Proc, GetModuleHandle(null), 0);
            Log.Write(_hook == IntPtr.Zero
                ? "could not install the Ctrl+E hook, error " + Marshal.GetLastWin32Error()
                : "Ctrl+E hook installed (game hook " + (_gameHookSeen == IntPtr.Zero ? "not found" : "found") + ")");
        }

        private static IntPtr GameHookId()
        {
            try
            {
                Type t = Game.T("KeyboardHook");
                return (IntPtr)t.GetField("_hookID", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            }
            catch
            {
                return IntPtr.Zero;
            }
        }

        private static IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && Marshal.ReadInt32(lParam) == VK_E)
            {
                int msg = (int)wParam;
                if (msg == WM_KEYUP && _held)
                {
                    _held = false;
                    return (IntPtr)1;
                }
                if (msg == WM_KEYDOWN && Down(VK_CONTROL) && !Down(VK_MENU) && !Down(VK_SHIFT) && GameInForeground())
                {
                    if (!_held) /* ignore auto-repeat while the key is held */
                        Revealer.Trigger();
                    _held = true;
                    return (IntPtr)1;
                }
            }
            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        private static bool Down(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

        private static bool GameInForeground()
        {
            GetWindowThreadProcessId(GetForegroundWindow(), out uint pid);
            return pid == (uint)Environment.ProcessId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam, lParam; public uint time; public int x, y; public uint lPrivate; }

        [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc fn, IntPtr hMod, uint threadId);
        [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] private static extern int GetMessage(out MSG msg, IntPtr hwnd, uint min, uint max);
        [DllImport("user32.dll")] private static extern IntPtr SetTimer(IntPtr hwnd, IntPtr id, uint ms, IntPtr fn);
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vk);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string name);
    }

    /* ---------------- the reveal ---------------- */

    internal static unsafe class Revealer
    {
        private const byte SDOOR = 14, SCORR = 15; /* include/rm.h */
        private const int COLNO = 80;              /* include/global.h; column 0 is never used */

        private static int _busy;
        private static readonly ManualResetEventSlim Done = new ManualResetEventSlim(false);
        private static Engine.Addresses _a;

        public static void Trigger()
        {
            if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
                return;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try { Run(); }
                catch (Exception e) { Log.Write("reveal failed: " + e); }
                finally { Interlocked.Exchange(ref _busy, 0); }
            });
        }

        private static void Run()
        {
            if (!Game.Ui(Game.AtCommandPrompt))
            {
                Log.Write("Ctrl+E ignored: the game is not waiting for a command");
                return;
            }
            if (!Game.EngineIdle(1500))
            {
                Log.Write("Ctrl+E ignored: the game is busy");
                return;
            }
            _a = Engine.Resolve();
            IntPtr original = Marshal.ReadIntPtr(_a.WizDetectSlot);
            if (original != _a.WizDetect)
                throw new InvalidOperationException("the #wizdetect command is not pointing at wiz_detect; not touching it");
            bool wasWizard = Marshal.ReadByte(_a.Debug) != 0;
            Done.Reset();
            try
            {
                if (!wasWizard)
                    Marshal.WriteByte(_a.Debug, 1);
                Marshal.WriteIntPtr(_a.WizDetectSlot, (IntPtr)(delegate* unmanaged<int>)&RevealAll);
                Game.Ui(() => Game.Call(Game.GHApp, "SendKeyPress", (int)'e', true, false));
                if (!Done.Wait(5000))
                    Log.Write("the engine did not run the command");
                Game.EngineIdle(5000);
            }
            finally
            {
                Marshal.WriteIntPtr(_a.WizDetectSlot, original);
                if (!wasWizard)
                    Marshal.WriteByte(_a.Debug, 0);
            }
        }

        /* Runs on the engine thread, called from rhack() in place of wiz_detect(). Returning
           0 means the command took no time. */
        [UnmanagedCallersOnly]
        private static int RevealAll()
        {
            try
            {
                string msg;
                if (Marshal.ReadByte(_a.USwallow) != 0)
                    msg = "You cannot sense anything from in here.";
                else
                {
                    var findone = (delegate* unmanaged<int, int, int*, void>)_a.FindOne;
                    int doors = 0, passages = 0, num = 0;
                    for (int x = 1; x < COLNO; x++)
                    {
                        for (int y = 0; y < _a.RowNo; y++)
                        {
                            byte typ = _a.Typ(x, y);
                            if (typ == SDOOR)
                                doors++;
                            else if (typ == SCORR)
                                passages++;
                            else
                                continue;
                            findone(x, y, &num);
                        }
                    }
                    msg = doors + passages == 0
                        ? "You sense no secret doors or hidden passages on this level."
                        : "You sense " + string.Join(" and ", new[] { Count(doors, "secret door"), Count(passages, "hidden passage") }.Where(s => s != null)) + ".";
                    Log.Write("revealed " + doors + " secret doors, " + passages + " secret corridors");
                }
                Game.Call(Game.Get(Game.GHApp, "CurrentGHGame"), "ClientCallback_RawPrint", msg);
            }
            catch (Exception e)
            {
                Log.Write("reveal on the engine thread failed: " + e);
            }
            finally
            {
                Done.Set();
            }
            return 0;
        }

        private static string Count(int n, string noun) => n == 0 ? null : n + " " + noun + (n == 1 ? "" : "s");
    }

    /* ---------------- engine memory ---------------- */

    internal static unsafe class Engine
    {
        internal sealed class Addresses
        {
            public IntPtr Debug;         /* flags.debug */
            public IntPtr WizDetectSlot; /* &extcmdlist[#wizdetect].ef_funct */
            public IntPtr WizDetect;     /* its normal value */
            public IntPtr FindOne;       /* findone(int x, int y, genericptr_t num) */
            public IntPtr USwallow;      /* u.uswallow */
            public IntPtr Levl;          /* &levl[0][0] */
            public int RowNo, CellSize, TypOffset;

            public byte Typ(int x, int y) => Marshal.ReadByte(Levl + (x * RowNo + y) * CellSize + TypOffset);
        }

        /* include/flag.h, struct flag: all booleans are single bytes from the top.
           acoustics 0, autodig 1, ... debug 11, ... explore 14, ... self_click_action 33 */
        private const int AutodigOffset = 1, DebugOffset = 11, ExploreOffset = 14, SelfClickOffset = 33;
        private const int CtrlE = 5, ROWNO = 21, MaxTyp = 64;

        private static Addresses _resolved;

        public static Addresses Resolve()
        {
            if (_resolved != null)
                return _resolved;
            IntPtr mod = GetModuleHandle("gnollhackwin.dll");
            if (mod == IntPtr.Zero)
                throw new InvalidOperationException("gnollhackwin.dll is not loaded");
            var a = new Addresses { Debug = DebugFlag(mod) };

            /* LibGetCommandFunctionPointer: return Cmd.commands[cmd]->ef_funct */
            IntPtr getCmd = Export(mod, "LibGetCommandFunctionPointer");
            Expect(getCmd, 0, "81 F9 FF 00 00 00 77 ?? 48 63 C1 48 8D 0D ?? ?? ?? ?? 48 8B 04 C1 48 85 C0 74 ?? 48 8B 40 ?? C3");
            IntPtr commands = Rip(getCmd + 14, 0);
            int functOffset = Marshal.ReadByte(getCmd + 30);
            IntPtr entry = Marshal.ReadIntPtr(commands + CtrlE * IntPtr.Size);
            if (entry == IntPtr.Zero || Marshal.ReadByte(entry) != CtrlE)
                throw new InvalidOperationException("^E is not bound to a command entry");
            a.WizDetectSlot = entry + functOffset;
            a.WizDetect = Marshal.ReadIntPtr(a.WizDetectSlot);
            if (a.WizDetect != ((delegate* unmanaged<int, IntPtr>)getCmd)(CtrlE))
                throw new InvalidOperationException("command table layout differs from LibGetCommandFunctionPointer");

            /* wiz_detect: if (wizard) (void) findit(); return 0; */
            Expect(a.WizDetect, 0, "48 83 EC 28 80 3D ?? ?? ?? ?? 00 74 ?? E8 ?? ?? ?? ??");
            if (Rip(a.WizDetect + 6, 1) != a.Debug)
                throw new InvalidOperationException("wiz_detect does not test flags.debug");
            IntPtr findit = Rip(a.WizDetect + 14, 0);

            /* findit: if (u.uswallow) return 0; do_clear_area(u.ux, u.uy, FIND_RANGE = 8, findone, &num); */
            int lea = Find(findit, 0x80, "4C 8D 0D ?? ?? ?? ?? 41 B8 08 00 00 00");
            a.FindOne = Rip(findit + lea + 3, 0);
            int swallow = Find(findit, lea, "80 3D ?? ?? ?? ?? 00");
            a.USwallow = Rip(findit + swallow + 2, 1);

            /* findone: if (levl[zx][zy].typ == SDOOR) ... else if (levl[zx][zy].typ == SCORR) */
            a.Levl = Rip(a.FindOne + Find(a.FindOne, 0x40, "4C 8D 35 ?? ?? ?? ??") + 3, 0);
            a.RowNo = Marshal.ReadByte(a.FindOne + Find(a.FindOne, 0x40, "48 6B DD ??") + 3);
            a.CellSize = Marshal.ReadInt32(a.FindOne + Find(a.FindOne, 0x60, "48 69 CB ?? ?? ?? ??") + 3);
            int typ = Find(a.FindOne, 0x60, "42 0F B6 84 31 ?? ?? ?? ?? 3C 0E 75");
            a.TypOffset = Marshal.ReadInt32(a.FindOne + typ + 5);
            Find(a.FindOne, 0xC0, "3C 0F 75"); /* the SCORR branch */

            if (a.RowNo != ROWNO || a.CellSize < 16 || a.CellSize > 4096 || a.TypOffset < 0 || a.TypOffset >= a.CellSize)
                throw new InvalidOperationException("unexpected level map layout: rows " + a.RowNo + ", cell " + a.CellSize + ", typ at " + a.TypOffset);
            for (int x = 0; x < 80; x++)
                for (int y = 0; y < a.RowNo; y++)
                    if (a.Typ(x, y) >= MaxTyp || x == 0 && a.Typ(x, y) != 0)
                        throw new InvalidOperationException("the level map does not look like terrain at " + x + "," + y + "; not touching it");
            Log.Write("engine resolved: levl cells of " + a.CellSize + " bytes, typ at +" + a.TypOffset);
            return _resolved = a;
        }

        private static IntPtr DebugFlag(IntPtr mod)
        {
            /* LibSetAutoDig: test ecx,ecx; setne byte [rip+d]; ret */
            IntPtr autodig = Rip(Export(mod, "LibSetAutoDig") + Expect(Export(mod, "LibSetAutoDig"), 0, "85 C9 0F 95 05 ?? ?? ?? ?? C3") + 5, 0);
            /* LibGetCharacterClickAction: movsx eax, byte [rip+d]; ret */
            IntPtr clickFn = Export(mod, "LibGetCharacterClickAction");
            IntPtr click = Rip(clickFn + Expect(clickFn, 0, "0F ?? 05 ?? ?? ?? ?? C3") + 3, 0);
            IntPtr flags = autodig - AutodigOffset;
            if ((long)click - (long)flags != SelfClickOffset)
                throw new InvalidOperationException("struct flag layout changed in this game build; not touching memory");
            foreach (int off in new[] { DebugOffset, ExploreOffset })
                if (Marshal.ReadByte(flags + off) > 1)
                    throw new InvalidOperationException("flags+" + off + " is not a boolean in this game build; not touching memory");
            return flags + DebugOffset;
        }

        private static IntPtr Export(IntPtr mod, string name)
        {
            IntPtr p = GetProcAddress(mod, name);
            return p != IntPtr.Zero ? p : throw new MissingMethodException("gnollhackwin.dll", name);
        }

        /* Address an instruction's RIP-relative operand refers to: dispAt points at the
           32-bit displacement, followed by `trailing` immediate bytes ending the instruction. */
        private static IntPtr Rip(IntPtr dispAt, int trailing) => dispAt + 4 + trailing + Marshal.ReadInt32(dispAt);

        private static int?[] Pattern(string hex) =>
            hex.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(b => b == "??" ? (int?)null : Convert.ToInt32(b, 16)).ToArray();

        private static bool MatchesAt(IntPtr code, int at, int?[] pat)
        {
            for (int i = 0; i < pat.Length; i++)
                if (pat[i] != null && Marshal.ReadByte(code + at + i) != pat[i])
                    return false;
            return true;
        }

        private static int Expect(IntPtr code, int at, string hex)
        {
            if (!MatchesAt(code, at, Pattern(hex)))
                throw new InvalidOperationException("unexpected engine code (" + hex + "); this game build is not supported");
            return at;
        }

        /* Offset of the first match of hex within the first `within` bytes of code. */
        private static int Find(IntPtr code, int within, string hex)
        {
            int?[] pat = Pattern(hex);
            for (int at = 0; at < within; at++)
                if (MatchesAt(code, at, pat))
                    return at;
            throw new InvalidOperationException("engine code not found (" + hex + "); this game build is not supported");
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string name);
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)] private static extern IntPtr GetProcAddress(IntPtr mod, string name);
    }

    /* ---------------- the game's UI, by reflection ---------------- */

    internal static class Game
    {
        private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;
        private static Assembly _gh;
        private static Type _mainThread;
        private static readonly Dictionary<string, Type> _types = new Dictionary<string, Type>();

        public static Type T(string simpleName)
        {
            lock (_types)
            {
                if (_types.TryGetValue(simpleName, out Type known))
                    return known;
            }
            _gh = _gh ?? AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.GetName().Name == "GnollHackM")
                ?? throw new InvalidOperationException("GnollHackM assembly not loaded");
            Type[] types;
            try { types = _gh.GetTypes(); }
            catch (ReflectionTypeLoadException e) { types = e.Types.Where(x => x != null).ToArray(); }
            Type t = types.FirstOrDefault(x => x.Name == simpleName) ?? throw new MissingMemberException("type " + simpleName);
            lock (_types)
                _types[simpleName] = t;
            return t;
        }

        public static Type GHApp => T("GHApp");

        public static object Get(object obj, string name)
        {
            if (obj == null)
                return null;
            Type t = obj as Type ?? obj.GetType();
            object target = obj is Type ? null : obj;
            for (Type c = t; c != null; c = c.BaseType)
            {
                FieldInfo f = c.GetField(name, All | BindingFlags.DeclaredOnly);
                if (f != null)
                    return f.GetValue(target);
                PropertyInfo p = c.GetProperties(All | BindingFlags.DeclaredOnly).FirstOrDefault(x => x.Name == name && x.GetIndexParameters().Length == 0);
                if (p != null)
                    return p.GetValue(target);
            }
            throw new MissingMemberException(t.Name + "." + name);
        }

        public static object Call(object obj, string name, params object[] args)
        {
            Type t = obj as Type ?? obj.GetType();
            object target = obj is Type ? null : obj;
            MethodInfo m = t.GetMethods(All).FirstOrDefault(x => x.Name == name && x.GetParameters().Length == args.Length)
                ?? throw new MissingMethodException(t.Name, name);
            return m.Invoke(target, args);
        }

        private static bool Visible(object view) => view != null && (bool)Get(view, "IsVisible");

        public static T Ui<T>(Func<T> f, int timeoutMs = 5000)
        {
            _mainThread = _mainThread ?? AppDomain.CurrentDomain.GetAssemblies().First(x => x.GetName().Name == "Microsoft.Maui.Essentials")
                .GetType("Microsoft.Maui.ApplicationModel.MainThread", true);
            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            Action act = () =>
            {
                try { tcs.SetResult(f()); }
                catch (Exception e) { tcs.SetException(e); }
            };
            _mainThread.GetMethod("BeginInvokeOnMainThread", new[] { typeof(Action) }).Invoke(null, new object[] { act });
            if (!tcs.Task.Wait(timeoutMs))
                throw new TimeoutException("UI thread did not respond in " + timeoutMs + " ms");
            return tcs.Task.Result;
        }

        private static object Page()
        {
            object top = Call(GHApp, "PageFromTopOfModalNavigationStack") ?? Get(GHApp, "CurrentMainPage");
            return top?.GetType().Name == "GamePage" ? top : null;
        }

        private static readonly string[] Overlays = { "LoadingGrid", "TipView", "MoreCommandsGrid", "GetLineGrid", "PopupGrid", "YnGrid", "MenuGrid", "TextGrid" };

        public static bool AtCommandPrompt()
        {
            object p = Page();
            return p != null && Get(GHApp, "CurrentGHGame") != null && !(bool)Get(GHApp, "PushingModalPage")
                && !Overlays.Any(o => Visible(Get(p, o))) && !(bool)Get(p, "ShowDirections");
        }

        /* True once the engine thread has drained a no-op response, i.e. it is sitting in an
           input wait, and the UI has drained the engine's requests. */
        public static bool EngineIdle(int timeoutMs)
        {
            object game = Get(GHApp, "CurrentGHGame");
            if (game == null)
                return false;
            object responses = Get(game, "ResponseQueue"), requests = Get(game, "RequestQueue");
            object ping = Activator.CreateInstance(T("GHResponse"), game, Enum.Parse(T("GHRequestType"), "TallyRealTime"));
            Call(responses, "Enqueue", ping);
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                if ((bool)Get(responses, "IsEmpty") && (bool)Get(requests, "IsEmpty"))
                    return true;
                Thread.Sleep(15);
            }
            return false;
        }
    }
}
