// Map the level for GnollHack (Steam, Windows). Loaded into GnollHackM.exe through
// DOTNET_STARTUP_HOOKS by "Play GnollHack (mods).cmd".
//
// Ctrl+F in the game window maps the whole current level: walls, floor, doors, stairs,
// furniture and every trap, as the engine's own wizard-mode command #wizmap (^F) does.
// That is a scroll of magic mapping that never fails, plus trap detection. flags.debug is
// switched on only while that command runs, and back off once the engine is waiting for
// input again.
//
// flags.debug is not exported. Two exported one-line setters/getters (LibSetAutoDig,
// LibGetCharacterClickAction) address `flags` RIP-relatively; that pins the struct and
// cross-checks its layout against include/flag.h before anything is written.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
            new Thread(MapLevel.Hotkey.Run) { IsBackground = true, Name = "MapLevelHotkey" }.Start();
            new Thread(MapLevel.Hotkey.RunEventTrigger) { IsBackground = true, Name = "MapLevelEvent" }.Start();
        }
        catch (Exception e)
        {
            MapLevel.Log.Write("startup failed: " + e);
        }
    }
}

namespace MapLevel
{
    internal static class Log
    {
        private static readonly string PathName = System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(typeof(Log).Assembly.Location) ?? ".", "maplevel.log");

        public static void Write(string msg)
        {
            try { File.AppendAllText(PathName, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss ") + msg + Environment.NewLine); }
            catch { }
        }
    }

    /* ---------------- Ctrl+F hotkey ---------------- */

    internal static class Hotkey
    {
        private const int WH_KEYBOARD_LL = 13, WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101, WM_TIMER = 0x0113;
        private const int VK_F = 0x46, VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private static readonly LowLevelKeyboardProc Proc = Callback; /* kept alive for the native side */
        private static IntPtr _hook;
        private static IntPtr _gameHookSeen;
        private static bool _held;

        /* Low-level hooks run newest first, and the game's own hook (KeyboardHook in
           GnollHackM's MauiProgram) forwards Ctrl+F to the engine and swallows it. So ours
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
           [System.Threading.EventWaitHandle]::OpenExisting('Local\GnollHackMapLevel').Set() */
        public static void RunEventTrigger()
        {
            try
            {
                using (var ev = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\GnollHackMapLevel"))
                    while (ev.WaitOne())
                        Mapper.Trigger();
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
                ? "could not install the Ctrl+F hook, error " + Marshal.GetLastWin32Error()
                : "Ctrl+F hook installed (game hook " + (_gameHookSeen == IntPtr.Zero ? "not found" : "found") + ")");
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
            if (nCode >= 0 && Marshal.ReadInt32(lParam) == VK_F)
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
                        Mapper.Trigger();
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

    /* ---------------- the mapping run ---------------- */

    internal static class Mapper
    {
        private static int _busy;

        public static void Trigger()
        {
            if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
                return;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try { Run(); }
                catch (Exception e) { Log.Write("mapping failed: " + e); }
                finally { Interlocked.Exchange(ref _busy, 0); }
            });
        }

        private static void Run()
        {
            if (!Game.Ui(Game.AtCommandPrompt))
            {
                Log.Write("Ctrl+F ignored: the game is not waiting for a command");
                return;
            }
            if (!Game.EngineIdle(1500))
            {
                Log.Write("Ctrl+F ignored: the game is busy");
                return;
            }
            IntPtr debug = Engine.DebugFlag();
            bool wasWizard = Marshal.ReadByte(debug) != 0;
            try
            {
                if (!wasWizard)
                    Marshal.WriteByte(debug, 1);
                Game.Ui(() => Game.Call(Game.GHApp, "SendKeyPress", (int)'f', true, false));
                /* The ^F response is queued ahead of this ping, so the engine drains the ping
                   only after wiz_map() has returned and it waits for input again. */
                bool done = Game.EngineIdle(10000);
                Log.Write(done ? "mapped the level" : "the engine did not finish the command in time");
            }
            finally
            {
                if (!wasWizard)
                    Marshal.WriteByte(debug, 0);
            }
        }
    }

    /* ---------------- engine memory ---------------- */

    internal static class Engine
    {
        /* include/flag.h, struct flag: all booleans are single bytes from the top.
           acoustics 0, autodig 1, ... dark_room 10, debug 11, ... explore 14, ... self_click_action 33 */
        private const int AutodigOffset = 1, DebugOffset = 11, ExploreOffset = 14, SelfClickOffset = 33;
        private static IntPtr _debug;

        public static IntPtr DebugFlag()
        {
            if (_debug != IntPtr.Zero)
                return _debug;
            IntPtr mod = GetModuleHandle("gnollhackwin.dll");
            if (mod == IntPtr.Zero)
                throw new InvalidOperationException("gnollhackwin.dll is not loaded");
            /* LibSetAutoDig: test ecx,ecx; setne byte [rip+d]; ret */
            IntPtr autodig = RipTarget(Export(mod, "LibSetAutoDig"), new byte[] { 0x85, 0xC9, 0x0F, 0x95, 0x05 });
            /* LibGetCharacterClickAction: movsx/movzx eax, byte [rip+d]; ret */
            IntPtr click = RipTarget(Export(mod, "LibGetCharacterClickAction"), new byte[] { 0x0F, 0xBE, 0x05 }, new byte[] { 0x0F, 0xB6, 0x05 });
            IntPtr flags = autodig - AutodigOffset;
            if ((long)click - (long)flags != SelfClickOffset)
                throw new InvalidOperationException("struct flag layout changed in this game build (autodig/self_click_action are "
                    + ((long)click - (long)autodig) + " bytes apart, expected " + (SelfClickOffset - AutodigOffset) + "); not touching memory");
            foreach (int off in new[] { DebugOffset, ExploreOffset })
                if (Marshal.ReadByte(flags + off) > 1)
                    throw new InvalidOperationException("flags+" + off + " is not a boolean in this game build; not touching memory");
            _debug = flags + DebugOffset;
            return _debug;
        }

        private static IntPtr Export(IntPtr mod, string name)
        {
            IntPtr p = GetProcAddress(mod, name);
            return p != IntPtr.Zero ? p : throw new MissingMethodException("gnollhackwin.dll", name);
        }

        /* Target of the instruction at fn, which must start with one of the opcode prefixes
           and continue with a 32-bit RIP displacement that ends the instruction. */
        private static IntPtr RipTarget(IntPtr fn, params byte[][] prefixes)
        {
            foreach (byte[] pre in prefixes)
            {
                if (Enumerable.Range(0, pre.Length).All(i => Marshal.ReadByte(fn + i) == pre[i]))
                    return fn + pre.Length + 4 + Marshal.ReadInt32(fn + pre.Length);
            }
            throw new InvalidOperationException("unexpected code at an engine export; this game build is not supported");
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
