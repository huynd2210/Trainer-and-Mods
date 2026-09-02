using System;
using System.Runtime.InteropServices;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace DisfigureTrainer;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public class TrainerPlugin : BasePlugin
{
    public const string PluginGuid = "com.local.disfigure.trainer";
    public const string PluginName = "Disfigure Trainer";
    public const string PluginVersion = "1.2.0";

    internal static ManualLogSource TrainerLog { get; private set; }
    internal static GameSpeedController SpeedController { get; private set; }

    public override void Load()
    {
        TrainerLog = Log;
        SpeedController = new GameSpeedController();

        // Nothing in Load may take the game down with it: a plugin that throws
        // here is logged and skipped, but the game still has to reach its menu.
        try
        {
            ClassInjector.RegisterTypeInIl2Cpp<TrainerBehaviour>();
            AddComponent<TrainerBehaviour>();
        }
        catch (Exception ex)
        {
            Log.LogError($"{PluginName} failed to attach; game continues without it: {ex}");
            return;
        }

        Log.LogInfo($"{PluginName} {PluginVersion} loaded. Hotkeys: F1 god | F2 one-shot | F3 +100k credits | F4 level up | F5 kill all | F6 XP x10 | F7 XP magnet | 1/2 game speed, 0 reset");
    }
}

public class TrainerBehaviour : MonoBehaviour
{
    // The game uses the new Input System only, so hotkeys are polled through WinAPI.
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private const int VK_F1 = 0x70; // god mode
    private const int VK_F2 = 0x71; // one-shot kills
    private const int VK_F3 = 0x72; // +1000 credits
    private const int VK_F4 = 0x73; // trigger level up
    private const int VK_F5 = 0x74; // kill all enemies
    private const int VK_F6 = 0x75; // XP x10
    private const int VK_F7 = 0x76; // infinite XP magnetism
    private const int VK_1 = 0x31; // game speed +20%
    private const int VK_2 = 0x32; // game speed -20%
    private const int VK_0 = 0x30; // game speed reset to 1x

    private static readonly int[] WatchedKeys = { VK_F1, VK_F2, VK_F3, VK_F4, VK_F5, VK_F6, VK_F7, VK_1, VK_2, VK_0 };
    private readonly bool[] _keyWasDown = new bool[WatchedKeys.Length];

    public bool GodMode;
    public bool OneShot;
    public bool XpBoost;
    public bool XpMagnet;
    private float _xpBoostOriginal = float.NaN;
    private float _magnetRetryTimer;

    private const float GameSpeedStep = 0.2f;
    private int _lastSceneHandle = int.MinValue;

    private PlayerStats _playerStats;
    private WeaponManager _weaponManager;
    private bool _originalTakeNoDamage;
    private bool _originalTakeDamageButNoDying;
    private DateTime _nextErrorLogUtc;

    public TrainerBehaviour(IntPtr ptr) : base(ptr) { }

    public void Update()
    {
        try
        {
            for (int i = 0; i < WatchedKeys.Length; i++)
            {
                bool down = (GetAsyncKeyState(WatchedKeys[i]) & 0x8000) != 0;
                if (down && !_keyWasDown[i])
                    HandleKey(i);
                _keyWasDown[i] = down;
            }

            if (GodMode)
                ApplyGodMode();

            if (XpMagnet)
                ApplyXpMagnet();

            // The game rewrites timeScale around its own pause, so a chosen speed
            // has to be re-asserted rather than set once.
            TrainerPlugin.SpeedController.Tick();

            WatchSceneForSpeedReset();
        }
        catch (Exception ex)
        {
            LogTrainerError("Update", ex);
        }
    }

    private void HandleKey(int index)
    {
        var speed = TrainerPlugin.SpeedController;

        switch (index)
        {
            case 0:
                GodMode = !GodMode;
                ApplyGodMode();
                break;
            case 1:
                OneShot = !OneShot;
                if (_playerStats != null)
                    _playerStats.oneShot = OneShot;
                break;
            case 2:
                CreditsWallet.AddCredits(100000f);
                break;
            case 3:
                var ps = FindPlayer();
                if (ps != null)
                    ps.levelUp(true); // game's own cheat path: skips weapon-upgrade offer
                break;
            case 4:
                var psk = FindPlayer();
                if (psk != null)
                    psk.killAllEnemies = true; // game logic consumes and resets this flag
                break;
            case 5:
                XpBoost = !XpBoost;
                ApplyXpBoost();
                break;
            case 6:
                ToggleXpMagnet();
                break;
            case 7:
                speed.SetSpeed(speed.Speed + GameSpeedStep);
                break;
            case 8:
                speed.SetSpeed(speed.Speed - GameSpeedStep);
                break;
            case 9:
                speed.Restore();
                break;
        }
    }

    private void ApplyGodMode()
    {
        var wm = FindWeaponManager();
        if (wm == null)
            return;

        wm.takeNoDamage = GodMode || _originalTakeNoDamage;
        wm.takeDamageButNoDying = GodMode || _originalTakeDamageButNoDying;

        if (GodMode)
        {
            var stats = wm.pS;
            int maxHealth = stats != null && stats ? stats.getMaxHealth() : 0;
            if (maxHealth <= 0 && wm.HPBar != null && wm.HPBar)
                maxHealth = Math.Max(1, (int)wm.HPBar.maxHealth);
            if (maxHealth > 0 && wm.getCurrentHealth() < maxHealth)
                wm.setCurrentHealth(maxHealth);
        }
    }

    private void ApplyXpBoost()
    {
        var ps = FindPlayer();
        if (ps == null)
            return;
        if (XpBoost)
        {
            if (float.IsNaN(_xpBoostOriginal))
                _xpBoostOriginal = ps.expGainBuff;
            ps.expGainBuff = 10f;
        }
        else
        {
            if (!float.IsNaN(_xpBoostOriginal))
                ps.expGainBuff = _xpBoostOriginal;
            _xpBoostOriginal = float.NaN;
        }
    }

    private void ApplyXpMagnet()
    {
        var em = ExpManager.instance;
        if (em == null || !em)
            return;

        // Throttle so we don't hammer the game's collection routine every frame.
        _magnetRetryTimer -= Time.deltaTime;
        if (_magnetRetryTimer > 0f)
            return;

        if (em.HasAnyActiveGroundOrb())
        {
            em.CollectAllGroundOrbs();
            _magnetRetryTimer = 0.1f;
        }
    }

    private void ToggleXpMagnet()
    {
        XpMagnet = !XpMagnet;
        _magnetRetryTimer = 0f;
    }

    private void WatchSceneForSpeedReset()
    {
        try
        {
            int handle = UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;
            if (handle == _lastSceneHandle)
                return;

            bool firstTick = _lastSceneHandle == int.MinValue;
            _lastSceneHandle = handle;

            // A level transition happened: restore normal speed so the wave/spawn
            // directors of the next scene start from clean, vanilla timing.
            if (!firstTick && !Mathf.Approximately(TrainerPlugin.SpeedController.Speed, 1f))
                TrainerPlugin.SpeedController.Restore();
        }
        catch (Exception)
        {
        }
    }

    public void OnDestroy()
    {
        // Do not leave global Unity timing modified if the trainer component is
        // unloaded or destroyed while a run is active.
        TrainerPlugin.SpeedController.Restore();
        GodMode = false;
        ApplyGodMode();
    }

    private PlayerStats FindPlayer()
    {
        if (_playerStats != null && _playerStats)
            return _playerStats;
        _playerStats = UnityEngine.Object.FindObjectOfType<PlayerStats>();
        return _playerStats;
    }

    private WeaponManager FindWeaponManager()
    {
        if (_weaponManager != null && _weaponManager)
            return _weaponManager;

        _weaponManager = UnityEngine.Object.FindObjectOfType<WeaponManager>();
        if (_weaponManager != null && _weaponManager)
        {
            _originalTakeNoDamage = _weaponManager.takeNoDamage;
            _originalTakeDamageButNoDying = _weaponManager.takeDamageButNoDying;
        }

        return _weaponManager;
    }

    private void LogTrainerError(string context, Exception ex)
    {
        DateTime now = DateTime.UtcNow;
        if (now < _nextErrorLogUtc)
            return;

        _nextErrorLogUtc = now.AddSeconds(5);
        TrainerPlugin.TrainerLog.LogError($"Trainer {context} failed: {ex}");
    }

    public void OnGUI()
    {
        try
        {
            string text =
                "DISFIGURE TRAINER\n" +
                $"[F1] God mode: {(GodMode ? "ON" : "off")}\n" +
                $"[F2] One-shot: {(OneShot ? "ON" : "off")}\n" +
                "[F3] +100k credits\n" +
                "[F4] Level up\n" +
                "[F5] Kill all enemies\n" +
                $"[F6] XP x10: {(XpBoost ? "ON" : "off")}\n" +
                $"[F7] XP magnet: {(XpMagnet ? "ON" : "off")}\n" +
                $"[1]/[2] Game speed: x{TrainerPlugin.SpeedController.Speed:0.0}  ([0] reset)";
            GUI.Label(new Rect(12f, 12f, 260f, 140f), text);
        }
        catch (Exception)
        {
        }
    }
}
