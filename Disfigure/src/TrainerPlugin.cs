using System;
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
    public const string PluginVersion = "1.3.0";

    internal static ManualLogSource TrainerLog { get; private set; }
    internal static GameSpeedController SpeedController { get; private set; }
    internal static Hotkey[] Keys { get; private set; }

    public override void Load()
    {
        TrainerLog = Log;
        Keys = TrainerBindings.Load(Config);
        try { SpeedController = new GameSpeedController(); }
        catch (Exception ex) { Log.LogError($"Game speed unavailable; trainer remains usable: {ex}"); }

        // Nothing in Load may take the game down with it: a plugin that throws
        // here is logged and skipped, but the game still has to reach its menu.
        try
        {
            ClassInjector.RegisterTypeInIl2Cpp<TrainerBehaviour>();
            AddComponent<TrainerBehaviour>();
        }
        catch (Exception ex)
        {
            SpeedController?.Dispose();
            Log.LogError($"{PluginName} failed to attach; game continues without it: {ex}");
            return;
        }

        Log.LogInfo($"{PluginName} {PluginVersion} loaded. Keybindings: {Config.ConfigFilePath}");
    }
}

public class TrainerBehaviour : MonoBehaviour
{
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
            WatchSceneForSpeedReset();
            for (int i = 0; i < TrainerPlugin.Keys.Length; i++)
            {
                if (TrainerPlugin.Keys[i].Pressed(Application.isFocused))
                    HandleKey(i);
            }

            if (GodMode)
                ApplyGodMode();

            if (XpMagnet)
                ApplyXpMagnet();


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
                if (speed != null) speed.SetSpeed(speed.Speed + GameSpeedStep);
                break;
            case 8:
                if (speed != null) speed.SetSpeed(speed.Speed - GameSpeedStep);
                break;
            case 9:
                speed?.Restore();
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
            if (!firstTick && TrainerPlugin.SpeedController != null)
                TrainerPlugin.SpeedController?.Restore();
        }
        catch (Exception)
        {
        }
    }

    public void OnDestroy()
    {
        // Do not leave global Unity timing modified if the trainer component is
        // unloaded or destroyed while a run is active.
        TrainerPlugin.SpeedController?.Dispose();
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
                $"[{TrainerPlugin.Keys[0].Label}] God mode: {(GodMode ? "ON" : "off")}\n" +
                $"[{TrainerPlugin.Keys[1].Label}] One-shot: {(OneShot ? "ON" : "off")}\n" +
                $"[{TrainerPlugin.Keys[2].Label}] +100k credits\n" +
                $"[{TrainerPlugin.Keys[3].Label}] Level up\n" +
                $"[{TrainerPlugin.Keys[4].Label}] Kill all enemies\n" +
                $"[{TrainerPlugin.Keys[5].Label}] XP x10: {(XpBoost ? "ON" : "off")}\n" +
                $"[{TrainerPlugin.Keys[6].Label}] XP magnet: {(XpMagnet ? "ON" : "off")}\n" +
                $"[{TrainerPlugin.Keys[7].Label}]/[{TrainerPlugin.Keys[8].Label}] Game speed: " +
                (TrainerPlugin.SpeedController == null ? "unavailable" : $"x{TrainerPlugin.SpeedController.Speed:0.0}") +
                $"  ([{TrainerPlugin.Keys[9].Label}] reset)";
            GUI.Label(new Rect(12f, 12f, 580f, 180f), text);
        }
        catch (Exception)
        {
        }
    }
}
