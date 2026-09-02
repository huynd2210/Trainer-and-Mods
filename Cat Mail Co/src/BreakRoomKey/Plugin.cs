using System.Text;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace CatMailCo.BreakRoomKey;

// The break room key prefab ("p_Entity_Carryable_UnlockKey_Key_BreakRoom") is
// referenced by the game scene but was never included in the addressables build,
// so the game's key spawn silently fails and the break room can never be opened.
// This mod repairs that once per save: when a game starts it finds the locked
// break room door, unlocks it, and drops a replacement key near it.
[BepInPlugin(Guid, Name, Version)]
[BepInProcess("CatMailCo.exe")]
public sealed class Plugin : BasePlugin
{
    public const string Guid = "com.catmailco.breakroomkey";
    public const string Name = "Break Room Key";
    public const string Version = "1.5.0";

    internal static Plugin Instance { get; private set; } = null!;

    public override void Load()
    {
        Instance = this;
        BreakRoomKeyState.Initialize(Config);

        AddComponent<BreakRoomKeyFixer>();
        Log.LogInfo($"{Name} {Version} loaded. Fixes the missing break room key on game start.");
    }
}

internal static class BreakRoomKeyState
{
    private static ConfigEntry<bool> _autoUnlock = null!;
    private static ConfigEntry<bool> _spawnKey = null!;

    internal static bool AutoUnlock => _autoUnlock.Value;
    internal static bool SpawnKey => _spawnKey.Value;

    internal static void Initialize(ConfigFile config)
    {
        _autoUnlock = config.Bind("Behavior", "AutoUnlockDoor", true,
            "Automatically unlock the break room door. The intended key prefab is missing from the game build, so this guarantees the door can be opened even if the spawned key cannot be validated by the game.");
        _spawnKey = config.Bind("Behavior", "SpawnKey", true,
            "Spawn a replacement break room key near the break room so it can be found and picked up.");
    }
}

internal sealed class BreakRoomKeyFixer : MonoBehaviour
{
    private const float ScanInterval = 1.5f;
    private const float GiveUpAfterSeconds = 120f;
    private const float StatusLogInterval = 10f;

    private bool _finished;
    private float _nextScanTime;
    private float _deadline;
    private float _lastStatusLogTime;
    private bool _loggedDoors;

    private void Awake()
    {
        _deadline = Time.time + GiveUpAfterSeconds;
    }

    // Keeps scanning every few seconds until the break room door is found in its
    // settled (locked) state. A single check at the exact frame the game flips to
    // "started" is racy — the door's locked state is set during world init, which
    // can land a frame later — so we retry instead of giving up after one attempt.
    private void Update()
    {
        if (_finished)
            return;

        if (Time.time < _nextScanTime)
            return;

        _nextScanTime = Time.time + ScanInterval;

        if (Time.time > _deadline)
        {
            Plugin.Instance.Log.LogInfo($"{Plugin.Name}: gave up waiting for the break room door to become ready.");
            _finished = true;
            return;
        }

        var doors = UnityEngine.Object.FindObjectsOfType<EntityUnlockable>();
        if (doors == null || doors.Length == 0)
        {
            LogStatus($"no unlockable door found yet; world may still be loading.");
            return; // world not loaded yet; keep retrying
        }

        if (BreakRoomKeyLogic.TryFix(doors, ref _loggedDoors))
            _finished = true;
    }

    private void LogStatus(string message)
    {
        if (Time.time - _lastStatusLogTime < StatusLogInterval)
            return;
        _lastStatusLogTime = Time.time;
        Plugin.Instance.Log.LogInfo($"{Plugin.Name}: {message}");
    }
}

internal static class BreakRoomKeyLogic
{
    // Returns true when the fix has been applied (or there is genuinely nothing
    // to fix), false when the world is not ready yet and the fixer should retry.
    internal static bool TryFix(EntityUnlockable[] doors, ref bool loggedDoors)
    {
        if (!loggedDoors)
        {
            var header = new StringBuilder();
            header.AppendLine($"{Plugin.Name}: scanning {doors.Length} unlockable door(s).");
            foreach (var door in doors)
            {
                if (door == null)
                    continue;
                header.AppendLine($"  door='{door.gameObject?.name}' locked={door.IsLocked} keyMissing={door.KeyToUnlockPrefab == null} nameHasBreak={NameHasBreak(door)} pos={door.transform.position}");
            }
            Plugin.Instance.Log.LogInfo(header.ToString());
            loggedDoors = true;
        }

        var candidate = FindBreakRoomDoor(doors);

        if (candidate == null)
        {
            // The break room door is not in its locked state yet (world still
            // initializing). Keep scanning.
            Plugin.Instance.Log.LogInfo($"{Plugin.Name}: break room door not ready yet; will keep scanning.");
            return false;
        }

        var log = new StringBuilder();
        log.AppendLine($"  -> selected door: '{candidate.gameObject?.name}' at {candidate.transform.position} (locked={candidate.IsLocked})");

        // The key always spawns so the break room is always unlockable; the door is
        // only force-opened when it is still locked.
        if (candidate.IsLocked && BreakRoomKeyState.AutoUnlock)
        {
            try
            {
                candidate.Unlock(true);
                log.AppendLine("  -> unlocked the break room door.");
            }
            catch (Exception exception)
            {
                log.AppendLine($"  -> unlock failed: {exception.Message}");
            }
        }

        if (BreakRoomKeyState.SpawnKey)
            SpawnKey(candidate, log);
        else
            log.AppendLine("  -> key spawn disabled in config.");

        try
        {
            candidate.SetKeySpawned();
        }
        catch (Exception exception)
        {
            log.AppendLine($"  -> SetKeySpawned failed: {exception.Message}");
        }

        Plugin.Instance.Log.LogInfo(log.ToString());
        return true;
    }

    private static bool NameHasBreak(EntityUnlockable door)
    {
        return door.gameObject != null && door.gameObject.name != null &&
               door.gameObject.name.IndexOf("Break", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    // The break room door is the only unlockable whose key prefab is missing from
    // the build (KeyToUnlockPrefab is null). Prefer that, then a name match, then
    // any locked door. Only act once it is locked so we don't fire mid-initialization.
    private static EntityUnlockable FindBreakRoomDoor(EntityUnlockable[] doors)
    {
        // The break room door is reliably the one named with "Break" (p_BreakRoom),
        // locked or not. We act on it regardless of its locked state so the key is
        // always spawned (the door may be unlocked but still needs its key on hand,
        // or on a fresh save it is simply present).
        foreach (var door in doors)
        {
            if (door != null && NameHasBreak(door))
                return door;
        }

        // Fallbacks: any door with a missing key prefab, then any locked door.
        EntityUnlockable fallbackLocked = null;
        foreach (var door in doors)
        {
            if (door == null)
                continue;

            if (door.KeyToUnlockPrefab == null)
                return door;

            if (door.IsLocked && fallbackLocked == null)
                fallbackLocked = door;
        }

        return fallbackLocked;
    }

    private static void SpawnKey(EntityUnlockable door, StringBuilder log)
    {
        try
        {
            var position = ResolveSpawnPosition(door);
            var handle = Addressables.LoadAssetAsync<GameObject>("p_Entity_Carryable_UnlockKey_Key_BrightRoom");
            handle.WaitForCompletion();
            var prefab = handle.Result;
            if (prefab == null)
            {
                log.AppendLine("  -> key prefab failed to load from addressables.");
                return;
            }

            var instance = UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity);

            var unlockKey = instance.GetComponent<UnlockKey>();
            if (unlockKey != null)
                unlockKey.UnlockType = UnlockType.BreakRoom;

            // Wire the door to accept this key (its original KeyToUnlockPrefab is
            // missing from the build), so holding it and interacting validates.
            var entity = instance.GetComponent<Entity>();
            if (entity != null)
                door.KeyToUnlockPrefab = entity;

            log.AppendLine($"  -> spawned replacement break room key at {position} (active={instance.activeSelf})");
        }
        catch (Exception exception)
        {
            log.AppendLine($"  -> key spawn failed: {exception.Message}");
        }
    }

    // The key must land somewhere the player will actually see it. v1.3.0 spawned
    // it 2 units straight up from the door, which put it inside the door frame —
    // invisible/inaccessible. Prefer the game's own key spawn point, then the
    // player's current position (guaranteed findable), then the door as a last resort.
    private static Vector3 ResolveSpawnPosition(EntityUnlockable door)
    {
        var designed = FindBreakRoomSpawnPoint();
        if (designed.HasValue)
            return designed.Value;

        var player = FindPlayerPosition();
        if (player.HasValue)
            return player.Value;

        Plugin.Instance.Log.LogWarning($"{Plugin.Name}: no key spawn point or player found; dropping the key at the door.");
        return door.transform.position + Vector3.up * 0.5f;
    }

    private static Vector3? FindBreakRoomSpawnPoint()
    {
        try
        {
            var go = GameObject.Find("sp_Unlock_KeyBreakRoom");
            if (go != null)
                return go.transform.position + Vector3.up * 0.5f;
        }
        catch
        {
        }

        try
        {
            foreach (var transform in UnityEngine.Object.FindObjectsOfType<Transform>(true))
            {
                if (transform != null && transform.name != null &&
                    transform.name.IndexOf("KeyBreakRoom", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return transform.position + Vector3.up * 0.5f;
                }
            }
        }
        catch
        {
        }
        return null;
    }

    private static Vector3? FindPlayerPosition()
    {
        try
        {
            var playerEntity = UnityEngine.Object.FindObjectOfType<PlayerEntity>();
            if (playerEntity != null && playerEntity.transform != null)
                return playerEntity.transform.position + Vector3.up * 0.5f;
        }
        catch
        {
        }

        try
        {
            var controller = UnityEngine.Object.FindObjectOfType<PlayerController>();
            if (controller != null && controller.transform != null)
                return controller.transform.position + Vector3.up * 0.5f;
        }
        catch
        {
        }

        return null;
    }
}
