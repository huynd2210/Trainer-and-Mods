#if SELFTEST
using BepInEx;
using UnityEngine;

namespace PTTR.GunSpawner;

// Built only with -c SelfTest. Loads a level, spawns every configured gun through the same code path as the
// hotkey, checks each one really appeared as a WeaponGun, picks one up, screenshots, then quits the game.
public sealed class SelfTest : MonoBehaviour
{
    private static readonly string Level = Environment.GetEnvironmentVariable("PTTR_SELFTEST_LEVEL") ?? "BikerBar";
    private const float MenuWaitSeconds = 15f;
    private const float LevelSettleSeconds = 8f;
    private const float PerGunSeconds = 1.5f;
    private const float WallClockLimitSeconds = 300f;

    private enum Step { WaitMenu, WaitLevel, Spawn, Pickup, Shot, Fired, Done }

    private WeaponGun _held;

    private void LogGunState(string when)
    {
        bool canShoot = _held.HeldByHumanThatCanShoot(out bool consumes);
        Log($"{when}: {_held.name} CanAttack={_held.CanAttack()} HeldByHumanThatCanShoot={canShoot} consumesAmmo={consumes} ammo={_held.ammo} unlimitedAmmo={_held.unlimitedAmmo}");
    }

    // Native entry points, so the IL2CPP code behind a gate can be disassembled from GameAssembly.dll.
    private void LogRvas()
    {
        var module = System.Diagnostics.Process.GetCurrentProcess().Modules.Cast<System.Diagnostics.ProcessModule>()
            .First(m => m.ModuleName.Equals("GameAssembly.dll", StringComparison.OrdinalIgnoreCase));
        foreach (var f in typeof(WeaponGun).GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static))
        {
            if (!f.Name.StartsWith("NativeMethodInfoPtr_")) continue;
            var mi = (IntPtr)f.GetValue(null);
            if (mi == IntPtr.Zero) continue;
            long rva = System.Runtime.InteropServices.Marshal.ReadIntPtr(mi).ToInt64() - module.BaseAddress.ToInt64();
            Log($"RVA WeaponGun.{f.Name.Substring(20)} = 0x{rva:X}");
        }
    }

    private Step _step;
    private float _stepStart;
    private int _gun;
    private readonly HashSet<int> _seenGuns = new();
    private readonly List<string> _results = new();
    private readonly List<WeaponGun> _spawned = new();
    private static string ShotDir => Path.Combine(Paths.BepInExRootPath, "selftest");

    public SelfTest(IntPtr ptr) : base(ptr) { }

    private void Log(string s) => Plugin.Logger.LogInfo("[SelfTest] " + s);

    private void Go(Step s)
    {
        _step = s;
        _stepStart = Time.realtimeSinceStartup;
    }

    private float InStep => Time.realtimeSinceStartup - _stepStart;

    private void Update()
    {
        if (Time.realtimeSinceStartup > WallClockLimitSeconds && _step != Step.Done)
        {
            Log($"TIMEOUT in step {_step}");
            Finish();
            return;
        }

        try { Tick(); }
        catch (Exception e)
        {
            Log($"EXCEPTION in step {_step}: {e}");
            Finish();
        }
    }

    private void Tick()
    {
        switch (_step)
        {
            case Step.WaitMenu:
                if (Time.realtimeSinceStartup < MenuWaitSeconds || GameManager.Instance == null) return;
                Log($"Loading {Level} (current: {GameManager.CurrentLevel})");
                GameManager.LoadLevel(Level, true);
                Go(Step.WaitLevel);
                break;

            case Step.WaitLevel:
                if (EntityManager.MainPlayer == null || Camera.main == null || GameManager.LoadingNewLevel)
                {
                    _stepStart = Time.realtimeSinceStartup;
                    return;
                }
                if (InStep < LevelSettleSeconds) return;
                Log($"In level {GameManager.CurrentLevel}; player at {EntityManager.MainPlayer.transform.position}");
                LogRvas();
                Directory.CreateDirectory(ShotDir);
                ScreenCapture.CaptureScreenshot(Path.Combine(ShotDir, "0-before.png"));
                foreach (var g in UnityEngine.Object.FindObjectsOfType<WeaponGun>()) _seenGuns.Add(g.GetInstanceID());
                Log($"WeaponGun instances already in level: {_seenGuns.Count}");
                _gun = -1;
                Go(Step.Spawn);
                break;

            case Step.Spawn:
                if (_gun >= 0 && InStep < PerGunSeconds) return;
                if (_gun >= 0) CheckSpawned(GunSpawnerState.Guns[_gun]);
                _gun++;
                if (_gun >= GunSpawnerState.Guns.Length)
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(ShotDir, "1-spawned.png"));
                    Go(Step.Pickup);
                    return;
                }
                if (!GunSpawnerBehaviour.TrySpawn(GunSpawnerState.Guns[_gun], out string err))
                    Log($"TrySpawn refused: {err}");
                Go(Step.Spawn);
                break;

            case Step.Pickup:
                if (InStep < 1f) return;
                TryPickup();
                Go(Step.Shot);
                break;

            case Step.Shot:
                if (InStep < 2f) return;
                var held = EntityManager.MainPlayer.CurrentWeapon();
                Log($"Holding after pickup: {(held == null ? "nothing" : held.name)}");
                _held = held == null ? null : held.TryCast<WeaponGun>();
                if (_held == null) { Go(Step.Done); return; }
                LogGunState("before trigger");
                ScreenCapture.CaptureScreenshot(Path.Combine(ShotDir, "2-holding.png"));
                _held.StartAttacking();
                Go(Step.Fired);
                break;

            case Step.Fired:
                if (InStep < 1.5f) return;
                LogGunState("1.5 s after StartAttacking");
                ScreenCapture.CaptureScreenshot(Path.Combine(ShotDir, "3-fired.png"));
                Go(Step.Done);
                break;

            case Step.Done:
                if (InStep < 1.5f) return; // let the screenshot write
                Finish();
                break;
        }
    }

    private void CheckSpawned(string gun)
    {
        var fresh = new List<WeaponGun>();
        foreach (var g in UnityEngine.Object.FindObjectsOfType<WeaponGun>())
            if (_seenGuns.Add(g.GetInstanceID())) fresh.Add(g);
        _spawned.AddRange(fresh);
        string r;
        if (fresh.Count == 1)
        {
            var g = fresh[0];
            var player = EntityManager.MainPlayer.transform.position;
            Camera cam = Camera.main;
            Vector3 vp = cam.WorldToViewportPoint(g.transform.position);
            bool inFrame = vp.z > 0 && vp.x is > 0 and < 1 && vp.y is > 0 and < 1;
            bool blocked = Physics.Linecast(cam.transform.position, g.transform.position + Vector3.up * 0.1f, out RaycastHit hit)
                && !hit.transform.IsChildOf(g.transform) && !hit.transform.IsChildOf(EntityManager.MainPlayer.transform.root)
                && Vector3.Distance(hit.point, g.transform.position) > 0.3f;
            string visible = inFrame ? (blocked ? $"in frame but behind {hit.transform.name}" : $"yes at viewport ({vp.x:F2},{vp.y:F2})") : $"no, viewport ({vp.x:F2},{vp.y:F2})";
            r = $"PASS {gun} -> {g.name} at {g.transform.position} ({Vector3.Distance(g.transform.position, player):F1} m from player, visible: {visible})";
        }
        else
        {
            r = $"FAIL {gun} -> {fresh.Count} new WeaponGun [{string.Join(", ", fresh.Select(f => f.name))}]";
        }
        _results.Add(r);
        Log(r);
    }

    private void LogWeaponTable()
    {
        var em = EntityManager.Instance;
        if (em == null || em.allWeaponPaths == null) { Log("No EntityManager weapon table"); return; }
        var paths = em.allWeaponPaths;
        Log($"allWeaponPaths: {paths.Length}");
        for (int i = 0; i < paths.Length; i++)
        {
            string p = paths[i];
            string kind = "?";
            try
            {
                var prefab = EntityManager.LoadPrefab(p);
                kind = prefab == null ? "no-prefab" : prefab.GetComponentInChildren<WeaponGun>(true) != null ? "GUN" : "weapon";
            }
            catch (Exception e) { kind = "err " + e.GetType().Name; }
            Log($"  [{i}] {p} : {kind}");
        }
    }

    private void TryPickup()
    {
        var player = EntityManager.MainPlayer;
        var gun = _spawned.FirstOrDefault(g => g != null);
        if (gun == null) { Log("Pickup: none of the spawned guns still exist"); return; }
        Log($"Pickup: {gun.name} at {Vector3.Distance(gun.transform.position, player.transform.position):F1} m");
        player.PickUpWeapon(gun);
    }

    private void Finish()
    {
        Log($"SUMMARY {_results.Count(r => r.StartsWith("PASS"))}/{GunSpawnerState.Guns.Length} pass");
        foreach (var r in _results) Log("  " + r);
        Log("Quitting.");
        _step = Step.Done;
        enabled = false;
        Application.Quit();
    }
}
#endif
