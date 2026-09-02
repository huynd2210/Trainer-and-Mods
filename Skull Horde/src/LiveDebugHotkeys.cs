using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SkullHordeTrainer;

internal sealed class LiveDebugHotkeys : IDisposable
{
    public const int MessageId = 0x0312;
    private readonly IntPtr _window;
    private readonly Action<string, bool> _status;
    private readonly System.Windows.Forms.Timer _timer;
    private int? _gameProcessId;
    private bool _consoleOpen;
    private bool _busy;
    private bool _autoXp;
    private bool _autoMoney;

    private static readonly Dictionary<int, (Keys Key, string Label)> Registrations = new()
    {
        [2] = (Keys.F2, "Auto XP"),
        [3] = (Keys.F3, "Auto money"),
        [5] = (Keys.F5, "Console"),
        [6] = (Keys.F6, "+999 XP"),
        [7] = (Keys.F7, "+999 money"),
        [8] = (Keys.F8, "Invincibility"),
        [9] = (Keys.F9, "Reveal map"),
        [10] = (Keys.F10, "Legendary drop"),
        [11] = (Keys.F11, "Give power"),
        [12] = (Keys.F12, "Next floor"),
    };

    public LiveDebugHotkeys(IntPtr window, Action<string, bool> status)
    {
        _window = window;
        _status = status;
        var failures = new List<string>();
        foreach ((int id, (Keys key, string label)) in Registrations)
            if (!RegisterHotKey(window, id, 0, (uint)key)) failures.Add($"{key} ({label})");
        if (failures.Count > 0)
            _status("Could not register: " + string.Join(", ", failures), true);

        _timer = new System.Windows.Forms.Timer { Interval = 900 };
        _timer.Tick += async (_, _) =>
        {
            if (_autoXp) await SendCommand("givexp");
            if (_autoMoney) await SendCommand("givemoney");
        };
        _timer.Start();
    }

    public void Handle(int id)
    {
        switch (id)
        {
            case 2:
                _autoXp = !_autoXp;
                _status($"Auto XP {(_autoXp ? "ON" : "OFF")}.", false);
                break;
            case 3:
                _autoMoney = !_autoMoney;
                _status($"Auto money {(_autoMoney ? "ON" : "OFF")}.", false);
                break;
            case 5:
                _ = ToggleConsole();
                break;
            case 6: _ = SendCommand("givexp"); break;
            case 7: _ = SendCommand("givemoney"); break;
            case 8: _ = SendCommand("cheatinvincible"); break;
            case 9: _ = SendCommand("seemap"); break;
            case 10: _ = SendCommand("droplegendary"); break;
            case 11: _ = SendCommand("givepower"); break;
            case 12: _ = SendCommand("nextfloor"); break;
        }
    }

    public void Run(string command) => _ = SendCommand(command);

    private async Task ToggleConsole()
    {
        Process? game = FindGame();
        if (game is null) return;
        FocusAndType(game, "console");
        await Task.Delay(80);
        _consoleOpen = !_consoleOpen;
        _status($"Developer console {(_consoleOpen ? "OPEN" : "CLOSED")}.", false);
    }

    private async Task SendCommand(string command)
    {
        if (_busy) return;
        _busy = true;
        try
        {
            Process? game = FindGame();
            if (game is null) return;
            if (!_consoleOpen)
            {
                FocusAndType(game, "console");
                _consoleOpen = true;
                await Task.Delay(120);
            }
            FocusAndType(game, command);
            _status($"Sent live command: {command}", false);
        }
        catch (Exception ex)
        {
            _status($"Live command failed: {ex.Message}", true);
        }
        finally
        {
            _busy = false;
        }
    }

    private Process? FindGame()
    {
        Process? game = Process.GetProcessesByName("SkullHorde").FirstOrDefault();
        if (game is null)
        {
            _consoleOpen = false;
            _gameProcessId = null;
            _status("Skull Horde is not running.", true);
            return null;
        }
        if (_gameProcessId != game.Id)
        {
            _gameProcessId = game.Id;
            _consoleOpen = false;
        }
        game.Refresh();
        if (game.MainWindowHandle == IntPtr.Zero)
        {
            _status("Skull Horde has no active game window yet.", true);
            return null;
        }
        return game;
    }

    private static void FocusAndType(Process game, string text)
    {
        ShowWindow(game.MainWindowHandle, 9);
        SetForegroundWindow(game.MainWindowHandle);
        SendKeys.SendWait(text);
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
        foreach (int id in Registrations.Keys) UnregisterHotKey(_window, id);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
