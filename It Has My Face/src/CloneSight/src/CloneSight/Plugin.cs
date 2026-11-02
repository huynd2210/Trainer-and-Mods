using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using DoubleMe.Core.AI;
using DoubleMe.Core.Human;
using DoubleMe.Core.Player;
using DoubleMe.Core.Story;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace CloneSight
{
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.ithasmyface.clonesight";
        public const string Name = "CloneSight";
        public const string Version = "1.0.0";

        internal static ManualLogSource Log;

        internal static ConfigEntry<Key> ToggleKey;
        internal static ConfigEntry<bool> HoldToShow;
        internal static ConfigEntry<bool> ShowOtherClones;
        internal static ConfigEntry<Color> CloneColor;
        internal static ConfigEntry<Color> OtherCloneColor;
        internal static ConfigEntry<float> OutlinePixels;
        internal static ConfigEntry<float> FillOpacity;

        private void Awake()
        {
            Log = Logger;

            ToggleKey = Config.Bind("Controls", "Key", Key.F3,
                "Key that shows the clone through walls. The game binds no F-keys.");
            HoldToShow = Config.Bind("Controls", "HoldToShow", false,
                "true: the clone shows only while the key is held. false: the key toggles it on and off.");
            ShowOtherClones = Config.Bind("Display", "ShowOtherClones", true,
                "Also mark the hostile 'other clones' that some difficulties add to the crowd.");
            CloneColor = Config.Bind("Display", "CloneColor", new Color(1f, 0.18f, 0.53f, 1f),
                "Colour of your clone's outline.");
            OtherCloneColor = Config.Bind("Display", "OtherCloneColor", new Color(1f, 0.69f, 0.13f, 1f),
                "Colour of the other clones' outline.");
            OutlinePixels = Config.Bind("Display", "OutlinePixels", 3f,
                new ConfigDescription("Outline thickness in pixels at 1080p; scales with resolution.",
                    new AcceptableValueRange<float>(1f, 10f)));
            FillOpacity = Config.Bind("Display", "FillOpacity", 0.2f,
                new ConfigDescription("Opacity of the tint inside the outline. 0 draws the outline only.",
                    new AcceptableValueRange<float>(0f, 1f)));

            // Our own object, so the overlay survives whatever the game does to BepInEx's manager.
            var host = new GameObject("CloneSight");
            DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
            host.AddComponent<CloneSightController>();

            Log.LogInfo($"{Name} {Version} loaded. Press {ToggleKey.Value} in a level to see your clone.");
        }
    }

    /// <summary>
    /// Draws every tracked clone as a tinted silhouette with an outline, on top of the scene.
    ///
    /// A depth-only-clearing overlay camera renders after the game camera and draws each clone renderer
    /// with every vertex pushed to the near plane, ZTest Less and ZWrite on. Pinning depth makes walls
    /// irrelevant and lets each pixel be written once: the fill pass claims the silhouette, then eight
    /// copies shifted a few pixels in screen space can only land outside it, which forms the outline.
    /// </summary>
    internal sealed class CloneSightController : MonoBehaviour
    {
        private const float RescanSeconds = 0.5f;
        private const float MaxPartSize = 4f;
        private const float NearPlaneDepth = 0.999f;

        private static readonly Vector2[] RimDirections =
        {
            new Vector2(1f, 0f), new Vector2(-1f, 0f), new Vector2(0f, 1f), new Vector2(0f, -1f),
            new Vector2(0.7071f, 0.7071f), new Vector2(-0.7071f, 0.7071f),
            new Vector2(0.7071f, -0.7071f), new Vector2(-0.7071f, -0.7071f),
        };

        private sealed class Tracked
        {
            public Color Color;
            public readonly List<Renderer> Parts = new List<Renderer>();
            public readonly List<int> SubMeshes = new List<int>();
        }

        private readonly List<Tracked> tracked = new List<Tracked>();
        private readonly HashSet<GameObject> seen = new HashSet<GameObject>();
        private readonly Dictionary<Color, Material> fillMaterials = new Dictionary<Color, Material>();
        private readonly Dictionary<Color, Material> rimMaterials = new Dictionary<Color, Material>();

        private bool shown;
        private bool shaderMissing;
        private float nextScan;
        private Shader shader;
        private Camera overlay;
        private Camera source;
        private CommandBuffer buffer;

        private void OnEnable()
        {
            Camera.onPreCull += OnCameraPreCull;
        }

        private void OnDisable()
        {
            Camera.onPreCull -= OnCameraPreCull;
        }

        private void Update()
        {
            ReadInput();

            Camera main = shown && !PlayerData.IsMultiplayer && EnsureOverlay() ? Camera.main : null;
            if (main == null)
            {
                SetOverlayActive(false);
                return;
            }

            if (Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + RescanSeconds;
                Rescan();
            }

            if (tracked.Count == 0)
            {
                SetOverlayActive(false);
                return;
            }

            source = main;
            // Camera order is fixed before any camera culls, so it has to be set here.
            overlay.depth = main.depth + 0.5f;
            SetOverlayActive(true);
        }

        // Runs after every LateUpdate, so the game camera has already moved for this frame. The clone
        // renderers are read when the buffer executes, so animation is never a frame behind either.
        private void OnCameraPreCull(Camera camera)
        {
            if (camera != overlay || source == null)
            {
                return;
            }

            float depth = overlay.depth;
            overlay.CopyFrom(source);
            overlay.depth = depth;
            overlay.cullingMask = 0;
            overlay.clearFlags = CameraClearFlags.Depth;
            overlay.renderingPath = RenderingPath.Forward;
            overlay.allowHDR = false;
            overlay.allowMSAA = false;
            overlay.useOcclusionCulling = false;

            buffer.Clear();
            Matrix4x4 view = overlay.worldToCameraMatrix;
            Matrix4x4 projection = overlay.projectionMatrix;

            buffer.SetViewProjectionMatrices(view, PinToNearPlane(projection));
            foreach (Tracked clone in tracked)
            {
                Draw(clone, FillMaterial(clone.Color));
            }

            float pixels = Plugin.OutlinePixels.Value * overlay.pixelHeight / 1080f;
            float stepX = 2f * pixels / Mathf.Max(1, overlay.pixelWidth);
            float stepY = 2f * pixels / Mathf.Max(1, overlay.pixelHeight);
            foreach (Vector2 direction in RimDirections)
            {
                buffer.SetViewProjectionMatrices(view,
                    PinToNearPlane(ShiftOnScreen(projection, direction.x * stepX, direction.y * stepY)));
                foreach (Tracked clone in tracked)
                {
                    Draw(clone, RimMaterial(clone.Color));
                }
            }
        }

        private void ReadInput()
        {
            Keyboard keyboard = Keyboard.current;
            Key key = Plugin.ToggleKey.Value;
            if (keyboard == null || key == Key.None)
            {
                return;
            }

            var control = keyboard[key];
            if (Plugin.HoldToShow.Value)
            {
                shown = control.isPressed;
            }
            else if (control.wasPressedThisFrame)
            {
                shown = !shown;
                nextScan = 0f;
                Plugin.Log.LogInfo(shown ? "Clone outline on." : "Clone outline off.");
            }
        }

        private void Rescan()
        {
            tracked.Clear();
            seen.Clear();

            WorldAIManager world = WorldAIManager.instance;
            if (world != null && world.Target != null && !world.Target.IsDead())
            {
                Track(world.Target.gameObject, Plugin.CloneColor.Value);
            }

            foreach (AgentAI agent in FindObjectsByType<AgentAI>(FindObjectsSortMode.None))
            {
                if (agent.IsDead())
                {
                    continue;
                }

                if (agent.IsTarget)
                {
                    Track(agent.gameObject, Plugin.CloneColor.Value);
                }
                else if (Plugin.ShowOtherClones.Value && agent.MyJob != null && agent.MyJob.JobName == "OtherClone")
                {
                    Track(agent.gameObject, Plugin.OtherCloneColor.Value);
                }
            }

            // When the clone dies it can burst into a tentacle monster that keeps the target role.
            foreach (TentacleMonster monster in FindObjectsByType<TentacleMonster>(FindObjectsSortMode.None))
            {
                if (monster.IsTarget && !monster.IsDead())
                {
                    Track(monster.gameObject, Plugin.CloneColor.Value);
                }
            }

            // Story chapter 5 places its clone by hand rather than through WorldAIManager.
            ML5Manager chapter5 = FindAnyObjectByType<ML5Manager>();
            if (chapter5 != null && chapter5.Clone != null && !chapter5.Clone.IsDead())
            {
                Track(chapter5.Clone.gameObject, Plugin.CloneColor.Value);
            }
        }

        private void Track(GameObject root, Color color)
        {
            if (!root.activeInHierarchy || !seen.Add(root))
            {
                return;
            }

            var clone = new Tracked { Color = color };
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
            {
                Mesh mesh = MeshOf(renderer);
                // Skip line/particle renderers and oversized helpers such as aim quads or effect volumes.
                if (mesh == null || renderer.bounds.size.magnitude > MaxPartSize)
                {
                    continue;
                }

                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    clone.Parts.Add(renderer);
                    clone.SubMeshes.Add(sub);
                }
            }

            if (clone.Parts.Count > 0)
            {
                tracked.Add(clone);
            }
        }

        private static Mesh MeshOf(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinned)
            {
                return skinned.sharedMesh;
            }

            if (renderer is MeshRenderer)
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                return filter != null ? filter.sharedMesh : null;
            }

            return null;
        }

        private void Draw(Tracked clone, Material material)
        {
            for (int i = 0; i < clone.Parts.Count; i++)
            {
                Renderer part = clone.Parts[i];
                if (part != null && part.enabled && part.gameObject.activeInHierarchy)
                {
                    buffer.DrawRenderer(part, material, clone.SubMeshes[i], 0);
                }
            }
        }

        // Moves geometry by a fixed number of pixels: adding k*w to clip-space x is k/2 of the screen in NDC.
        private static Matrix4x4 ShiftOnScreen(Matrix4x4 projection, float ndcX, float ndcY)
        {
            Vector4 w = projection.GetRow(3);
            projection.SetRow(0, projection.GetRow(0) + w * ndcX);
            projection.SetRow(1, projection.GetRow(1) + w * ndcY);
            return projection;
        }

        // Clip z = -0.999w puts every vertex just inside the near plane, in front of any wall.
        private static Matrix4x4 PinToNearPlane(Matrix4x4 projection)
        {
            projection.SetRow(2, projection.GetRow(3) * -NearPlaneDepth);
            return projection;
        }

        private Material FillMaterial(Color color)
        {
            return MaterialFor(fillMaterials, color, Plugin.FillOpacity.Value);
        }

        private Material RimMaterial(Color color)
        {
            return MaterialFor(rimMaterials, color, color.a);
        }

        private Material MaterialFor(Dictionary<Color, Material> cache, Color color, float alpha)
        {
            if (!cache.TryGetValue(color, out Material material))
            {
                material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                material.SetInt("_Cull", (int)CullMode.Off);
                material.SetInt("_ZWrite", 1);
                material.SetInt("_ZTest", (int)CompareFunction.Less);
                material.SetFloat("_ZBias", 0f);
                cache[color] = material;
            }

            // Fill opacity is live-editable in the config, so set the colour every time.
            material.SetColor("_Color", new Color(color.r, color.g, color.b, alpha));
            return material;
        }

        private bool EnsureOverlay()
        {
            if (shaderMissing)
            {
                return false;
            }

            if (shader == null)
            {
                // Built into every Unity player; it exposes blend, cull and depth state as properties.
                shader = Shader.Find("Hidden/Internal-Colored");
                if (shader == null)
                {
                    shaderMissing = true;
                    Plugin.Log.LogError("Shader Hidden/Internal-Colored not found; cannot draw the outline.");
                    return false;
                }
            }

            if (overlay == null)
            {
                var go = new GameObject("CloneSight Overlay");
                DontDestroyOnLoad(go);
                go.hideFlags = HideFlags.HideAndDontSave;
                overlay = go.AddComponent<Camera>();
                overlay.enabled = false;
                buffer = new CommandBuffer { name = "CloneSight" };
                overlay.AddCommandBuffer(CameraEvent.AfterForwardAlpha, buffer);
            }

            return true;
        }

        private void SetOverlayActive(bool active)
        {
            if (!active)
            {
                source = null;
            }

            if (overlay != null && overlay.enabled != active)
            {
                overlay.enabled = active;
                if (!active)
                {
                    buffer.Clear();
                }
            }
        }
    }
}
