using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using UnityEngine;

namespace PTTR.GunSpawner;

[BepInPlugin(Guid, Name, Version)]
[BepInProcess("PaintTheTownRed.exe")]
public sealed class Plugin : BasePlugin
{
    public const string Guid = "com.pttr.gunspawner";
    public const string Name = "Gun Spawner";
    public const string Version = "1.0.0";

    internal static ManualLogSource Logger { get; private set; }

    public override void Load()
    {
        Logger = Log;
        GunSpawnerState.Initialize(Config);
        AddComponent<GunSpawnerBehaviour>();
#if SELFTEST
        AddComponent<SelfTest>();
#endif
        Log.LogInfo($"{Name} {Version} loaded. {GunSpawnerState.SpawnKey}: spawn next gun, Shift+{GunSpawnerState.SpawnKey}: previous. {GunSpawnerState.Guns.Length} guns in rotation.");
    }
}

internal static class GunSpawnerState
{
    // Resource paths the game's own EntityManager.CreateWeapon accepts. All are shipped game prefabs.
    private const string DefaultGuns =
        "Extra/AK47, Extra/M4, Extra/MP5, Extra/Pistol1911, Extra/AutoRevolver, Saloon/Revolver, " +
        "PirateCove/PiratePistol, Prison/Shotgun, Beneath/DoubleShotgun, Extra/Nailgun, Beneath/RangedTaser, " +
        "Beneath/Crossbow, Beneath/TripleCrossbow, Beneath/Speargun, Beneath/Harpoongun";

    internal const string PathPrefix = "NetObjects/Weapons/";

    private static ConfigEntry<KeyCode> _spawnKey;
    private static ConfigEntry<string> _guns;
    private static ConfigEntry<float> _spawnDistance;

    internal static KeyCode SpawnKey => _spawnKey.Value;
    internal static float SpawnDistance => _spawnDistance.Value;
    internal static string[] Guns { get; private set; }

    internal static void Initialize(ConfigFile config)
    {
        _spawnKey = config.Bind("Controls", "SpawnKey", KeyCode.F7,
            "Spawns the next gun in front of you. Hold Shift to step back to the previous gun instead.");
        _spawnDistance = config.Bind("Spawn", "Distance", 3f,
            "How far ahead the gun lands, in metres: where you aim if that is closer, else on the floor this far ahead.");
        _guns = config.Bind("Spawn", "Guns", DefaultGuns,
            "Comma-separated weapon paths under NetObjects/Weapons/ to cycle through, in order.");
        Guns = _guns.Value.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
    }

    internal static string DisplayName(string gun) => gun.Substring(gun.LastIndexOf('/') + 1);
}

public sealed class GunSpawnerBehaviour : MonoBehaviour
{
    private const float ToastSeconds = 2.5f;

    private int _current = -1; // index of the last gun spawned, -1 before the first
    private string _toast;
    private float _toastUntil;
    private GUIStyle _style;

    public GunSpawnerBehaviour(IntPtr ptr) : base(ptr) { }

    private void Update()
    {
        if (!Input.GetKeyDown(GunSpawnerState.SpawnKey) || GunSpawnerState.Guns.Length == 0)
            return;

        bool back = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        int count = GunSpawnerState.Guns.Length;
        int index = _current < 0
            ? (back ? count - 1 : 0)
            : (_current + (back ? count - 1 : 1)) % count;
        string gun = GunSpawnerState.Guns[index];

        if (TrySpawn(gun, out string error))
        {
            _current = index;
            Show($"{GunSpawnerState.DisplayName(gun)}  ({index + 1}/{count})");
        }
        else
        {
            Show(error);
        }
    }

    internal static bool TrySpawn(string gun, out string error)
    {
        error = null;
        PTTRPlayer player = EntityManager.MainPlayer;
        Camera cam = Camera.main;
        if (player == null || cam == null || GameManager.LoadingNewLevel)
        {
            error = "Gun Spawner: load into a level first";
            return false;
        }

        Transform view = cam.transform;
        Vector3 forward = Vector3.ProjectOnPlane(view.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.01f)
            forward = player.transform.forward;
        Vector3 position = PickSpawnPoint(view, forward, player.transform);
        Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);

        try
        {
            EntityManager.CreateWeapon(GunSpawnerState.PathPrefix + gun, position, rotation, Vector3.zero);
            Plugin.Logger.LogInfo($"Spawned {gun} at {position}");
            return true;
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Spawning {gun} failed: {e}");
            error = $"Gun Spawner: couldn't spawn {GunSpawnerState.DisplayName(gun)}";
            return false;
        }
    }

    // Where the crosshair meets the world if that is close, otherwise on the floor a few metres ahead, so the gun
    // lands in view rather than at the player's feet (which a first-person camera cannot see).
    private static Vector3 PickSpawnPoint(Transform view, Vector3 flatForward, Transform player)
    {
        float reach = GunSpawnerState.SpawnDistance;
        if (TryRaycast(view.position, view.forward, reach, player, out RaycastHit aim))
            return aim.point - view.forward * 0.4f + Vector3.up * 0.3f;

        float ahead = reach;
        if (TryRaycast(view.position, flatForward, reach, player, out RaycastHit wall))
            ahead = Mathf.Max(0.5f, wall.distance - 0.4f);
        Vector3 above = view.position + flatForward * ahead;
        if (TryRaycast(above, Vector3.down, 10f, player, out RaycastHit floor))
            return floor.point + Vector3.up * 0.3f;
        return above;
    }

    private static bool TryRaycast(Vector3 origin, Vector3 dir, float max, Transform player, out RaycastHit best)
    {
        best = default;
        float bestDist = float.MaxValue;
        foreach (RaycastHit h in Physics.RaycastAll(origin, dir, max, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (h.distance >= bestDist || h.transform.IsChildOf(player.root))
                continue;
            best = h;
            bestDist = h.distance;
        }
        return bestDist < float.MaxValue;
    }

    private void Show(string text)
    {
        _toast = text;
        _toastUntil = Time.unscaledTime + ToastSeconds;
    }

    private void OnGUI()
    {
        if (_toast == null || Time.unscaledTime > _toastUntil)
            return;

        _style ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.Max(18, Screen.height / 40),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
        };

        var rect = new Rect(0, Screen.height * 0.72f, Screen.width, Screen.height / 16f);
        var shadow = new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height);
        _style.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
        GUI.Label(shadow, _toast, _style);
        _style.normal.textColor = Color.white;
        GUI.Label(rect, _toast, _style);
    }
}
