// CULTIC Replay - standalone BepInEx plugin.
//
// Records gameplay (sprite-billboard actors, projectiles, camera) into a ring
// buffer while you play, saves recordings to disk, and lets you browse saved
// replays from a native-styled "REPLAYS" entry on the main menu. Playback
// reloads the map, strips live AI, and re-renders the recording through ghost
// sprite actors at normal speed - your SuperHot dodges look superhuman.
//
// Everything rendered here is a SpriteRenderer billboard (CULTIC is a sprite
// game), so an actor's full visual state is position + facing + sprite frame.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using MenuID = scrMenuControllerV2.MenuID;

namespace CulticReplay
{
    [BepInPlugin("local.codex.culticreplay", "CULTIC Replay", "1.4.0")]
    public sealed class CulticReplayPlugin : BaseUnityPlugin
    {
        public static CulticReplayPlugin Instance;

        private ConfigEntry<float> bufferSeconds;
        private ConfigEntry<float> sampleRate;
        private ConfigEntry<bool> autoSaveOnDeath;
        private ConfigEntry<bool> recordWholeTake;
        private ConfigEntry<KeyboardShortcut> saveKey;
        private ConfigEntry<float> playSpeed;
        private ConfigEntry<bool> hideHud;
        private ConfigEntry<bool> devAutoPlay;
        private ConfigEntry<bool> disablePostFx;
        private ConfigEntry<bool> compressFrozenTime;
        private ConfigEntry<bool> playRecordedAudio;
        private ConfigEntry<bool> showWeapon;
        private Harmony audioHarmony;

        internal Recorder recorder;
        internal float BufferSecondsValue { get { return Mathf.Max(15f, bufferSeconds.Value); } }
        internal float SampleRateValue { get { return Mathf.Clamp(sampleRate.Value, 10f, 60f); } }
        internal bool AutoSaveOnDeathValue { get { return autoSaveOnDeath.Value; } }
        internal bool RecordWholeTakeValue { get { return recordWholeTake.Value; } }
        internal KeyboardShortcut SaveKeyValue { get { return saveKey.Value; } }
        internal float PlaySpeedValue { get { return Mathf.Max(0.05f, playSpeed.Value); } }
        internal bool HideHudValue { get { return hideHud.Value; } }
        internal bool DisablePostFxValue { get { return disablePostFx.Value; } }
        internal bool CompressFrozenTimeValue { get { return compressFrozenTime.Value; } }
        internal bool PlayRecordedAudioValue { get { return playRecordedAudio.Value; } }
        internal bool ShowWeaponValue { get { return showWeapon.Value; } }
        internal string SaveKeyHint { get { return saveKey.Value.ToString(); } }

        private string statusText = "";
        private float statusUntil;
        private bool loadingReplay;
        private AsyncOperation loadOp;
        private string loadingName = "";

        private void Awake()
        {
            Instance = this;
            bufferSeconds = Config.Bind("Recording", "BufferSeconds", 120f,
                new ConfigDescription("Rolling window length when RecordWholeTake is false.",
                    new AcceptableValueRange<float>(15f, 600f)));
            sampleRate = Config.Bind("Recording", "SampleRate", 30f,
                new ConfigDescription("Recording samples per second.",
                    new AcceptableValueRange<float>(10f, 60f)));
            autoSaveOnDeath = Config.Bind("Recording", "AutoSaveOnDeath", true,
                "Automatically save the current take as a replay when you die.");
            recordWholeTake = Config.Bind("Recording", "RecordWholeTake", true,
                "Record from the beginning of the current gameplay take. When false, " +
                "only the rolling BufferSeconds window is retained.");
            saveKey = Config.Bind("Hotkeys", "SaveReplay", new KeyboardShortcut(KeyCode.F9),
                "Save the current gameplay take as a replay (in-game).");
            playSpeed = Config.Bind("Playback", "Speed", 1f,
                new ConfigDescription("Replay playback speed multiplier.",
                    new AcceptableValueRange<float>(0.1f, 4f)));
            hideHud = Config.Bind("Playback", "HideHud", true,
                "Hide the in-game HUD while a replay plays (first-person camera).");
            devAutoPlay = Config.Bind("Playback", "DevAutoPlayLatest", false,
                "DEV DEBUG: automatically play the newest replay ~15 s after boot.");
            disablePostFx = Config.Bind("Playback", "DisablePostFx", true,
                "Disable retro palette/dither/post effects on the camera during playback. " +
                "Their palette data is initialized by the game's level flow, which a raw " +
                "scene load skips - leaving them on can render the view black.");
            compressFrozenTime = Config.Bind("Playback", "CompressFrozenTime", true,
                "Neutralize SuperHot pacing in playback by using recorded game time for " +
                "actors, camera, weapon and audio. Frozen stretches are removed completely.");
            playRecordedAudio = Config.Bind("Playback", "PlayRecordedAudio", true,
                "Replay the sounds captured during recording (gunshots, hits, footsteps...).");
            showWeapon = Config.Bind("Playback", "ShowWeapon", true,
                "Show the recorded first-person weapon during playback.");

            audioHarmony = new Harmony("local.codex.culticreplay.audio");
            audioHarmony.PatchAll(typeof(AudioCapturePatches));
            audioHarmony.PatchAll(typeof(ProjectileCapturePatches));

            var hostGo = new GameObject("CulticReplayHost");
            DontDestroyOnLoad(hostGo);
            recorder = hostGo.AddComponent<Recorder>();
            Recorder.Setup(this);

            Logger.LogInfo("CULTIC Replay loaded. " + saveKey.Value + " saves a replay in-game; replays are watched from the main menu.");
        }

        public void ShowStatus(string text)
        {
            statusText = "[Replay] " + text;
            statusUntil = Time.unscaledTime + 3f;
            Logger.LogInfo(text);
        }

        internal void LogWarn(string message)
        {
            Logger.LogWarning(message);
        }

        internal void Log(string message)
        {
            Logger.LogInfo(message);
        }

        private bool devAutoPlayed;

        private void TryDevAutoPlay()
        {
            if (devAutoPlayed || !devAutoPlay.Value || loadingReplay || PlaybackDirector.Playing)
            {
                return;
            }
            if (Time.unscaledTime < 15f) { return; }
            scrMenuControllerV2 mc = UnityEngine.Object.FindFirstObjectByType<scrMenuControllerV2>();
            if (mc == null) { return; }
            List<string> files = ListReplays();
            if (files.Count == 0) { return; }
            devAutoPlayed = true;
            ShowStatus("DEV autoplay: " + Path.GetFileName(files[0]));
            StartReplayLoad(files[0]);
        }

        private void Update()
        {
            TryDevAutoPlay();

            // Hook every menu controller that appears (main menu scene).
            scrMenuControllerV2 mc = UnityEngine.Object.FindFirstObjectByType<scrMenuControllerV2>();
            if (mc != null && mc.GetComponent<ReplayMenuHook>() == null)
            {
                mc.gameObject.AddComponent<ReplayMenuHook>().Setup(this);
            }

            if (!loadingReplay && !PlaybackDirector.Playing)
            {
                if (Recorder.InRealGameplay() && saveKey.Value.IsDown())
                {
                    SaveNow();
                }
            }
            else if (loadOp != null && loadOp.isDone)
            {
                loadingReplay = false;
                loadOp = null;
                PlaybackDirector.Begin(loadingName);
            }
        }

        private void OnGUI()
        {
            if (Time.unscaledTime < statusUntil && !string.IsNullOrEmpty(statusText))
            {
                GUI.Label(new Rect(18f, 44f, 700f, 30f), statusText);
            }
            if (loadingReplay)
            {
                GUI.Label(new Rect(18f, 18f, 700f, 30f),
                    "[Replay] Loading map" + (loadingName.Length > 0 ? ": " + loadingName : "") + "...");
            }
        }

        public void SaveNow()
        {
            string path = Recorder.SaveCurrentTake(bufferSeconds.Value);
            if (path != null)
            {
                ShowStatus("Replay saved: " + Path.GetFileName(path));
            }
            else
            {
                ShowStatus("Nothing recorded yet.");
            }
        }

        public static string ReplaysDir()
        {
            string dir = Path.Combine(Paths.BepInExRootPath, "replays");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return dir;
        }

        public static List<string> ListReplays()
        {
            List<string> files = new List<string>(Directory.GetFiles(ReplaysDir(), "*.cshrep"));
            files.Sort(delegate(string a, string b) { return String.Compare(b, a, StringComparison.OrdinalIgnoreCase); });
            return files;
        }

        public void StartReplayLoad(string filePath)
        {
            string scene = ReplayFile.ReadSceneName(filePath);
            if (scene == null || scene.Length == 0)
            {
                ShowStatus("Corrupt replay file.");
                return;
            }
            loadingName = Path.GetFileName(filePath);
            loadingReplay = true;
            PlaybackDirector.pendingFile = filePath;
            loadOp = SceneManager.LoadSceneAsync(scene);
            if (loadOp == null)
            {
                loadingReplay = false;
                ShowStatus("Could not load scene '" + scene + "'.");
            }
        }
    }

    // =====================================================================
    // Data model.
    // =====================================================================

    public class LayerHeader
    {
        public Vector3 lossyScale;
        public int sortOrder;
        public int sortingLayerId;
        public float r, g, b, a;
    }

    public class ActorDef
    {
        public int id;
        public string typeName;
        public bool isPlayer;
        public bool isProjectile;
        public List<LayerHeader> layers = new List<LayerHeader>();
    }

    public class LayerState
    {
        public Vector3 pos;
        public float yaw;
        public int spriteIdx; // -1 = hidden
        public bool flipX;
        public float aux; // tracer length for projectile layers, 0 otherwise
        public float pitch; // v3: vertical tracer angle in degrees
    }

    // One sound captured during recording, on the scaled-time timeline.
    public class AudioEvent
    {
        public float st;
        public string clip;
        public Vector3 pos;
        public float vol;
        public float pitch;
    }

    // A native visual-effect prefab spawned during recording. Version 4 uses
    // this for explosions so playback can run CULTIC's own particle/light
    // presentation without re-enabling its gameplay damage.
    public class EffectEvent
    {
        public float st;
        public string prefab;
        public Vector3 pos;
        public Quaternion rot;
    }

    public class ActorFrame
    {
        public int actorId;
        public List<LayerState> layers = new List<LayerState>();
    }

    public class ReplayFrame
    {
        public float t;   // wall-clock time at capture
        public float st;  // scaled (game) time at capture - the playback timeline
        public float ts;  // Time.timeScale at capture (SuperHot pacing)
        public List<ActorFrame> entries = new List<ActorFrame>();
        public Vector3 camPos;
        public float camPitch;
        public float camYaw;

        // First-person weapon UI snapshot (-1 sprite = not captured).
        public int wpSprite = -1;
        public bool wpEnabled;
        public Vector2 wpPos;
        public Vector2 wpSize;
        public Vector2 wpScale;
        public float wpRotZ;
        public Vector2 wpAnchorMin;
        public Vector2 wpAnchorMax;
        public Vector2 wpPivot;
    }

    public class ReplayData
    {
        public int formatVersion = ReplayFile.FormatVersion;
        public string sceneName = "";
        public string recordedAt = "";
        public float duration;
        public List<string> spriteTable = new List<string>();
        public List<ActorDef> actors = new List<ActorDef>();
        public List<ReplayFrame> frames = new List<ReplayFrame>();
        public List<AudioEvent> audio = new List<AudioEvent>();
        public List<EffectEvent> effects = new List<EffectEvent>();

        // Arm-canvas scaler settings so weapon UI maps onto the replay canvas.
        public Vector2 uiRefRes = Vector2.zero;
        public int uiMatchMode;
        public float uiMatch = 0.5f;

        public int InternSprite(string name)
        {
            int idx = spriteTable.IndexOf(name);
            if (idx < 0)
            {
                idx = spriteTable.Count;
                spriteTable.Add(name);
            }
            return idx;
        }
    }

    // One clock must drive every replay surface. With compression enabled, a
    // frame's scaled game timestamp is its playback timestamp; this removes
    // SuperHot's frozen wall-clock stretches without applying a second inverse
    // timeScale multiplier. With compression disabled, the original wall clock
    // is used instead. Keeping this calculation in one place prevents the old
    // actor/camera/audio drift where each surface followed a different clock.
    public static class ReplayTimeline
    {
        public static float FrameTime(ReplayFrame frame, bool compressFrozenTime)
        {
            return compressFrozenTime ? frame.st : frame.t;
        }

        public static float Interpolation(ReplayFrame a, ReplayFrame b, float cursor,
            bool compressFrozenTime)
        {
            float start = FrameTime(a, compressFrozenTime);
            float span = FrameTime(b, compressFrozenTime) - start;
            return span > 0.0001f ? Mathf.Clamp01((cursor - start) / span) : 0f;
        }
    }

    // =====================================================================
    // Binary file IO.
    // =====================================================================

    public static class ReplayFile
    {
        public const int FormatVersion = 4;

        public static void Write(string path, ReplayData d)
        {
            using (FileStream fs = new FileStream(path, FileMode.Create))
            using (BinaryWriter w = new BinaryWriter(fs))
            {
                w.Write((uint)0x31485343); // "CSH1"
                w.Write(FormatVersion);
                w.Write(d.sceneName == null ? "" : d.sceneName);
                w.Write(d.recordedAt == null ? "" : d.recordedAt);
                w.Write(d.duration);
                w.Write(d.spriteTable.Count);
                foreach (string s in d.spriteTable) { w.Write(s); }
                w.Write(d.actors.Count);
                foreach (ActorDef a in d.actors)
                {
                    w.Write(a.id);
                    w.Write(a.typeName == null ? "" : a.typeName);
                    w.Write(a.isPlayer);
                    w.Write(a.isProjectile);
                    w.Write(a.layers.Count);
                    foreach (LayerHeader h in a.layers)
                    {
                        WriteVec(w, h.lossyScale);
                        w.Write(h.sortOrder);
                        w.Write(h.sortingLayerId);
                        w.Write(h.r); w.Write(h.g); w.Write(h.b); w.Write(h.a);
                    }
                }
                w.Write(d.frames.Count);
                foreach (ReplayFrame f in d.frames)
                {
                    w.Write(f.t);
                    w.Write(f.st);
                    w.Write(f.ts);
                    WriteVec(w, f.camPos);
                    w.Write(f.camPitch);
                    w.Write(f.camYaw);
                    w.Write(f.wpSprite);
                    w.Write(f.wpEnabled);
                    w.Write(f.wpPos.x); w.Write(f.wpPos.y);
                    w.Write(f.wpSize.x); w.Write(f.wpSize.y);
                    w.Write(f.wpScale.x); w.Write(f.wpScale.y);
                    w.Write(f.wpRotZ);
                    w.Write(f.wpAnchorMin.x); w.Write(f.wpAnchorMin.y);
                    w.Write(f.wpAnchorMax.x); w.Write(f.wpAnchorMax.y);
                    w.Write(f.wpPivot.x); w.Write(f.wpPivot.y);
                    w.Write(f.entries.Count);
                    foreach (ActorFrame af in f.entries)
                    {
                        w.Write(af.actorId);
                        w.Write(af.layers.Count);
                        foreach (LayerState ls in af.layers)
                        {
                            WriteVec(w, ls.pos);
                            w.Write(ls.yaw);
                            w.Write(ls.spriteIdx);
                            w.Write(ls.flipX);
                            w.Write(ls.aux);
                            w.Write(ls.pitch);
                        }
                    }
                }
                w.Write(d.audio.Count);
                foreach (AudioEvent ev in d.audio)
                {
                    w.Write(ev.st);
                    w.Write(ev.clip == null ? "" : ev.clip);
                    WriteVec(w, ev.pos);
                    w.Write(ev.vol);
                    w.Write(ev.pitch);
                }
                w.Write(d.uiRefRes.x); w.Write(d.uiRefRes.y);
                w.Write(d.uiMatchMode);
                w.Write(d.uiMatch);
                w.Write(d.effects.Count);
                foreach (EffectEvent ev in d.effects)
                {
                    w.Write(ev.st);
                    w.Write(ev.prefab == null ? "" : ev.prefab);
                    WriteVec(w, ev.pos);
                    w.Write(ev.rot.x); w.Write(ev.rot.y); w.Write(ev.rot.z); w.Write(ev.rot.w);
                }
            }
        }

        public static string ReadSceneName(string path)
        {
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                using (BinaryReader r = new BinaryReader(fs))
                {
                    if (r.ReadUInt32() != 0x31485343) { return null; }
                    r.ReadInt32();
                    return r.ReadString();
                }
            }
            catch
            {
                return null;
            }
        }

        public static ReplayData Read(string path)
        {
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (BinaryReader r = new BinaryReader(fs))
            {
                if (r.ReadUInt32() != 0x31485343) { throw new IOException("bad magic"); }
                int version = r.ReadInt32();
                if (version < 1 || version > FormatVersion) { throw new IOException("bad version"); }
                bool v2 = version >= 2;
                bool v3 = version >= 3;
                bool v4 = version >= 4;
                ReplayData d = new ReplayData();
                d.formatVersion = version;
                d.sceneName = r.ReadString();
                d.recordedAt = r.ReadString();
                d.duration = r.ReadSingle();
                int n = r.ReadInt32();
                for (int i = 0; i < n; i++) { d.spriteTable.Add(r.ReadString()); }
                n = r.ReadInt32();
                for (int i = 0; i < n; i++)
                {
                    ActorDef a = new ActorDef();
                    a.id = r.ReadInt32();
                    a.typeName = r.ReadString();
                    a.isPlayer = r.ReadBoolean();
                    a.isProjectile = v2 && r.ReadBoolean();
                    int lc = r.ReadInt32();
                    for (int j = 0; j < lc; j++)
                    {
                        LayerHeader h = new LayerHeader();
                        h.lossyScale = ReadVec(r);
                        h.sortOrder = r.ReadInt32();
                        h.sortingLayerId = r.ReadInt32();
                        h.r = r.ReadSingle(); h.g = r.ReadSingle(); h.b = r.ReadSingle(); h.a = r.ReadSingle();
                        a.layers.Add(h);
                    }
                    d.actors.Add(a);
                }
                n = r.ReadInt32();
                float stBase = -1f;
                for (int i = 0; i < n; i++)
                {
                    ReplayFrame f = new ReplayFrame();
                    f.t = r.ReadSingle();
                    if (v2)
                    {
                        f.st = r.ReadSingle();
                        f.ts = r.ReadSingle();
                    }
                    else
                    {
                        // v1: no scaled timeline - assume unscaled pacing.
                        if (stBase < 0f) { stBase = f.t; }
                        f.st = f.t - stBase;
                        f.ts = 1f;
                    }
                    f.camPos = ReadVec(r);
                    f.camPitch = r.ReadSingle();
                    f.camYaw = r.ReadSingle();
                    if (v2)
                    {
                        f.wpSprite = r.ReadInt32();
                        f.wpEnabled = r.ReadBoolean();
                        f.wpPos = new Vector2(r.ReadSingle(), r.ReadSingle());
                        f.wpSize = new Vector2(r.ReadSingle(), r.ReadSingle());
                        f.wpScale = new Vector2(r.ReadSingle(), r.ReadSingle());
                        f.wpRotZ = r.ReadSingle();
                        f.wpAnchorMin = new Vector2(r.ReadSingle(), r.ReadSingle());
                        f.wpAnchorMax = new Vector2(r.ReadSingle(), r.ReadSingle());
                        f.wpPivot = new Vector2(r.ReadSingle(), r.ReadSingle());
                    }
                    int ec = r.ReadInt32();
                    for (int e = 0; e < ec; e++)
                    {
                        ActorFrame af = new ActorFrame();
                        af.actorId = r.ReadInt32();
                        int lc = r.ReadInt32();
                        for (int j = 0; j < lc; j++)
                        {
                            LayerState ls = new LayerState();
                            ls.pos = ReadVec(r);
                            ls.yaw = r.ReadSingle();
                            ls.spriteIdx = r.ReadInt32();
                            ls.flipX = r.ReadBoolean();
                            if (v2) { ls.aux = r.ReadSingle(); }
                            if (v3) { ls.pitch = r.ReadSingle(); }
                            af.layers.Add(ls);
                        }
                        f.entries.Add(af);
                    }
                    d.frames.Add(f);
                }
                if (v2)
                {
                    n = r.ReadInt32();
                    for (int i = 0; i < n; i++)
                    {
                        AudioEvent ev = new AudioEvent();
                        ev.st = r.ReadSingle();
                        ev.clip = r.ReadString();
                        ev.pos = ReadVec(r);
                        ev.vol = r.ReadSingle();
                        ev.pitch = r.ReadSingle();
                        d.audio.Add(ev);
                    }
                    d.uiRefRes = new Vector2(r.ReadSingle(), r.ReadSingle());
                    d.uiMatchMode = r.ReadInt32();
                    d.uiMatch = r.ReadSingle();
                    if (v4)
                    {
                        n = r.ReadInt32();
                        for (int i = 0; i < n; i++)
                        {
                            EffectEvent ev = new EffectEvent();
                            ev.st = r.ReadSingle();
                            ev.prefab = r.ReadString();
                            ev.pos = ReadVec(r);
                            ev.rot = new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                            d.effects.Add(ev);
                        }
                    }
                }
                return d;
            }
        }

        private static void WriteVec(BinaryWriter w, Vector3 v)
        {
            w.Write(v.x); w.Write(v.y); w.Write(v.z);
        }

        private static Vector3 ReadVec(BinaryReader r)
        {
            Vector3 v;
            v.x = r.ReadSingle(); v.y = r.ReadSingle(); v.z = r.ReadSingle();
            return v;
        }
    }

    // =====================================================================
    // Recorder - runs always-on inside gameplay, keeps a whole take by default
    // (or a circular window when RecordWholeTake is disabled).
    // =====================================================================

    public class TrackedActor
    {
        public int id;
        public GameObject root;
        public bool isPlayer;
        public bool isProjectile;
        public bool isDead;
        public Vector3 lastSeenPos;
        public LineRenderer tracerSource;
        public List<SpriteRenderer> layers = new List<SpriteRenderer>();
        public List<LayerHeader> headers = new List<LayerHeader>();
    }

    public class Recorder : MonoBehaviour
    {
        private static CulticReplayPlugin plugin;
        private static bool playerWasDead;

        private readonly List<TrackedActor> tracked = new List<TrackedActor>();
        private readonly List<ReplayFrame> buffer = new List<ReplayFrame>();
        private readonly Dictionary<string, int> spriteIntern = new Dictionary<string, int>();
        // Persistent per-take registry of actor definitions. Enemies are often
        // destroyed long before the buffer is saved (death auto-save!), so defs
        // MUST NOT be collected from the still-alive set at save time.
        private readonly Dictionary<int, ActorDef> defRegistry = new Dictionary<int, ActorDef>();
        private readonly List<AudioEvent> audioEvents = new List<AudioEvent>();
        private readonly List<EffectEvent> effectEvents = new List<EffectEvent>();

        private int nextActorId = 1;
        private float sampleAccum;
        private float scanAccum;
        private float projectileScanAccum;
        private long tickCounter;
        private float scaledTime; // cumulative game (scaled) time of this take
        private scrPlayerControl lastKnownPlayer;
        private float lastGameplayTime = -999f;
        private bool wasInGameplay;

        // Weapon UI capture cache (refreshed when the player object changes).
        private scrPlayerControl uiCachedPlayer;
        private RectTransform wpRect;
        private UnityEngine.UI.Image wpImage;
        private UnityEngine.UI.CanvasScaler armScaler;
        private Canvas armCanvas;
        private readonly Vector3[] weaponWorldCorners = new Vector3[4];

        public static void Setup(CulticReplayPlugin p)
        {
            plugin = p;
        }

        private void Start()
        {
        }

        public static bool InRealGameplay()
        {
            scrGameControl game = scrGameControl.Instance;
            if (game == null || game.gameState != 0) { return false; }
            if (game.connectionStatus != scrGameControl.ConnectionStatus.Offline) { return false; }
            return true;
        }

        // ---- Audio capture (called from the Harmony patches) ----------------

        public static bool CanRecordAudio()
        {
            if (plugin == null || PlaybackDirector.Playing) { return false; }
            Recorder self = plugin.recorder;
            if (self == null) { return false; }
            return InRealGameplay();
        }

        public static void RecordAudio(string clipName, Vector3 pos, float vol, float pitch)
        {
            if (string.IsNullOrEmpty(clipName) || vol <= 0.001f) { return; }
            Recorder self = plugin.recorder;
            if (self == null) { return; }
            AudioEvent ev = new AudioEvent();
            ev.st = self.scaledTime;
            ev.clip = clipName;
            ev.pos = pos;
            ev.vol = vol;
            ev.pitch = pitch;
            self.audioEvents.Add(ev);
        }

        // Spawn patches call this from each known projectile's Start method.
        // Registration happens immediately, before a short-lived bullet can be
        // destroyed between two 30 Hz samples. The periodic scan below remains
        // only as a low-frequency safety net for already-live scene objects.
        public static void RegisterProjectile(MonoBehaviour projectile, bool forceTracer)
        {
            if (plugin == null || projectile == null || projectile.gameObject == null) { return; }
            if (PlaybackDirector.Playing || !InRealGameplay()) { return; }
            Recorder self = plugin.recorder;
            if (self == null || self.FindTracked(projectile.gameObject) != null) { return; }
            self.TrackProjectile(projectile.gameObject, forceTracer);
        }

        // Persistent sprite props (barrels, crates, breakable scenery) need the
        // same frame-by-frame treatment as actors. Only register ones the replay
        // renderer can reproduce; mesh-only props remain part of the live map.
        public static void RegisterVisualActor(MonoBehaviour visual)
        {
            if (plugin == null || visual == null || visual.gameObject == null) { return; }
            if (PlaybackDirector.Playing || !InRealGameplay()) { return; }
            Recorder self = plugin.recorder;
            if (self == null || self.FindTracked(visual.gameObject) != null) { return; }
            if (visual.gameObject.GetComponentsInChildren<SpriteRenderer>(true).Length == 0) { return; }
            self.Track(visual.gameObject, false);
        }

        // scrExplosion instances are native CULTIC VFX prefabs. Record their
        // spawn, then playback can instantiate the same resident prefab with
        // damage/ignition disabled.
        public static void RecordEffect(scrExplosion effect)
        {
            if (plugin == null || effect == null || effect.gameObject == null) { return; }
            if (PlaybackDirector.Playing || !InRealGameplay()) { return; }
            Recorder self = plugin.recorder;
            if (self == null) { return; }

            EffectEvent ev = new EffectEvent();
            ev.st = self.scaledTime;
            ev.prefab = CleanCloneName(effect.gameObject.name);
            ev.pos = effect.transform.position;
            ev.rot = effect.transform.rotation;
            self.effectEvents.Add(ev);
        }

        private static string CleanCloneName(string name)
        {
            if (string.IsNullOrEmpty(name)) { return ""; }
            const string clone = "(Clone)";
            return name.EndsWith(clone, StringComparison.OrdinalIgnoreCase)
                ? name.Substring(0, name.Length - clone.Length).TrimEnd()
                : name;
        }

        private float BufferSeconds()
        {
            return Mathf.Max(15f, plugin.BufferSecondsValue);
        }

        private float SampleHz()
        {
            return plugin.SampleRateValue;
        }

        private void Update()
        {
            // Never record a replay playback as if it were gameplay (a freshly
            // loaded map looks exactly like real gameplay to InRealGameplay).
            if (PlaybackDirector.Playing) { return; }

            bool inGameplay = InRealGameplay();
            if (inGameplay)
            {
                lastGameplayTime = Time.unscaledTime;
                // Capture the opening frame immediately instead of waiting for
                // the periodic actor scan. This matters most at level/arena
                // start, where the previous implementation trimmed the blank
                // lead-in and appeared to begin late.
                if (!wasInGameplay)
                {
                    ScanActors();
                    scanAccum = 0f;
                    projectileScanAccum = 1f;
                }
            }

            // Death auto-save must be checked for a short window AFTER gameplay
            // ends as well: the game flips gameState away from 0 the moment you
            // die, so the death frame itself may already count as non-gameplay.
            if (Time.unscaledTime - lastGameplayTime <= 2f)
            {
                CheckAutoSave();
            }
            else
            {
                // Long out of gameplay: reset the take completely so a later
                // save never mixes scenes, timelines or audio.
                playerWasDead = false;
                lastKnownPlayer = null;
                uiCachedPlayer = null;
                wpRect = null;
                wpImage = null;
                armScaler = null;
                armCanvas = null;
                if (buffer.Count > 0 || audioEvents.Count > 0 || effectEvents.Count > 0 ||
                    tracked.Count > 0 || defRegistry.Count > 0 || spriteIntern.Count > 0)
                {
                    buffer.Clear();
                    audioEvents.Clear();
                    effectEvents.Clear();
                    spriteIntern.Clear();
                    defRegistry.Clear();
                    tracked.Clear();
                    nextActorId = 1;
                    scaledTime = 0f;
                    sampleAccum = 0f;
                    scanAccum = 0f;
                    projectileScanAccum = 0f;
                    tickCounter = 0;
                }
            }

            wasInGameplay = inGameplay;

            if (!inGameplay)
            {
                return;
            }

            scanAccum += Time.unscaledDeltaTime;
            if (scanAccum >= 0.5f)
            {
                scanAccum = 0f;
                ScanActors();
            }

            sampleAccum += Time.unscaledDeltaTime;
            float step = 1f / SampleHz();
            while (sampleAccum >= step)
            {
                sampleAccum -= step;
                Capture(step);
            }
        }

        private void CheckAutoSave()
        {
            scrPlayerControl player = lastKnownPlayer;
            if (player == null) { return; }
            if (player.isDead && !playerWasDead && plugin.AutoSaveOnDeathValue)
            {
                playerWasDead = true;
                string path = SaveCurrentTake(BufferSeconds());
                if (path != null)
                {
                    plugin.ShowStatus("Death replay saved: " + Path.GetFileName(path));
                }
            }
            if (!player.isDead)
            {
                playerWasDead = false;
            }
        }

        private void ScanActors()
        {
            scrGameControl game = scrGameControl.Instance;

            // Enemies.
            scrEnemy[] enemies = UnityEngine.Object.FindObjectsByType<scrEnemy>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (scrEnemy e in enemies)
            {
                if (e == null || e.gameObject == null) { continue; }
                if (FindTracked(e.gameObject) != null) { continue; }
                Track(e.gameObject, false);
            }

            // Sprite-based barrels, crates and other breakable scenery. Their
            // renderer and active state are sampled so intact/broken/removed
            // states replay instead of leaving the freshly loaded prop behind.
            scrDestructible[] destructibles = UnityEngine.Object.FindObjectsByType<scrDestructible>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (scrDestructible d in destructibles)
            {
                if (d == null || d.gameObject == null) { continue; }
                if (FindTracked(d.gameObject) != null) { continue; }
                if (d.gameObject.GetComponentsInChildren<SpriteRenderer>(true).Length == 0) { continue; }
                Track(d.gameObject, false);
            }

            // Local player.
            scrPlayerControl player = FindLocalPlayer(game);
            if (player != null)
            {
                lastKnownPlayer = player;
                if (FindTracked(player.gameObject) == null)
                {
                    Track(player.gameObject, true);
                }
            }

            // Drop destroyed actors.
            for (int i = tracked.Count - 1; i >= 0; i--)
            {
                if (tracked[i].root == null) { tracked.RemoveAt(i); }
            }
        }

        private TrackedActor FindTracked(GameObject go)
        {
            for (int i = 0; i < tracked.Count; i++)
            {
                if (tracked[i].root == go) { return tracked[i]; }
            }
            return null;
        }

        private void Track(GameObject root, bool isPlayer)
        {
            TrackEx(root, isPlayer, false);
        }

        private void TrackEx(GameObject root, bool isPlayer, bool isProjectile)
        {
            TrackedActor ta = new TrackedActor();
            ta.id = nextActorId++;
            ta.root = root;
            ta.isPlayer = isPlayer;
            ta.isProjectile = isProjectile;
            ta.lastSeenPos = root.transform.position;
            SpriteRenderersOf(root, ta.layers);
            foreach (SpriteRenderer sr in ta.layers)
            {
                LayerHeader h = new LayerHeader();
                h.lossyScale = sr.transform.lossyScale;
                h.sortOrder = sr.sortingOrder;
                h.sortingLayerId = sr.sortingLayerID;
                h.r = sr.color.r; h.g = sr.color.g; h.b = sr.color.b; h.a = sr.color.a;
                ta.headers.Add(h);
            }
            ActorDef def = new ActorDef();
            def.id = ta.id;
            def.typeName = root.name;
            def.isPlayer = isPlayer;
            def.isProjectile = isProjectile;
            def.layers.AddRange(ta.headers);
            defRegistry[ta.id] = def;
            tracked.Add(ta);
        }

        // Projectiles live for fractions of a second - the normal actor scan
        // would miss them. Start patches register new instances immediately;
        // this one-second scan is only a fallback for already-live objects.
        // Sprite projectiles (axes, TNT, Molotovs, grenades...) are captured as
        // their real animated sprites. Sprite-less projectiles fall back to a
        // tracer so they remain visible instead of producing a layerless ghost.
        private void ScanProjectiles(float delta)
        {
            projectileScanAccum += delta;
            if (projectileScanAccum < 1f) { return; }
            projectileScanAccum = 0f;

            ScanProjectileType<scrBullet>(true);
            ScanProjectileType<scrBulletTracer>(true);

            // Sprite-based thrown items and shells (player and enemy owned).
            ScanProjectileType<scrThrownAxe>(false);
            ScanProjectileType<scrTNT>(false);
            ScanProjectileType<scrMolotov>(false);
            ScanProjectileType<scrGasGrenade>(false);
            ScanProjectileType<scrMolomite>(false);
            ScanProjectileType<scrNailJar>(false);
            ScanProjectileType<scrGrenadeShell>(false);
            ScanProjectileType<scrImpactProjectile>(false);
            ScanProjectileType<scrInfectedSpit>(false);
            ScanProjectileType<scrDroppedShield>(false);

            // These may use meshes, trails or particles instead of sprites;
            // TrackProjectile chooses a tracer only when no sprite exists.
            ScanProjectileType<scrHeliRocket>(false);
            ScanProjectileType<scrArchonEnergyWave>(false);
            ScanProjectileType<scrArchonBloodBall>(false);
            ScanProjectileType<scrVomitParticle>(false);
            ScanProjectileType<scrIncineratorParticle>(false);
            ScanProjectileType<scrFireParticle>(false);
            ScanProjectileType<scrFlare>(false);
            ScanProjectileType<scrNail>(false);
            ScanProjectileType<scrThrownPitchfork>(false);

            // Flying limbs, heads, organs and other sprite gore use scrGib.
            ScanProjectileType<scrGib>(false);
        }

        private void ScanProjectileType<T>(bool forceTracer) where T : MonoBehaviour
        {
            T[] found = UnityEngine.Object.FindObjectsByType<T>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (T p in found)
            {
                if (p == null || p.gameObject == null) { continue; }
                if (FindTracked(p.gameObject) != null) { continue; }
                TrackProjectile(p.gameObject, forceTracer);
            }
        }

        private void TrackProjectile(GameObject root, bool forceTracer)
        {
            SpriteRenderer[] sprites = root.GetComponentsInChildren<SpriteRenderer>(true);
            bool useTracer = forceTracer || sprites == null || sprites.Length == 0;
            TrackEx(root, false, useTracer);
            if (useTracer)
            {
                TrackedActor trackedProjectile = FindTracked(root);
                if (trackedProjectile != null)
                {
                    trackedProjectile.tracerSource = root.GetComponentInChildren<LineRenderer>(true);
                }
            }
        }

        private static void SpriteRenderersOf(GameObject root, List<SpriteRenderer> into)
        {
            into.Clear();
            SpriteRenderer[] all = root.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (SpriteRenderer sr in all)
            {
                if (sr != null) { into.Add(sr); }
            }
        }

        private static scrPlayerControl FindLocalPlayer(scrGameControl game)
        {
            if (game != null && game.localPlayerID >= 0)
            {
                try
                {
                    GameObject obj = game.getPlayerObject(game.localPlayerID);
                    if (obj != null)
                    {
                        scrPlayerControl p = obj.GetComponent<scrPlayerControl>();
                        if (p != null) { return p; }
                    }
                }
                catch (Exception)
                {
                }
            }
            return UnityEngine.Object.FindFirstObjectByType<scrPlayerControl>();
        }

        private void Capture(float step)
        {
            Camera cam = Camera.main;
            ReplayFrame f = new ReplayFrame();
            f.t = Time.unscaledTime;
            scaledTime += step * Time.timeScale;
            f.st = scaledTime;
            f.ts = Time.timeScale;

            tickCounter++;
            ScanProjectiles(step);

            if (cam != null)
            {
                f.camPos = cam.transform.position;
                Vector3 euler = cam.transform.eulerAngles;
                f.camPitch = euler.x;
                f.camYaw = euler.y;
            }

            for (int i = 0; i < tracked.Count; i++)
            {
                TrackedActor ta = tracked[i];
                if (ta.root == null) { continue; }

                scrEnemy enemy = ta.root.GetComponent<scrEnemy>();
                if (enemy != null && enemy.isDead) { ta.isDead = true; }

                ActorFrame af = new ActorFrame();
                af.actorId = ta.id;

                if (ta.isProjectile)
                {
                    CaptureProjectile(ta, af);
                    f.entries.Add(af);
                    continue;
                }

                for (int j = 0; j < ta.layers.Count; j++)
                {
                    SpriteRenderer sr = ta.layers[j];
                    LayerState ls = new LayerState();
                    if (sr == null || !sr.enabled || !sr.gameObject.activeInHierarchy || sr.sprite == null)
                    {
                        ls.spriteIdx = -1;
                        ls.pos = Vector3.zero;
                        ls.yaw = 0f;
                        ls.flipX = false;
                    }
                    else
                    {
                        ls.pos = sr.transform.position;
                        ls.yaw = sr.transform.eulerAngles.y;
                        ls.spriteIdx = Intern(sr.sprite.name);
                        ls.flipX = sr.flipX;
                    }
                    af.layers.Add(ls);
                }
                f.entries.Add(af);
            }

            CaptureWeaponUi(f);

            buffer.Add(f);
            if (!plugin.RecordWholeTakeValue)
            {
                float cutoff = Time.unscaledTime - BufferSeconds();
                while (buffer.Count > 0 && buffer[0].t < cutoff)
                {
                    buffer.RemoveAt(0);
                }
            }
        }

        // scrBullet is a LineRenderer tracer, not a sprite: record the segment
        // it swept since our last sample as pos + yaw + length. Other projectile
        // types fall back to a synthetic dot if they have no sprites.
        private void CaptureProjectile(TrackedActor ta, ActorFrame af)
        {
            LayerState ls = new LayerState();
            Vector3 head = ta.root.transform.position;
            Vector3 seg = head - ta.lastSeenPos;
            Vector3 segmentStart = head - seg;
            Vector3 segmentEnd = head;

            // Use the game's actual LineRenderer geometry when available. Both
            // prefabTracer and prefabEnemyBullet are native line renderers, and
            // their local endpoints/lengths differ from a motion-sample streak.
            LineRenderer source = ta.tracerSource;
            if (source != null && source.enabled && source.positionCount >= 2)
            {
                segmentStart = source.GetPosition(0);
                segmentEnd = source.GetPosition(source.positionCount - 1);
                if (!source.useWorldSpace)
                {
                    segmentStart = source.transform.TransformPoint(segmentStart);
                    segmentEnd = source.transform.TransformPoint(segmentEnd);
                }
                seg = segmentEnd - segmentStart;
                head = segmentEnd;
            }
            float len = seg.magnitude;
            if (len < 0.05f)
            {
                // Barely moved this tick (or just spawned) - extrapolate a
                // short segment along its facing so it stays a visible streak.
                Vector3 fwd = ta.root.transform.forward;
                if (fwd.sqrMagnitude < 0.0001f) { fwd = Vector3.forward; }
                seg = fwd;
                len = 0.6f;
                segmentStart = head - seg * 0.5f;
                segmentEnd = head + seg * 0.5f;
            }
            ta.lastSeenPos = head;
            ls.pos = (segmentStart + segmentEnd) * 0.5f;
            ls.yaw = Mathf.Atan2(seg.x, seg.z) * Mathf.Rad2Deg;
            float horizontal = Mathf.Sqrt(seg.x * seg.x + seg.z * seg.z);
            ls.pitch = Mathf.Atan2(seg.y, horizontal) * Mathf.Rad2Deg;
            ls.spriteIdx = Intern("___tracer___");
            ls.aux = len;
            af.layers.Add(ls);
        }

        private void CaptureWeaponUi(ReplayFrame f)
        {
            scrPlayerControl player = lastKnownPlayer;
            if (player == null || player.gameObject == null)
            {
                f.wpSprite = -1;
                return;
            }

            if (uiCachedPlayer != player)
            {
                uiCachedPlayer = player;
                wpRect = player.playerWeapon;
                wpImage = wpRect != null ? wpRect.GetComponent<UnityEngine.UI.Image>() : null;
                armCanvas = player.armCanvas != null ? player.armCanvas.GetComponent<Canvas>() : null;
                armScaler = armCanvas != null ? armCanvas.GetComponent<UnityEngine.UI.CanvasScaler>() : null;
            }

            if (wpRect == null || wpImage == null)
            {
                f.wpSprite = -1;
                return;
            }

            Sprite s = wpImage.sprite;
            if (s == null || !wpRect.gameObject.activeInHierarchy)
            {
                f.wpSprite = -1;
                return;
            }

            f.wpSprite = Intern(s.name);
            f.wpEnabled = wpImage.enabled;

            // v3 stores the weapon's final screen-space quad, not its local
            // RectTransform under the game's nested arm hierarchy. Re-parenting
            // the old local values directly under ReplayWeaponCanvas was the
            // source of the visibly too-low/patchwork hand placement.
            wpRect.GetWorldCorners(weaponWorldCorners);
            Camera uiCamera = armCanvas != null && armCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? armCanvas.worldCamera : null;
            Vector2 bl = RectTransformUtility.WorldToScreenPoint(uiCamera, weaponWorldCorners[0]);
            Vector2 tl = RectTransformUtility.WorldToScreenPoint(uiCamera, weaponWorldCorners[1]);
            Vector2 tr = RectTransformUtility.WorldToScreenPoint(uiCamera, weaponWorldCorners[2]);
            Vector2 br = RectTransformUtility.WorldToScreenPoint(uiCamera, weaponWorldCorners[3]);
            float screenW = Mathf.Max(1f, Screen.width);
            float screenH = Mathf.Max(1f, Screen.height);
            Vector2 center = (bl + tl + tr + br) * 0.25f;
            Vector2 axisX = tr - tl;
            Vector2 axisY = tl - bl;
            float handedness = axisX.x * axisY.y - axisX.y * axisY.x;
            f.wpPos = new Vector2(center.x / screenW, center.y / screenH);
            f.wpSize = new Vector2(axisX.magnitude / screenW, axisY.magnitude / screenH);
            f.wpScale = new Vector2(1f, handedness < 0f ? -1f : 1f);
            f.wpRotZ = Mathf.Atan2(axisX.y, axisX.x) * Mathf.Rad2Deg;
            f.wpAnchorMin = f.wpPos;
            f.wpAnchorMax = f.wpPos;
            f.wpPivot = new Vector2(0.5f, 0.5f);
        }

        private int Intern(string spriteName)
        {
            int idx;
            if (!spriteIntern.TryGetValue(spriteName, out idx))
            {
                idx = spriteIntern.Count;
                spriteIntern[spriteName] = idx;
            }
            return idx;
        }

        public static string SaveCurrentTake(float seconds)
        {
            if (plugin == null) { return null; }
            Recorder self = plugin.recorder;
            if (self == null || self.buffer.Count < 2) { return null; }

            int first = 0;
            if (!plugin.RecordWholeTakeValue)
            {
                float cutoff = Time.unscaledTime - seconds;
                while (first < self.buffer.Count && self.buffer[first].t < cutoff) { first++; }
            }
            // Skip leading frames before anything was tracked so the replay
            // does not start on empty seconds.
            while (first < self.buffer.Count - 2 && self.buffer[first].entries.Count == 0)
            {
                first++;
            }
            if (self.buffer.Count - first < 2) { return null; }

            ReplayData data = new ReplayData();
            data.sceneName = SceneManager.GetActiveScene().name;
            data.recordedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            data.duration = self.buffer[self.buffer.Count - 1].t - self.buffer[first].t;

            // Collect actor defs for every actor that appears anywhere in the
            // slice (first-appearance order), from the persistent registry.
            // Collecting only still-alive actors lost every enemy that died
            // before the save - the "empty stage" bug.
            Dictionary<int, ActorDef> defs = new Dictionary<int, ActorDef>();
            List<int> defOrder = new List<int>();
            HashSet<int> seen = new HashSet<int>();
            for (int i = first; i < self.buffer.Count; i++)
            {
                foreach (ActorFrame af in self.buffer[i].entries)
                {
                    if (seen.Add(af.actorId)) { defOrder.Add(af.actorId); }
                }
            }
            foreach (int id in defOrder)
            {
                ActorDef def;
                if (!self.defRegistry.TryGetValue(id, out def)) { continue; }
                ActorDef copy = new ActorDef();
                copy.id = def.id;
                copy.typeName = def.typeName;
                copy.isPlayer = def.isPlayer;
                copy.isProjectile = def.isProjectile;
                copy.layers.AddRange(def.layers);
                defs[id] = copy;
                data.actors.Add(copy);
            }

            // CRITICAL: spriteIntern assigns indices by INSERTION order, but
            // Dictionary enumeration returns arbitrary (bucket) order. Place by
            // index value so table position == stored index, never foreach-add.
            string[] orderedSprites = new string[self.spriteIntern.Count];
            foreach (KeyValuePair<string, int> kv in self.spriteIntern)
            {
                orderedSprites[kv.Value] = kv.Key;
            }
            for (int i = 0; i < orderedSprites.Length; i++)
            {
                data.spriteTable.Add(orderedSprites[i]);
            }

            for (int i = first; i < self.buffer.Count; i++)
            {
                data.frames.Add(self.buffer[i]);
            }

            // Audio events on the scaled timeline inside the saved window.
            float stLo = self.buffer[first].st - 0.05f;
            float stHi = self.buffer[self.buffer.Count - 1].st;
            foreach (AudioEvent ev in self.audioEvents)
            {
                if (ev.st >= stLo && ev.st <= stHi) { data.audio.Add(ev); }
            }
            if (data.audio.Count == 0 && self.audioEvents.Count > 0)
            {
                // Take reset edge case: keep whatever exists rather than nothing.
                data.audio.AddRange(self.audioEvents);
            }

            // Native VFX events use the same scaled timeline and saved-window
            // bounds as audio, so explosions stay synchronized in both whole-
            // take and rolling-buffer recordings.
            foreach (EffectEvent ev in self.effectEvents)
            {
                if (ev.st >= stLo && ev.st <= stHi) { data.effects.Add(ev); }
            }

            if (self.armScaler != null)
            {
                data.uiRefRes = self.armScaler.referenceResolution;
                data.uiMatchMode = (int)self.armScaler.screenMatchMode;
                data.uiMatch = self.armScaler.matchWidthOrHeight;
            }

            string stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
            string safeScene = data.sceneName.Replace("/", "-");
            string path = Path.Combine(CulticReplayPlugin.ReplaysDir(), safeScene + "_" + stamp + ".cshrep");
            ReplayFile.Write(path, data);
            return path;
        }
    }

    // =====================================================================
    // Playback director - takes over a freshly loaded map and re-renders
    // the recording through ghost sprite actors.
    // =====================================================================

    public class GhostLayer
    {
        public GameObject go;
        public SpriteRenderer sr;
        public LineRenderer tracer;
        public LayerHeader header;
    }

    public class GhostActor
    {
        public int id;
        public List<GhostLayer> layers = new List<GhostLayer>();
    }

    public class PlaybackDirector : MonoBehaviour
    {
        public static string pendingFile;

        /// <summary>True while a replay is on screen. Other mods and the
        /// recorder itself check this so playback is not mistaken for gameplay.</summary>
        public static bool Playing;

        private ReplayData data;
        private readonly List<GhostActor> ghosts = new List<GhostActor>();
        private Dictionary<string, Sprite> spriteLookup;
        private float playT;
        private int frameIdx;
        private bool finishedNoticeShown;
        private Camera replayCam;
        private CulticReplayPlugin plugin;
        private int effectIdx;
        private Dictionary<string, GameObject> effectLookup;
        private readonly List<GameObject> spawnedEffects = new List<GameObject>();

        public static void Begin(string fileName)
        {
            Playing = true;
            prevTimeScaleStatic = Time.timeScale;
            if (prevTimeScaleStatic <= 0f) { prevTimeScaleStatic = 1f; }
            SetSuperHotStandDown(true);
            GameObject go = new GameObject("CulticReplayDirector");
            DontDestroyOnLoad(go);
            PlaybackDirector d = go.AddComponent<PlaybackDirector>();
            d.plugin = CulticReplayPlugin.Instance;
            d.StartCoroutine(d.Run(fileName));
        }

        private static float prevTimeScaleStatic = 1f;

        private void OnDestroy()
        {
            if (replayCam != null)
            {
                Destroy(replayCam.gameObject);
                replayCam = null;
            }
            if (weaponCanvas != null)
            {
                Destroy(weaponCanvas.gameObject);
                weaponCanvas = null;
                weaponImage = null;
            }
            foreach (GameObject effect in spawnedEffects)
            {
                if (effect != null) { Destroy(effect); }
            }
            spawnedEffects.Clear();
            if (Playing)
            {
                Playing = false;
                SetSuperHotStandDown(false);
                Time.timeScale = prevTimeScaleStatic;
            }
        }

        private IEnumerator Run(string fileName)
        {
            yield return null; // let the scene finish initializing

            try
            {
                data = ReplayFile.Read(Path.Combine(CulticReplayPlugin.ReplaysDir(), fileName));
            }
            catch (Exception ex)
            {
                plugin.ShowStatus("Failed to read replay: " + ex.Message);
                Destroy(gameObject);
                yield break;
            }

            NeutralizeScene();
            DisableOverlayCameras();
            SpawnReplayCamera();
            if (plugin.DisablePostFxValue)
            {
                DisableCameraPostFx();
            }

            if (plugin.HideHudValue)
            {
                HideHud();
            }

            // Sprites from the loaded map should now be resident.
            spriteLookup = new Dictionary<string, Sprite>();
            Sprite[] all = Resources.FindObjectsOfTypeAll<Sprite>();
            foreach (Sprite s in all)
            {
                if (s != null && !spriteLookup.ContainsKey(s.name))
                {
                    spriteLookup[s.name] = s;
                }
            }

            // Synthetic sprites for projectile ghosts that have no real sprite.
            if (data.spriteTable.Contains("___dot___") && !spriteLookup.ContainsKey("___dot___"))
            {
                spriteLookup["___dot___"] = MakeFallbackSprite(new Color(1f, 0.85f, 0.4f), 64f);
            }

            BuildGhosts();

            // Audio: resolve recorded clip names to resident clips and order
            // the events along the scaled timeline.
            clipLookup = new Dictionary<string, AudioClip>();
            AudioClip[] clips = Resources.FindObjectsOfTypeAll<AudioClip>();
            foreach (AudioClip c in clips)
            {
                if (c != null && !clipLookup.ContainsKey(c.name))
                {
                    clipLookup[c.name] = c;
                }
            }
            data.audio.Sort(delegate(AudioEvent x, AudioEvent y) { return x.st.CompareTo(y.st); });
            audioIdx = 0;
            int missingClips = 0;
            foreach (AudioEvent ev in data.audio)
            {
                if (!clipLookup.ContainsKey(ev.clip)) { missingClips++; }
            }

            // Resolve resident native effect prefabs referenced by the loaded
            // scene. Prefer prefab assets (invalid Scene) over scene instances
            // that happen to share the same name.
            effectLookup = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
            GameObject[] gameObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (GameObject candidate in gameObjects)
            {
                if (candidate == null) { continue; }
                string key = CleanEffectName(candidate.name);
                GameObject existing;
                if (!effectLookup.TryGetValue(key, out existing) ||
                    (existing != null && existing.scene.IsValid() && !candidate.scene.IsValid()))
                {
                    effectLookup[key] = candidate;
                }
            }
            data.effects.Sort(delegate(EffectEvent x, EffectEvent y) { return x.st.CompareTo(y.st); });
            effectIdx = 0;
            int missingEffects = 0;
            foreach (EffectEvent ev in data.effects)
            {
                if (!effectLookup.ContainsKey(ev.prefab)) { missingEffects++; }
            }

            if (plugin.ShowWeaponValue)
            {
                CreateWeaponCanvas();
            }

            LogDiagnostics();
            plugin.Log(string.Format(
                "Playback: {0} audio events ({1} clips unresolved), unified {2} timeline.",
                data.audio.Count, missingClips,
                plugin.CompressFrozenTimeValue ? "scaled-game-time" : "wall-clock"));
            plugin.Log(string.Format(
                "Playback: {0} native explosion event(s) ({1} prefabs unresolved).",
                data.effects.Count, missingEffects));

            playT = data.frames.Count > 0 ? data.frames[0].t : 0f;
            playST = data.frames.Count > 0 ? data.frames[0].st : 0f;
            frameIdx = 0;
            finishedNoticeShown = false;
            plugin.ShowStatus("Playing " + fileName + " (" +
                (data.frames.Count > 0 ? (data.frames[data.frames.Count - 1].st - data.frames[0].st).ToString("F0") : "?") +
                "s game time, " + data.sceneName + ")");
        }

        private float playST;
        private int audioIdx;
        private Dictionary<string, AudioClip> clipLookup;
        private Canvas weaponCanvas;
        private UnityEngine.UI.Image weaponImage;

        private static Sprite MakeFallbackSprite(Color color, float pixelsPerUnit)
        {
            Texture2D tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            Color[] px = new Color[64];
            for (int i = 0; i < px.Length; i++) { px[i] = color; }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, 8f, 8f), new Vector2(0.5f, 0.5f), pixelsPerUnit);
        }

        private void CreateWeaponCanvas()
        {
            try
            {
                GameObject cgo = new GameObject("ReplayWeaponCanvas");
                DontDestroyOnLoad(cgo);
                weaponCanvas = cgo.AddComponent<Canvas>();
                weaponCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                weaponCanvas.sortingOrder = 500;
                // v3 weapon snapshots are already normalized final screen-space
                // quads. Older recordings still need their original CanvasScaler
                // because their fields are local to the game's arm hierarchy.
                if (data.formatVersion < 3 && data.uiRefRes.x > 1f && data.uiRefRes.y > 1f)
                {
                    UnityEngine.UI.CanvasScaler scaler = cgo.AddComponent<UnityEngine.UI.CanvasScaler>();
                    scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = data.uiRefRes;
                    scaler.screenMatchMode = (UnityEngine.UI.CanvasScaler.ScreenMatchMode)data.uiMatchMode;
                    scaler.matchWidthOrHeight = data.uiMatch;
                }
                GameObject igo = new GameObject("ReplayWeapon");
                igo.transform.SetParent(cgo.transform, false);
                weaponImage = igo.AddComponent<UnityEngine.UI.Image>();
                weaponImage.raycastTarget = false;
                weaponImage.enabled = false;
                plugin.Log("Playback: weapon canvas created (" +
                    (data.formatVersion >= 3 ? "screen-space v3" :
                    (data.uiRefRes.x > 1f ? "legacy refRes " + data.uiRefRes.ToString("F0") : "legacy no scaler meta")) + ")");
            }
            catch (Exception ex)
            {
                plugin.LogWarn("CreateWeaponCanvas failed: " + ex.Message);
            }
        }

        private static void NeutralizeScene()
        {
            // Kill AI brains and spawners so the map stands still.
            scrEnemy[] enemies = UnityEngine.Object.FindObjectsByType<scrEnemy>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (scrEnemy e in enemies)
            {
                if (e == null) { continue; }
                e.enabled = false;
                // A disabled MonoBehaviour still renders. Leaving these fresh-
                // scene sprites visible made a recorded death reveal an intact,
                // motionless enemy underneath the ghost.
                Renderer[] renderers = e.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer renderer in renderers) { renderer.enabled = false; }
            }

            // The recorder owns sprite destructibles during replay. Hide the
            // freshly loaded intact copies so recorded barrel/crate state can
            // replace them without an unbreakable duplicate underneath.
            scrDestructible[] destructibles = UnityEngine.Object.FindObjectsByType<scrDestructible>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (scrDestructible destructible in destructibles)
            {
                if (destructible == null) { continue; }
                SpriteRenderer[] renderers = destructible.GetComponentsInChildren<SpriteRenderer>(true);
                if (renderers.Length == 0) { continue; }
                destructible.enabled = false;
                foreach (SpriteRenderer renderer in renderers) { renderer.enabled = false; }
            }

            scrSpawnTrigger[] triggers = UnityEngine.Object.FindObjectsByType<scrSpawnTrigger>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (scrSpawnTrigger t in triggers) { t.enabled = false; }

            scrPGEnemySpawner[] spawners = UnityEngine.Object.FindObjectsByType<scrPGEnemySpawner>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (scrPGEnemySpawner s in spawners) { s.enabled = false; }

            MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (MonoBehaviour b in behaviours)
            {
                if (b == null) { continue; }
                string tn = b.GetType().Name;
                if (tn.EndsWith("WaveManager") || tn == "scrSurvivalController")
                {
                    b.enabled = false;
                }
            }

            // Hide any player the scene spawned; we render his ghost instead.
            scrPlayerControl player = UnityEngine.Object.FindFirstObjectByType<scrPlayerControl>();
            if (player != null)
            {
                player.enabled = false;
                Renderer[] rends = player.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer r in rends) { r.enabled = false; }
                Canvas[] canvases = player.GetComponentsInChildren<Canvas>(true);
                foreach (Canvas c in canvases) { c.enabled = false; }
            }
        }

        private void BuildGhosts()
        {
            Material nativeBulletMaterial = null;
            Material[] materials = Resources.FindObjectsOfTypeAll<Material>();
            foreach (Material material in materials)
            {
                if (material != null && material.name == "matBulletTracer")
                {
                    nativeBulletMaterial = material;
                    break;
                }
            }

            foreach (ActorDef def in data.actors)
            {
                // First-person POV: the camera sits where the player's head was,
                // so his ghost billboard would fill the screen. Skip it.
                if (def.isPlayer) { continue; }
                GhostActor ga = new GhostActor();
                ga.id = def.id;
                GameObject root = new GameObject("Ghost_" + def.typeName + "_" + def.id);
                if (def.isProjectile)
                {
                    // Projectiles (scrBullet is a LineRenderer, sprite-less):
                    // render as a bright tracer segment fed by the recorded
                    // per-tick motion segment.
                    GameObject lo = new GameObject("tracer");
                    lo.transform.SetParent(root.transform, false);
                    LineRenderer lr = lo.AddComponent<LineRenderer>();
                    lr.positionCount = 2;
                    lr.useWorldSpace = true;
                    bool playerTracer = def.typeName != null &&
                        def.typeName.StartsWith("prefabTracer", StringComparison.OrdinalIgnoreCase);
                    float nativeWidth = playerTracer ? 0.35f : 0.15f;
                    lr.startWidth = nativeWidth;
                    lr.endWidth = nativeWidth;
                    if (nativeBulletMaterial != null) { lr.sharedMaterial = nativeBulletMaterial; }
                    else { lr.material = new Material(Shader.Find("Sprites/Default")); }
                    lr.startColor = Color.white;
                    lr.endColor = Color.white;
                    lr.enabled = false;
                    GhostLayer gl = new GhostLayer();
                    gl.go = lo;
                    gl.sr = null;
                    gl.tracer = lr;
                    gl.header = new LayerHeader();
                    ga.layers.Add(gl);
                    ghosts.Add(ga);
                    continue;
                }
                for (int i = 0; i < def.layers.Count; i++)
                {
                    LayerHeader h = def.layers[i];
                    GameObject lo = new GameObject("layer" + i);
                    lo.transform.SetParent(root.transform, false);
                    lo.transform.localScale = h.lossyScale;
                    SpriteRenderer sr = lo.AddComponent<SpriteRenderer>();
                    sr.sortingOrder = h.sortOrder;
                    try { sr.sortingLayerID = h.sortingLayerId; } catch (Exception) { }
                    sr.color = new Color(h.r, h.g, h.b, h.a);
                    GhostLayer gl = new GhostLayer();
                    gl.go = lo;
                    gl.sr = sr;
                    gl.header = h;
                    ga.layers.Add(gl);
                }
                ghosts.Add(ga);
            }

            // The game runs a custom render pipeline; the built-in sprite shader
            // a fresh SpriteRenderer gets may not render at all in it. Clone the
            // material from a live scene sprite instead so ghosts use exactly
            // what the game's own sprites use.
            SpriteRenderer donorSr = UnityEngine.Object.FindFirstObjectByType<SpriteRenderer>(
                FindObjectsInactive.Exclude);
            Material donorMat = donorSr != null ? donorSr.sharedMaterial : null;
            if (donorMat != null)
            {
                foreach (GhostActor ga in ghosts)
                {
                    foreach (GhostLayer gl in ga.layers)
                    {
                        if (gl.sr != null) { gl.sr.sharedMaterial = donorMat; }
                    }
                }
            }
            plugin.Log("Playback: ghost material " +
                (donorMat != null ? donorMat.name + " (" + donorMat.shader.name + ")" : "NOT FOUND - using default"));
            plugin.Log("Playback: bullet material " +
                (nativeBulletMaterial != null ? nativeBulletMaterial.name + " (native widths)" : "NOT FOUND - fallback"));
        }

        // The scene carries extra cameras that normally render into render
        // textures owned by the HUD system (UIcamera depth=50, Viewmodel
        // Camera depth=5, stopwatch/visualizer/light-average helpers). With
        // the HUD torn down they render straight to the screen instead and
        // their black output paints over the entire 3D view - the "black
        // screen" bug. Playback only needs the world camera.
        private void DisableOverlayCameras()
        {
            try
            {
                Camera main = Camera.main;
                int off = 0;
                Camera[] cams = UnityEngine.Object.FindObjectsByType<Camera>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (Camera c in cams)
                {
                    if (c == null || c == main) { continue; }
                    c.enabled = false;
                    off++;
                }
                plugin.Log("Playback: disabled " + off + " overlay camera(s); keeping " +
                    (main == null ? "NULL" : main.gameObject.name) + ".");
            }
            catch (Exception ex)
            {
                plugin.LogWarn("DisableOverlayCameras failed: " + ex.Message);
            }
        }

        // RetroPixelMax / Dither / PostProcessLayer blit the camera image every
        // frame. Their palette and pattern data are set up by the game's own
        // level flow (scrPlayerControl.prefsUpdated), which a raw scene load
        // bypasses - the blit then outputs black. Off by default in playback.
        private void DisableCameraPostFx()
        {
            try
            {
                Camera main = Camera.main;
                if (main == null) { return; }
                int off = 0;
                MonoBehaviour[] comps = main.GetComponents<MonoBehaviour>();
                foreach (MonoBehaviour b in comps)
                {
                    if (b == null) { continue; }
                    string n = b.GetType().Name;
                    if (n == "RetroPixelMax" || n == "Dither" || n == "PostProcessLayer")
                    {
                        b.enabled = false;
                        off++;
                    }
                }
                plugin.Log("Playback: disabled " + off + " camera post effect(s).");
            }
            catch (Exception ex)
            {
                plugin.LogWarn("DisableCameraPostFx failed: " + ex.Message);
            }
        }

        // The scene's Main Camera sits in game state we cannot reproduce by
        // loading the map raw - even with every post effect and overlay camera
        // disabled it renders only the skybox (verified: a forced green clear
        // never reached the screen, while a fresh camera rendered the world
        // and ghosts perfectly). So playback uses its own clean camera and
        // parks the scene cameras.
        private void SpawnReplayCamera()
        {
            try
            {
                Camera main = Camera.main;
                float fov = main != null ? main.fieldOfView : 90f;
                if (main != null)
                {
                    main.enabled = false;
                    AudioListener mainListener = main.GetComponent<AudioListener>();
                    if (mainListener != null) { mainListener.enabled = false; }
                }

                GameObject go = new GameObject("ReplayCamera");
                DontDestroyOnLoad(go);
                replayCam = go.AddComponent<Camera>();
                replayCam.fieldOfView = fov;
                replayCam.nearClipPlane = 0.05f;
                replayCam.farClipPlane = 2000f;
                replayCam.cullingMask = ~0;
                replayCam.clearFlags = CameraClearFlags.Skybox;
                replayCam.depth = 100f;
                go.AddComponent<AudioListener>();
                plugin.Log("Playback: replay camera spawned (fov " + fov.ToString("F0") + "); scene cameras parked.");
            }
            catch (Exception ex)
            {
                plugin.LogWarn("SpawnReplayCamera failed: " + ex.Message);
            }
        }

        private void HideHud()
        {
            try
            {
                Scene active = SceneManager.GetActiveScene();
                Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
                int hidden = 0;
                foreach (Canvas c in canvases)
                {
                    if (c == null || c.gameObject.scene != active) { continue; }
                    c.enabled = false;
                    hidden++;
                }
                scrPlayerHUD[] huds = UnityEngine.Object.FindObjectsByType<scrPlayerHUD>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (scrPlayerHUD h in huds) { if (h != null) { h.enabled = false; } }
                plugin.Log("Playback: hid " + hidden + " canvas(es).");
            }
            catch (Exception ex)
            {
                plugin.LogWarn("HideHud failed: " + ex.Message);
            }
        }

        private void LogDiagnostics()
        {
            try
            {
                int layerTotal = 0;
                foreach (GhostActor ga in ghosts) { layerTotal += ga.layers.Count; }

                int resolved = 0;
                List<string> missing = new List<string>();
                foreach (string name in data.spriteTable)
                {
                    if (spriteLookup.ContainsKey(name)) { resolved++; }
                    else if (missing.Count < 12) { missing.Add(name); }
                }

                ReplayFrame f0 = data.frames[0];
                int visible0 = 0;
                foreach (ActorFrame af in f0.entries)
                {
                    foreach (LayerState ls in af.layers) { if (ls.spriteIdx >= 0) { visible0++; } }
                }

                Camera cam = replayCam != null ? replayCam : Camera.main;
                Scene active = SceneManager.GetActiveScene();
                int meshCount = UnityEngine.Object.FindObjectsByType<MeshRenderer>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
                plugin.Log(string.Format(
                    "Playback diag: scene={0} frames={1} actors={2} ghostLayers={3} spriteTable={4} resolved={5} missing=[{6}] frame0entries={7} frame0visibleLayers={8} cam={9} camPos={10} ambient={11} fog={12} meshRenderers={13} sceneRoots={14}",
                    data.sceneName, data.frames.Count, data.actors.Count, layerTotal,
                    data.spriteTable.Count, resolved, string.Join(",", missing.ToArray()),
                    f0.entries.Count, visible0,
                    cam == null ? "NULL" : cam.gameObject.name,
                    cam == null ? "-" : cam.transform.position.ToString("F1"),
                    RenderSettings.ambientLight.ToString("F2"),
                    RenderSettings.fog ? "on" : "off",
                    meshCount, active.rootCount));
            }
            catch (Exception ex)
            {
                plugin.LogWarn("Diagnostics failed: " + ex.Message);
            }
        }

        private void Update()
        {
            if (data == null || data.frames.Count < 2) { return; }

            // ESC exits to the main menu (Input System, since legacy may be off).
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                ExitToMenu();
                return;
            }

            // Advance exactly one selected replay clock. Scaled game time removes
            // SuperHot's frozen wall-clock stretches by definition; multiplying
            // by 1/timeScale again would double-compensate the slowdown. Camera,
            // actors, weapon and audio all derive from this same cursor below.
            float speed = plugin.PlaySpeedValue;
            bool compressed = plugin.CompressFrozenTimeValue;
            if (compressed)
            {
                playST += Time.unscaledDeltaTime * speed;
            }
            else
            {
                playT += Time.unscaledDeltaTime * speed;
            }

            ReplayFrame last = data.frames[data.frames.Count - 1];
            float cursor = compressed ? playST : playT;
            float end = ReplayTimeline.FrameTime(last, compressed);
            if (cursor >= end)
            {
                if (!finishedNoticeShown)
                {
                    finishedNoticeShown = true;
                    plugin.ShowStatus("Replay finished. ESC to exit.");
                }
                cursor = end;
                if (compressed) { playST = cursor; }
                else { playT = cursor; }
            }

            // Advance on the selected clock, then derive the other timestamp so
            // audio and diagnostics stay synchronized in either playback mode.
            while (frameIdx < data.frames.Count - 2 &&
                ReplayTimeline.FrameTime(data.frames[frameIdx + 1], compressed) <= cursor)
            {
                frameIdx++;
            }
            ReplayFrame clockA = data.frames[frameIdx];
            ReplayFrame clockB = data.frames[Mathf.Min(frameIdx + 1, data.frames.Count - 1)];
            float clockK = ReplayTimeline.Interpolation(clockA, clockB, cursor, compressed);
            if (compressed) { playT = Mathf.Lerp(clockA.t, clockB.t, clockK); }
            else { playST = Mathf.Lerp(clockA.st, clockB.st, clockK); }

            ScheduleAudio();
            ScheduleEffects();

            if (!deepDiagDone && playST - data.frames[0].st > 3f)
            {
                deepDiagDone = true;
                LogDeepDiagnostics();
            }

            if (playT - data.frames[0].t < 20f)
            {
                WatchCameras();
            }
        }

        private bool deepDiagDone;
        private readonly Dictionary<Camera, string> camWatch = new Dictionary<Camera, string>();

        // Watch every camera's render-relevant state for ~20 s of playback and
        // report exactly when something changes it.
        private void WatchCameras()
        {
            Camera[] cams = UnityEngine.Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Camera c in cams)
            {
                if (c == null) { continue; }
                string state = c.enabled + "|" + c.cullingMask + "|" + c.clearFlags + "|" +
                    (c.targetTexture != null ? c.targetTexture.name : "-");
                string prev;
                if (!camWatch.TryGetValue(c, out prev))
                {
                    camWatch[c] = state;
                    plugin.Log(string.Format("Playback camwatch t={0:F1}s: {1} initial {2}",
                        playT - data.frames[0].t, c.gameObject.name, state));
                    continue;
                }
                if (prev != state)
                {
                    camWatch[c] = state;
                    plugin.Log(string.Format("Playback camwatch t={0:F1}s: {1} {2} -> {3}",
                        playT - data.frames[0].t, c.gameObject.name, prev, state));
                }
            }
        }

        // One-shot, ~3 s into playback: everything that could darken or block
        // the view, so a black screen can be attributed precisely.
        private void LogDeepDiagnostics()
        {
            try
            {
                Camera cam = Camera.main;
                string camInfo = cam == null ? "NULL" : string.Format(
                    "{0} enabled={1} clear={2} bg={3} cullingMask={4} depth={5} rect={6} fov={7} pos={8}",
                    cam.gameObject.name, cam.enabled, cam.clearFlags, cam.backgroundColor,
                    cam.cullingMask, cam.depth, cam.rect.ToString(), cam.fieldOfView,
                    cam.transform.position.ToString("F1"));

                int activeCanvases = 0;
                string overlayInfo = "";
                Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                foreach (Canvas c in canvases)
                {
                    if (c == null || !c.isActiveAndEnabled) { continue; }
                    activeCanvases++;
                    if (activeCanvases <= 6)
                    {
                        Renderer cr = c.GetComponent<Renderer>();
                        overlayInfo += string.Format("[{0} mode={1} order={2} mat={3}]",
                            c.gameObject.name, c.renderMode,
                            c.sortingOrder,
                            cr != null && cr.material != null ? cr.material.name + " col=" + cr.material.color : "-");
                    }
                }

                string faderInfo = "none";
                MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (MonoBehaviour b in behaviours)
                {
                    if (b == null || b.GetType().Name != "scrCameraFader") { continue; }
                    Type ft = b.GetType();
                    FieldInfo sf = ft.GetField("fadeStart");
                    FieldInfo ef = ft.GetField("fadeEnd");
                    FieldInfo mf = ft.GetField("matColor");
                    Renderer rr = b.GetComponent<Renderer>();
                    faderInfo = string.Format("enabled={0} rendererEnabled={1} fadeStart={2} fadeEnd={3} matColor={4}",
                        b.enabled,
                        rr != null ? rr.enabled.ToString() : "-",
                        sf != null ? sf.GetValue(b) : "?",
                        ef != null ? ef.GetValue(b) : "?",
                        mf != null ? mf.GetValue(b) : "?");
                    break;
                }

                plugin.Log(string.Format(
                    "Playback deep diag: cam={0} | activeCanvases={1} {2} | fogColor={3} fogDensity={4} ambient={5} skybox={6} | fader {7} | timeScale={8}",
                    camInfo, activeCanvases, overlayInfo,
                    RenderSettings.fogColor, RenderSettings.fog? RenderSettings.fogDensity.ToString("F3") : "-",
                    RenderSettings.ambientLight,
                    RenderSettings.skybox != null ? RenderSettings.skybox.name : "null",
                    faderInfo, Time.timeScale));

                // All cameras: a second camera with a bigger depth would paint
                // over everything we do.
                string camList = "";
                Camera[] cams = UnityEngine.Object.FindObjectsByType<Camera>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (Camera c2 in cams)
                {
                    camList += string.Format("[{0} en={1} depth={2} rt={3}]",
                        c2.gameObject.name, c2.enabled, c2.depth,
                        c2.targetTexture != null ? c2.targetTexture.name : "-");
                }
                plugin.Log("Playback deep diag cams: " + camList);

                // Sample ghosts: is Unity actually rendering them?
                string ghostInfo = "";
                int shown = 0;
                foreach (GhostActor ga in ghosts)
                {
                    foreach (GhostLayer gl in ga.layers)
                    {
                        if (shown >= 4) { break; }
                        if (gl.sr == null || gl.sr.sprite == null || !gl.sr.enabled) { continue; }
                        float dist = cam != null ? Vector3.Distance(cam.transform.position, gl.go.transform.position) : -1f;
                        ghostInfo += string.Format("[{0} pos={1} dist={2:F1} visible={3} en={4} spr={5}]",
                            gl.go.name, gl.go.transform.position.ToString("F1"), dist,
                            gl.sr.isVisible, gl.sr.enabled, gl.sr.sprite.name);
                        shown++;
                    }
                    if (shown >= 4) { break; }
                }
                plugin.Log("Playback deep diag ghosts: " + ghostInfo);

                // Post-processing stack on the main camera.
                string postInfo = "none";
                if (cam != null)
                {
                    MonoBehaviour[] comps = cam.GetComponents<MonoBehaviour>();
                    foreach (MonoBehaviour b in comps)
                    {
                        if (b == null) { continue; }
                        postInfo += "[" + b.GetType().Name + " en=" + b.enabled + "]";
                    }
                }
                plugin.Log("Playback deep diag camComps: " + postInfo);

                // Frustum + active-geometry ground truth, plus a bright test
                // marker 3 m in front of the camera.
                if (cam != null)
                {
                    Vector3 vp = cam.WorldToViewportPoint(new Vector3(30.7f, 1.9f, -13.3f));
                    plugin.Log(string.Format(
                        "Playback deep diag frustum: ghostViewport={0} (onScreen={1})",
                        vp.ToString("F2"),
                        vp.z > 0f && vp.x > 0f && vp.x < 1f && vp.y > 0f && vp.y < 1f));

                    int activeMeshes = 0; int nearMeshes = 0;
                    MeshRenderer[] meshes = UnityEngine.Object.FindObjectsByType<MeshRenderer>(
                        FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                    foreach (MeshRenderer mr in meshes)
                    {
                        activeMeshes++;
                        if (Vector3.Distance(cam.transform.position, mr.transform.position) < 30f) { nearMeshes++; }
                    }
                    plugin.Log(string.Format(
                        "Playback deep diag geometry: activeMeshRenderers={0} within30m={1}",
                        activeMeshes, nearMeshes));
                }
            }
            catch (Exception ex)
            {
                plugin.LogWarn("Deep diagnostics failed: " + ex.Message);
            }
        }

        // Applied in LateUpdate so any surviving game script that still writes
        // the camera this frame is overridden before rendering.
        private void LateUpdate()
        {
            if (data == null || data.frames.Count < 2) { return; }

            // Any time-modding mod that still thinks this is gameplay would
            // crawl the audio; playback owns time while it is on screen.
            Time.timeScale = 1f;

            ApplyFrame();
            ApplyRecordedCamera();
        }

        // Fire every recorded sound whose scaled timestamp has arrived. Clips
        // are played at their recorded world position, so they are spatialized
        // exactly where they happened relative to the camera path.
        private void ScheduleAudio()
        {
            if (!plugin.PlayRecordedAudioValue || data.audio.Count == 0) { return; }
            int guard = 0;
            while (audioIdx < data.audio.Count && data.audio[audioIdx].st <= playST && guard < 16)
            {
                guard++;
                AudioEvent ev = data.audio[audioIdx];
                audioIdx++;
                AudioClip clip;
                if (clipLookup == null || !clipLookup.TryGetValue(ev.clip, out clip) || clip == null)
                {
                    continue;
                }
                AudioSource.PlayClipAtPoint(clip, ev.pos, Mathf.Clamp(ev.vol, 0.05f, 1f));
            }
        }

        private static string CleanEffectName(string name)
        {
            if (string.IsNullOrEmpty(name)) { return ""; }
            const string clone = "(Clone)";
            return name.EndsWith(clone, StringComparison.OrdinalIgnoreCase)
                ? name.Substring(0, name.Length - clone.Length).TrimEnd()
                : name;
        }

        private void ScheduleEffects()
        {
            if (data.effects.Count == 0 || effectLookup == null) { return; }
            int guard = 0;
            while (effectIdx < data.effects.Count && data.effects[effectIdx].st <= playST && guard < 32)
            {
                guard++;
                EffectEvent ev = data.effects[effectIdx++];
                GameObject prefab;
                if (!effectLookup.TryGetValue(ev.prefab, out prefab) || prefab == null) { continue; }

                GameObject instance = Instantiate(prefab, ev.pos, ev.rot);
                instance.name = "ReplayEffect_" + ev.prefab;
                spawnedEffects.Add(instance);

                // Preserve CULTIC's native particles, animated sprites, lights
                // and cleanup timing, but make the effect presentation-only.
                scrExplosion[] explosions = instance.GetComponentsInChildren<scrExplosion>(true);
                foreach (scrExplosion explosion in explosions)
                {
                    explosion.dealsDamage = false;
                    explosion.ignitesTargets = false;
                    explosion.shakeMultiplier = 0f;
                }
                Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);
                foreach (Collider collider in colliders) { collider.enabled = false; }
            }
        }

        private void ApplyFrame()
        {
            ReplayFrame a = data.frames[frameIdx];
            ReplayFrame b = data.frames[Mathf.Min(frameIdx + 1, data.frames.Count - 1)];
            bool compressed = plugin.CompressFrozenTimeValue;
            float cursor = compressed ? playST : playT;
            float k = ReplayTimeline.Interpolation(a, b, cursor, compressed);

            Dictionary<int, ActorFrame> amap = new Dictionary<int, ActorFrame>();
            foreach (ActorFrame af in a.entries) { amap[af.actorId] = af; }
            Dictionary<int, ActorFrame> bmap = new Dictionary<int, ActorFrame>();
            foreach (ActorFrame bf in b.entries) { bmap[bf.actorId] = bf; }

            foreach (GhostActor ga in ghosts)
            {
                ActorFrame fa, fb;
                if (!amap.TryGetValue(ga.id, out fa))
                {
                    SetGhostVisible(ga, false);
                    continue;
                }
                // Keep the last captured state until the following frame. On the
                // next frame the actor is absent from A and is hidden. Previously
                // destroyed bullets/throws could remain frozen in mid-air forever.
                if (!bmap.TryGetValue(ga.id, out fb)) { fb = fa; }
                for (int i = 0; i < ga.layers.Count && i < fa.layers.Count && i < fb.layers.Count; i++)
                {
                    LayerState la = fa.layers[i];
                    LayerState lb = fb.layers[i];
                    GhostLayer gl = ga.layers[i];

                    if (gl.tracer != null)
                    {
                        ApplyTracer(gl, la, lb, k);
                        continue;
                    }

                    if (la.spriteIdx < 0)
                    {
                        gl.sr.enabled = false;
                        continue;
                    }
                    gl.sr.enabled = true;
                    if (la.spriteIdx < data.spriteTable.Count)
                    {
                        Sprite s;
                        string name = data.spriteTable[la.spriteIdx];
                        if (spriteLookup.TryGetValue(name, out s)) { gl.sr.sprite = s; }
                        else { gl.sr.enabled = false; }
                    }
                    // Do not lerp a visible layer toward the hidden sentinel's
                    // zero position; hold it until the next frame hides it.
                    float layerK = lb.spriteIdx < 0 ? 0f : k;
                    gl.go.transform.position = Vector3.Lerp(la.pos, lb.pos, layerK);
                    gl.go.transform.rotation = Quaternion.Euler(0f,
                        Mathf.LerpAngle(la.yaw, lb.yaw, layerK), 0f);
                    gl.sr.flipX = la.flipX;
                }
            }

            ApplyWeaponUi(a, b, k);
        }

        private static void SetGhostVisible(GhostActor actor, bool visible)
        {
            foreach (GhostLayer layer in actor.layers)
            {
                if (layer.sr != null) { layer.sr.enabled = visible; }
                if (layer.tracer != null) { layer.tracer.enabled = visible; }
            }
        }

        private void ApplyTracer(GhostLayer gl, LayerState la, LayerState lb, float k)
        {
            if (la.spriteIdx < 0 || la.aux <= 0.001f)
            {
                gl.tracer.enabled = false;
                return;
            }
            Vector3 pos = Vector3.Lerp(la.pos, lb.pos, k);
            float yaw = Mathf.LerpAngle(la.yaw, lb.yaw, k) * Mathf.Deg2Rad;
            float pitch = Mathf.LerpAngle(la.pitch, lb.pitch, k) * Mathf.Deg2Rad;
            float len = Mathf.Lerp(la.aux, lb.aux, k);
            float pitchCos = Mathf.Cos(pitch);
            Vector3 dir = new Vector3(
                Mathf.Sin(yaw) * pitchCos,
                Mathf.Sin(pitch),
                Mathf.Cos(yaw) * pitchCos);
            gl.tracer.SetPosition(0, pos - dir * (len * 0.5f));
            gl.tracer.SetPosition(1, pos + dir * (len * 0.5f));
            gl.tracer.enabled = true;
        }

        private void ApplyWeaponUi(ReplayFrame a, ReplayFrame b, float k)
        {
            if (weaponImage == null) { return; }
            int idx = a.wpSprite;
            if (idx < 0)
            {
                weaponImage.enabled = false;
                return;
            }
            Sprite s = null;
            if (idx < data.spriteTable.Count)
            {
                spriteLookup.TryGetValue(data.spriteTable[idx], out s);
            }
            if (s == null)
            {
                weaponImage.enabled = false;
                return;
            }
            weaponImage.enabled = a.wpEnabled;
            weaponImage.sprite = s;
            if (data.formatVersion >= 3)
            {
                float screenK = b.wpSprite < 0 ? 0f : k;
                Vector2 normalizedPos = Vector2.Lerp(a.wpPos, b.wpPos, screenK);
                Vector2 normalizedSize = Vector2.Lerp(a.wpSize, b.wpSize, screenK);
                RectTransform rt = weaponImage.rectTransform;
                rt.anchorMin = normalizedPos;
                rt.anchorMax = normalizedPos;
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(
                    normalizedSize.x * Screen.width,
                    normalizedSize.y * Screen.height);
                Vector2 scale = Vector2.Lerp(a.wpScale, b.wpScale, screenK);
                rt.localScale = new Vector3(scale.x, scale.y, 1f);
                rt.localRotation = Quaternion.Euler(0f, 0f,
                    Mathf.LerpAngle(a.wpRotZ, b.wpRotZ, screenK));
                return;
            }
            weaponImage.rectTransform.anchorMin = a.wpAnchorMin;
            weaponImage.rectTransform.anchorMax = a.wpAnchorMax;
            weaponImage.rectTransform.pivot = a.wpPivot;
            weaponImage.rectTransform.anchoredPosition = a.wpPos;
            weaponImage.rectTransform.sizeDelta = a.wpSize;
            weaponImage.rectTransform.localScale = new Vector3(a.wpScale.x, a.wpScale.y, 1f);
            weaponImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, a.wpRotZ);
        }

        private void ApplyRecordedCamera()
        {
            // First-person POV follows the same selected clock as every ghost.
            ReplayFrame a = data.frames[frameIdx];
            ReplayFrame b = data.frames[Mathf.Min(frameIdx + 1, data.frames.Count - 1)];
            bool compressed = plugin.CompressFrozenTimeValue;
            float cursor = compressed ? playST : playT;
            float k = ReplayTimeline.Interpolation(a, b, cursor, compressed);

            Vector3 pos = Vector3.Lerp(a.camPos, b.camPos, k);
            float pitch = Mathf.LerpAngle(a.camPitch, b.camPitch, k);
            float yaw = Mathf.LerpAngle(a.camYaw, b.camYaw, k);

            Camera cam = replayCam != null && replayCam.enabled ? replayCam : Camera.main;
            if (cam != null)
            {
                cam.transform.position = pos;
                cam.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            }
        }

        private void ExitToMenu()
        {
            plugin.ShowStatus("Exiting replay.");
            SceneManager.LoadSceneAsync("sceneMainMenu");
            Destroy(gameObject, 1f);
        }

        // Tells the SuperHot mod (if installed) to fully stand down during
        // playback. Done via reflection so Replay does not hard-depend on it:
        // a freshly loaded replay map has gameState == 0 and a live player
        // object, which SuperHot reads as real gameplay - pressing movement
        // keys during a replay then froze time and slowed the audio.
        private static void SetSuperHotStandDown(bool on)
        {
            try
            {
                foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type t = asm.GetType("CulticSuperHot.SuperHotPlugin");
                    if (t == null) { continue; }
                    FieldInfo f = t.GetField("StandDown");
                    if (f != null)
                    {
                        f.SetValue(null, on);
                        Debug.Log("[Replay] SuperHot StandDown=" + on);
                    }
                    return;
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[Replay] SuperHot stand-down failed: " + ex.Message);
            }
        }

        private void OnGUI()
        {
            if (data == null || data.frames.Count == 0) { return; }
            bool compressed = plugin.CompressFrozenTimeValue;
            float start = ReplayTimeline.FrameTime(data.frames[0], compressed);
            float end = ReplayTimeline.FrameTime(data.frames[data.frames.Count - 1], compressed);
            float cursor = compressed ? playST : playT;
            float elapsed = Mathf.Clamp(cursor - start, 0f, end - start);
            GUI.Label(new Rect(18f, 18f, 900f, 28f),
                "[Replay] " + data.sceneName + "  " +
                elapsed.ToString("F1") + "s / " + (end - start).ToString("F1") +
                (compressed ? "s (real-time action)" : "s (recorded wall time)") +
                "   (ESC: exit)");
        }
    }

    // ======================================================================
    // Native menu integration.
    //
    // The game's buttons dispatch custom actions via
    //   systemMenu.SendMessage(invokedFunction, functionValue)
    // so any component on the scrMenuControllerV2 GameObject can receive them.
    // We clone an existing main-menu button (native look/sounds/navigation),
    // point it at our hook methods, and register a fully native browser Menu
    // (rows per replay file) into the controller's menu list.
    // ======================================================================

    public class ReplayMenuHook : MonoBehaviour
    {
        private const int BrowserMenuId = 7771;

        private CulticReplayPlugin plugin;
        private scrMenuControllerV2 menu;
        private Menu browser;
        private readonly List<string> browserFiles = new List<string>();
        private bool built;

        public void Setup(CulticReplayPlugin p)
        {
            plugin = p;
        }

        private IEnumerator Start()
        {
            // The controller fills its menu array in its own Start; retry until
            // everything we need exists (or give up after ~20 s).
            float waited = 0f;
            while (!built && waited < 20f)
            {
                if (menu == null)
                {
                    menu = GetComponent<scrMenuControllerV2>();
                    if (menu == null) { yield break; }
                }
                TryBuild();
                yield return null;
                waited += Time.unscaledDeltaTime;
            }
            if (!built)
            {
                plugin.LogWarn("Replay menu build timed out (main menu not found).");
            }
        }

        private void TryBuild()
        {
            if (built) { return; }
            Menu main = FindMenu(MenuID.menuMain);
            if (main == null || main.menuElements == null || main.menuElements.Length == 0) { return; }

            scrMenuButton donor = null;
            foreach (scrMenuElement el in main.menuElements)
            {
                scrMenuButton b = el as scrMenuButton;
                if (b != null && b.isActive && b.controlMainText != null)
                {
                    donor = b; // keep the last one; usually the bottom-most entry
                }
            }
            if (donor == null) { return; }

            if (!InjectMainButton(main, donor)) { return; }
            if (!BuildBrowser(main, donor)) { return; }

            built = true;
            plugin.ShowStatus("Replay menu ready.");
        }

        private Menu FindMenu(MenuID id)
        {
            if (menu.menus == null) { return null; }
            foreach (Menu m in menu.menus)
            {
                if (m != null && m.menuID == id) { return m; }
            }
            return null;
        }

        private bool InjectMainButton(Menu main, scrMenuButton donor)
        {
            try
            {
                GameObject cloneGo = Instantiate(donor.gameObject);
                cloneGo.name = "SHR_ReplaysButton";
                cloneGo.transform.SetParent(donor.transform.parent, false);

                scrMenuButton btn = cloneGo.GetComponent<scrMenuButton>();
                if (btn == null) { Destroy(cloneGo); return false; }

                btn.buttonFunction = (scrMenuButton.ButtonFunction)2; // CustomInvoke
                btn.buttonMenuTarget = MenuID.menuNull;
                btn.invokedFunction = "SHR_OpenBrowser";
                btn.functionValue = "";
                btn.elementIndex = 990001;
                btn.controlMainText.text = "REPLAYS";

                // Register into the menu and rebuild the vertical nav chain.
                scrMenuElement[] old = main.menuElements;
                scrMenuElement[] arr = new scrMenuElement[old.Length + 1];
                for (int i = 0; i < old.Length; i++) { arr[i] = old[i]; }
                arr[old.Length] = btn;
                main.menuElements = arr;
                RewireVertical(arr);

                return true;
            }
            catch (Exception ex)
            {
                plugin.LogWarn("Main-menu button injection failed: " + ex.Message);
                return false;
            }
        }

        private static void RewireVertical(scrMenuElement[] elements)
        {
            for (int i = 0; i < elements.Length; i++)
            {
                if (elements[i] == null) { continue; }
                elements[i].elementAbove = (i > 0) ? elements[i - 1] : elements[elements.Length - 1];
                elements[i].elementBelow = (i < elements.Length - 1) ? elements[i + 1] : elements[0];
            }
        }

        private bool BuildBrowser(Menu main, scrMenuButton donor)
        {
            try
            {
                if (main.menuGroup == null || donor == null || donor.transform.parent == null)
                {
                    return false;
                }
                GameObject group = Instantiate(main.menuGroup);
                group.name = "SHR_ReplayBrowser";
                group.transform.SetParent(main.menuGroup.transform.parent, false);

                // Locate the cloned counterpart of the donor button so rows can
                // live in the same container (same anchors / layout group).
                Transform rowsParent = null;
                scrMenuButton[] clonedButtons = group.GetComponentsInChildren<scrMenuButton>(true);
                foreach (scrMenuButton cb in clonedButtons)
                {
                    if (cb.gameObject.name == donor.gameObject.name && cb.transform.parent != null)
                    {
                        rowsParent = cb.transform.parent;
                        Destroy(cb.gameObject);
                        break;
                    }
                }
                if (rowsParent == null)
                {
                    // Fall back to the group root; rows will be positioned manually.
                    rowsParent = group.transform;
                }

                // Strip every other interactive element from the clone; the rest
                // of the visual structure (panel, backdrop) stays native.
                scrMenuElement[] stale = group.GetComponentsInChildren<scrMenuElement>(true);
                foreach (scrMenuElement el in stale)
                {
                    Destroy(el.gameObject);
                }

                // Hidden until opened - the game's openMenu only ever ACTIVATES
                // a target group, it never deactivates the previous one.
                group.SetActive(false);

                Menu m = new Menu();
                m.menuName = "Replays";
                m.menuID = (MenuID)BrowserMenuId;
                m.menuGroup = group;
                m.menuPanel = main.menuPanel;
                m.menuRect = main.menuRect;
                m.returnMenu = MenuID.menuMain;
                m.returnElement = null;
                m.closeAllMenus = true;
                m.showHelpPanel = false;

                browser = m;
                browserRowsParent = rowsParent;
                AppendBrowserMenu(m);
                return true;
            }
            catch (Exception ex)
            {
                plugin.LogWarn("Browser menu construction failed: " + ex.Message);
                return false;
            }
        }

        private void AppendBrowserMenu(Menu m)
        {
            Menu[] old = menu.menus;
            Menu[] arr = new Menu[old.Length + 1];
            for (int i = 0; i < old.Length; i++) { arr[i] = old[i]; }
            arr[old.Length] = m;
            menu.menus = arr;
        }

        // ---- SendMessage targets (called by native buttons) ----------------

        public void SHR_OpenBrowser(object value)
        {
            if (!built)
            {
                TryBuild();
                if (!built)
                {
                    plugin.ShowStatus("Replay menu unavailable here.");
                    return;
                }
            }
            // openMenu only activates the target group; hide the others so the
            // browser does not stack on top of a still-visible main menu.
            if (menu.menus != null)
            {
                foreach (Menu m in menu.menus)
                {
                    if (m == null || m == browser || m.menuGroup == null) { continue; }
                    if (m.menuGroup.activeSelf) { m.menuGroup.SetActive(false); }
                }
            }
            RefreshRows();
            menu.changeMenuByID((MenuID)BrowserMenuId, null, false);
        }

        public void SHR_PlayReplay(object value)
        {
            string s = value as string;
            int idx;
            if (!Int32.TryParse(s, out idx))
            {
                plugin.ShowStatus("Replay launch got bad argument '" + (s == null ? "null" : s) + "'.");
                return;
            }
            if (idx < 0 || idx >= browserFiles.Count)
            {
                plugin.ShowStatus("Replay row index " + idx + " out of range (" + browserFiles.Count + " loaded).");
                return;
            }
            string file = browserFiles[idx];
            plugin.ShowStatus("Launching replay " + Path.GetFileName(file) + "...");
            menu.closeAllMenus();
            plugin.StartReplayLoad(file);
        }

        // ---- Row construction ----------------------------------------------

        private void ClearRows()
        {
            if (browser == null || browser.menuGroup == null) { return; }
            scrMenuElement[] rows = browser.menuGroup.GetComponentsInChildren<scrMenuElement>(true);
            foreach (scrMenuElement el in rows)
            {
                if (el.gameObject == rowTemplate) { continue; } // template has no element anyway, belt and braces
                Destroy(el.gameObject);
            }
            browser.menuElements = new scrMenuElement[0];
        }

        private void RefreshRows()
        {
            ClearRows();
            browserFiles.Clear();

            List<string> files = CulticReplayPlugin.ListReplays();
            GameObject template = MakeRowTemplate();

            List<scrMenuElement> made = new List<scrMenuElement>();
            int slot = 0;

            for (int i = 0; i < files.Count && slot < 12; i++)
            {
                string scene = ReplayFile.ReadSceneName(files[i]);
                if (scene == null) { continue; }
                string label = Path.GetFileNameWithoutExtension(files[i]);
                scrMenuElement row = MakeRow(template, label, "SHR_PlayReplay", slot.ToString(), slot, true);
                if (row != null)
                {
                    browserFiles.Add(files[i]);
                    made.Add(row);
                    slot++;
                }
            }

            if (made.Count == 0)
            {
                scrMenuElement empty = MakeRow(template, "NO REPLAYS YET - PRESS " + plugin.SaveKeyHint + " IN GAME", "", "", slot, false);
                if (empty != null)
                {
                    made.Add(empty);
                    slot++;
                }
            }

            scrMenuElement back = MakeRow(template, "BACK", "", "", slot, true);
            if (back != null)
            {
                scrMenuButton bb = back as scrMenuButton;
                if (bb != null)
                {
                    bb.buttonFunction = scrMenuButton.ButtonFunction.ReturnToMenu;
                    bb.invokedFunction = "";
                    bb.buttonMenuTarget = MenuID.menuNull;
                }
                made.Add(back);
            }

            browser.menuElements = made.ToArray();
            RewireVertical(browser.menuElements);
            if (browser.menuElements.Length > 0)
            {
                browser.defaultMenuObject = browser.menuElements[0];
            }
            else
            {
                browser.defaultMenuObject = null;
            }
        }

        private GameObject rowTemplate;
        private Transform browserRowsParent;
        private bool rowsParentHasLayout;

        private GameObject MakeRowTemplate()
        {
            if (rowTemplate != null) { return rowTemplate; }

            Menu main = FindMenu(MenuID.menuMain);
            if (main == null || main.menuElements == null) { return null; }
            scrMenuButton donor = null;
            foreach (scrMenuElement el in main.menuElements)
            {
                scrMenuButton b = el as scrMenuButton;
                if (b != null && b.isActive) { donor = b; break; }
            }
            if (donor == null) { return null; }

            GameObject go = Instantiate(donor.gameObject);
            go.name = "SHR_RowTemplate";
            // The template must not carry a live menu element, or the controller
            // could navigate into it while it sits hidden in our group.
            scrMenuElement tmplEl = go.GetComponent<scrMenuElement>();
            if (tmplEl != null) { Destroy(tmplEl); }
            Transform parent = browserRowsParent != null ? browserRowsParent : browser.menuGroup.transform;
            rowsParentHasLayout = parent.GetComponent<UnityEngine.UI.LayoutGroup>() != null ||
                parent.GetComponent<UnityEngine.UI.VerticalLayoutGroup>() != null;
            go.transform.SetParent(parent, false);
            go.SetActive(false);
            rowTemplate = go;
            return go;
        }

        private scrMenuElement MakeRow(GameObject template, string label, string invoke, string value, int index, bool interactive)
        {
            if (template == null) { return null; }
            GameObject go = Instantiate(template);
            go.name = "SHR_Row_" + label;
            Transform parent = browserRowsParent != null ? browserRowsParent : browser.menuGroup.transform;
            go.transform.SetParent(parent, false);

            // Stack rows vertically only when no layout group manages them.
            if (!rowsParentHasLayout)
            {
                RectTransform rt = go.GetComponent<RectTransform>();
                RectTransform tt = template.GetComponent<RectTransform>();
                if (rt != null && tt != null)
                {
                    rt.anchorMin = tt.anchorMin;
                    rt.anchorMax = tt.anchorMax;
                    rt.pivot = tt.pivot;
                    rt.sizeDelta = tt.sizeDelta;
                    Vector2 p = tt.anchoredPosition;
                    float step = tt.sizeDelta.y > 1f ? tt.sizeDelta.y * 1.25f : 40f;
                    p.y -= step * index;
                    rt.anchoredPosition = p;
                    rt.localScale = tt.localScale;
                }
            }

            go.SetActive(true);
            scrMenuButton btn = go.GetComponent<scrMenuButton>();
            if (btn == null) { Destroy(go); return null; }

            // elementIndex MUST be -1 here: the game's Press() passes
            // elementIndex.ToString() to SendMessage unless it is -1, in which
            // case functionValue goes through. We need functionValue.
            btn.systemMenu = menu;
            btn.elementIndex = -1;
            btn.buttonFunction = scrMenuButton.ButtonFunction.CustomInvoke;
            btn.invokedFunction = invoke == null ? "" : invoke;
            btn.functionValue = value == null ? "" : value;
            btn.controlMainText.text = label;
            btn.ignoreElement = !interactive;
            return btn;
        }
    }

    // ======================================================================
    // Projectile spawn capture. Registering at Start closes the gap where a
    // very short-lived bullet can be born and destroyed between recorder scans.
    // The bool says only that the object must be a tracer; other types use their
    // real SpriteRenderer when present and automatically fall back to a tracer.
    // ======================================================================

    public static class ProjectileCapturePatches
    {
        [HarmonyPatch(typeof(scrBullet), "Start"), HarmonyPostfix]
        public static void BulletStart(scrBullet __instance) { Recorder.RegisterProjectile(__instance, true); }

        [HarmonyPatch(typeof(scrBulletTracer), "Start"), HarmonyPostfix]
        public static void BulletTracerStart(scrBulletTracer __instance) { Recorder.RegisterProjectile(__instance, true); }

        [HarmonyPatch(typeof(scrThrownAxe), "Start"), HarmonyPostfix]
        public static void AxeStart(scrThrownAxe __instance) { Recorder.RegisterProjectile(__instance, false); }

        [HarmonyPatch(typeof(scrTNT), "Start"), HarmonyPostfix]
        public static void TntStart(scrTNT __instance) { Recorder.RegisterProjectile(__instance, false); }

        [HarmonyPatch(typeof(scrMolotov), "Start"), HarmonyPostfix]
        public static void MolotovStart(scrMolotov __instance) { Recorder.RegisterProjectile(__instance, false); }

        [HarmonyPatch(typeof(scrGasGrenade), "Start"), HarmonyPostfix]
        public static void GasGrenadeStart(scrGasGrenade __instance) { Recorder.RegisterProjectile(__instance, false); }

        [HarmonyPatch(typeof(scrMolomite), "Start"), HarmonyPostfix]
        public static void MolomiteStart(scrMolomite __instance) { Recorder.RegisterProjectile(__instance, false); }

        [HarmonyPatch(typeof(scrNailJar), "Start"), HarmonyPostfix]
        public static void NailJarStart(scrNailJar __instance) { Recorder.RegisterProjectile(__instance, false); }

        [HarmonyPatch(typeof(scrGrenadeShell), "Start"), HarmonyPostfix]
        public static void GrenadeShellStart(scrGrenadeShell __instance) { Recorder.RegisterProjectile(__instance, false); }

        [HarmonyPatch(typeof(scrImpactProjectile), "Start"), HarmonyPostfix]
        public static void ImpactProjectileStart(scrImpactProjectile __instance) { Recorder.RegisterProjectile(__instance, false); }

        [HarmonyPatch(typeof(scrInfectedSpit), "Start"), HarmonyPostfix]
        public static void InfectedSpitStart(scrInfectedSpit __instance) { Recorder.RegisterProjectile(__instance, false); }

        [HarmonyPatch(typeof(scrDroppedShield), "Start"), HarmonyPostfix]
        public static void DroppedShieldStart(scrDroppedShield __instance) { Recorder.RegisterProjectile(__instance, false); }

        [HarmonyPatch(typeof(scrHeliRocket), "Start"), HarmonyPostfix]
        public static void HeliRocketStart(scrHeliRocket __instance) { Recorder.RegisterProjectile(__instance, false); }

        [HarmonyPatch(typeof(scrArchonEnergyWave), "Start"), HarmonyPostfix]
        public static void EnergyWaveStart(scrArchonEnergyWave __instance) { Recorder.RegisterProjectile(__instance, false); }

        [HarmonyPatch(typeof(scrArchonBloodBall), "Start"), HarmonyPostfix]
        public static void BloodBallStart(scrArchonBloodBall __instance) { Recorder.RegisterProjectile(__instance, false); }

        [HarmonyPatch(typeof(scrVomitParticle), "Start"), HarmonyPostfix]
        public static void VomitStart(scrVomitParticle __instance) { Recorder.RegisterProjectile(__instance, false); }

        [HarmonyPatch(typeof(scrIncineratorParticle), "Start"), HarmonyPostfix]
        public static void IncineratorParticleStart(scrIncineratorParticle __instance) { Recorder.RegisterProjectile(__instance, false); }

        [HarmonyPatch(typeof(scrFireParticle), "Start"), HarmonyPostfix]
        public static void FireParticleStart(scrFireParticle __instance) { Recorder.RegisterProjectile(__instance, false); }

        [HarmonyPatch(typeof(scrFlare), "Start"), HarmonyPostfix]
        public static void FlareStart(scrFlare __instance) { Recorder.RegisterProjectile(__instance, false); }

        [HarmonyPatch(typeof(scrNail), "Start"), HarmonyPostfix]
        public static void NailStart(scrNail __instance) { Recorder.RegisterProjectile(__instance, false); }

        [HarmonyPatch(typeof(scrThrownPitchfork), "Start"), HarmonyPostfix]
        public static void PitchforkStart(scrThrownPitchfork __instance) { Recorder.RegisterProjectile(__instance, false); }

        [HarmonyPatch(typeof(scrGib), "Start"), HarmonyPostfix]
        public static void GibStart(scrGib __instance) { Recorder.RegisterProjectile(__instance, false); }

        [HarmonyPatch(typeof(scrDestructible), "Start"), HarmonyPostfix]
        public static void DestructibleStart(scrDestructible __instance) { Recorder.RegisterVisualActor(__instance); }

        [HarmonyPatch(typeof(scrExplosion), "Start"), HarmonyPostfix]
        public static void ExplosionStart(scrExplosion __instance) { Recorder.RecordEffect(__instance); }
    }

    // ======================================================================
    // Audio capture. While recording, every sound the game plays through
    // AudioSource is logged (clip name, position, volume, pitch) on the
    // scaled-time timeline so playback can reschedule it. Playback sounds
    // themselves are excluded via PlaybackDirector.Playing.
    // ======================================================================

    public static class AudioCapturePatches
    {
        [HarmonyPatch(typeof(AudioSource), "Play", new Type[0])]
        [HarmonyPostfix]
        public static void PlayPostfix(AudioSource __instance)
        {
            if (!Recorder.CanRecordAudio() || __instance == null) { return; }
            AudioClip clip = __instance.clip;
            if (clip == null) { return; }
            Recorder.RecordAudio(clip.name, __instance.transform.position, __instance.volume, __instance.pitch);
        }

        [HarmonyPatch(typeof(AudioSource), "PlayOneShot", new Type[] { typeof(AudioClip) })]
        [HarmonyPostfix]
        public static void PlayOneShotPostfix(AudioSource __instance, AudioClip clip)
        {
            if (!Recorder.CanRecordAudio() || __instance == null || clip == null) { return; }
            Recorder.RecordAudio(clip.name, __instance.transform.position, __instance.volume, __instance.pitch);
        }

        [HarmonyPatch(typeof(AudioSource), "PlayOneShot", new Type[] { typeof(AudioClip), typeof(float) })]
        [HarmonyPostfix]
        public static void PlayOneShotScaledPostfix(AudioSource __instance, AudioClip clip, float volumeScale)
        {
            if (!Recorder.CanRecordAudio() || __instance == null || clip == null) { return; }
            Recorder.RecordAudio(clip.name, __instance.transform.position, __instance.volume * volumeScale, __instance.pitch);
        }

        [HarmonyPatch(typeof(AudioSource), "PlayClipAtPoint", new Type[] { typeof(AudioClip), typeof(Vector3) })]
        [HarmonyPostfix]
        public static void PlayClipAtPointPostfix(AudioClip clip, Vector3 position)
        {
            if (!Recorder.CanRecordAudio() || clip == null) { return; }
            Recorder.RecordAudio(clip.name, position, 1f, 1f);
        }

        [HarmonyPatch(typeof(AudioSource), "PlayClipAtPoint", new Type[] { typeof(AudioClip), typeof(Vector3), typeof(float) })]
        [HarmonyPostfix]
        public static void PlayClipAtPointVolumePostfix(AudioClip clip, Vector3 position, float volume)
        {
            if (!Recorder.CanRecordAudio() || clip == null) { return; }
            Recorder.RecordAudio(clip.name, position, volume, 1f);
        }
    }
}
