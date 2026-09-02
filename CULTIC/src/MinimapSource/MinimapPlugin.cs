using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace CulticMinimap
{
    /// <summary>
    /// "CULTIC Minimap" - a corner minimap built on the game's own automap.
    ///
    /// The game ships a full automap system (scrAutomap): level geometry baked into
    /// a vertex-colored mesh (automapMesh), objective/door/secret markers spawned as
    /// world objects, and a dedicated automapCamera. Normally you only see it on the
    /// full-screen map screen. This mod adds our own tiny ORTHOGRAPHIC camera that
    /// hovers above the local player looking straight down, renders ONLY the automap
    /// layers into a RenderTexture, and draws that texture as a minimap panel.
    ///
    /// No hidden information:
    ///  - On every scene load (and whenever the game rebuilds the map mesh, e.g. the
    ///    moving train level) we call scrAutomap.revealAll() + updatePalette(), which
    ///    fills the whole map with the explored color and marks all mapData revealed.
    ///    The game's own per-frame exploration tinting still runs harmlessly on top.
    ///  - updateObjectives() is called so objective / door / secret markers exist.
    ///
    /// Blips are drawn on top of the rendered map in OnGUI from cached component
    /// lists (refreshed every BlipRefreshInterval seconds):
    ///  - red dots   : living enemies (scrEnemy with hp > 0); bosses get bigger dots
    ///  - cyan dots  : pickups (scrPickup, active in hierarchy only)
    ///  - gold dots  : key items (pickups with a non-empty objectiveID)
    ///  - white dot  : the local player (panel center)
    /// Off-panel blips are clamped to the panel edge so you still get direction.
    ///
    /// The patch runs purely additive: we never touch gameplay state, timeScale or
    /// the real automap camera, so opening the actual map screen behaves normally.
    /// </summary>
    [BepInPlugin("local.codex.culticminimap", "CULTIC Minimap", "1.4.0")]
    public sealed class MinimapPlugin : BaseUnityPlugin
    {
        private const float BlipClampPad = 0.04f;
        private const float LearnedCellSize = 0.9f;
        private const float LearnedSampleDistanceSqr = 0.2025f;
        private const float CoverageHeightTolerance = 1.25f;
        private const float LearnedAutosaveDelay = 10f;
        private const int LearnedMapMagic = 0x314D4D43; // "CMM1"
        private const int LearnedMapVersion = 1;
        private const string AutomapMaskProperty = "_Mask_Size";

        private static readonly Color EnemyColor = new Color(0.95f, 0.15f, 0.10f);
        private static readonly Color BossColor = new Color(0.75f, 0.05f, 0.30f);
        private static readonly Color ItemColor = new Color(0.20f, 0.85f, 0.95f);
        private static readonly Color KeyItemColor = new Color(1f, 0.80f, 0.15f);
        private static readonly Color PlayerColor = new Color(1f, 1f, 1f);
        private static readonly Color PanelBg = new Color(0.02f, 0.02f, 0.03f, 0.85f);

        private static MinimapPlugin instance;

        private ConfigEntry<bool> enabledOnStart;
        private ConfigEntry<KeyboardShortcut> toggleKey;
        private ConfigEntry<KeyboardShortcut> fullMapKey;
        private ConfigEntry<KeyboardShortcut> escapeKey;
        private ConfigEntry<float> fullMapScreenShare;
        private ConfigEntry<int> panelSize;
        private ConfigEntry<int> panelMargin;
        private ConfigEntry<float> viewSize;
        private ConfigEntry<float> cameraHeight;
        private ConfigEntry<bool> rotateWithPlayer;
        private ConfigEntry<bool> revealFullMap;
        private ConfigEntry<bool> showEnemies;
        private ConfigEntry<bool> showItems;
        private ConfigEntry<bool> showKeyItems;
        private ConfigEntry<bool> nativeShowEnemies;
        private ConfigEntry<bool> nativeShowItems;
        private ConfigEntry<bool> nativeShowKeyItems;
        private ConfigEntry<float> blipRefreshInterval;

        private bool shown;
        private string statusText = "";
        private float statusUntil;
        private Harmony harmony;

        // Our own render rig.
        private Camera minimapCam;
        private RenderTexture minimapRT;

        // Automap tracking (per scene).
        private scrAutomap automap;
        private float nextAutomapSearch;
        private bool revealedForThisScene;
        private static bool pendingReveal;
        private bool maskDirty = true;
        private float nextMaskCheck;

        // The native map remains the base. Grounded player positions outside its
        // triangles become persistent per-scene cells, loaded on future runs and
        // optionally bundled into distribution packages.
        private Vector3[] coverageVerts;
        private int[] coverageTriangles;
        private Transform coverageTransform;
        private GameObject learnedMapObject;
        private Mesh learnedMapMesh;
        private MeshRenderer learnedMapRenderer;
        private Material learnedMapMaterial;
        private readonly List<Vector3> learnedVerts = new List<Vector3>();
        private readonly List<int> learnedTriangles = new List<int>();
        private readonly List<Color> learnedColors = new List<Color>();
        private readonly Dictionary<CollisionCell, float> learnedCells =
            new Dictionary<CollisionCell, float>();
        private Vector3 lastLearnedSample;
        private bool haveLearnedSample;
        private bool learnedMapDirty;
        private float learnedMapSaveTime;
        private string learnedSceneName = "";

        // Blip caches.
        private List<scrEnemy> enemyCache = new List<scrEnemy>();
        private List<scrPickup> pickupCache = new List<scrPickup>();
        private float nextBlipRefresh;

        // Native tumble-map markers. These live exclusively on the automap's
        // render layer, so they cannot leak into the normal gameplay camera.
        private GameObject nativeMarkerRoot;
        private Mesh nativeMarkerMesh;
        private Material nativeEnemyMaterial;
        private Material nativeBossMaterial;
        private Material nativeItemMaterial;
        private Material nativeKeyItemMaterial;
        private readonly Dictionary<int, GameObject> nativeEnemyMarkers =
            new Dictionary<int, GameObject>();
        private readonly Dictionary<int, GameObject> nativePickupMarkers =
            new Dictionary<int, GameObject>();
        private readonly HashSet<int> liveMarkerIds = new HashSet<int>();

        // World->panel projection inputs (set while drawing).
        private Vector3 playerPos;
        private Vector3 viewCenter;      // camera focus point (projection origin)
        private float camYawRad;
        private float invViewSpan;
        private bool havePlayer;

        // Full-map cycling (M key): minimap -> 2D overlay -> native game map.
        private const int StageMinimap = 0;
        private const int StageFull2D = 1;
        private const int StageGameMap = 2;
        private int mapStage;
        private float menuReopenGuard;   // keep Menu (ESC) suppressed briefly after closing
        private RenderTexture fullMapRT;
        private UnityEngine.InputSystem.InputAction nativeMapAction;
        private UnityEngine.InputSystem.InputAction nativeMenuAction;

        private struct CollisionCell : IEquatable<CollisionCell>
        {
            public int x;
            public int y;
            public int z;

            public CollisionCell(int cellX, int cellY, int cellZ)
            {
                x = cellX;
                y = cellY;
                z = cellZ;
            }

            public bool Equals(CollisionCell other)
            {
                return x == other.x && y == other.y && z == other.z;
            }

            public override bool Equals(object obj)
            {
                return obj is CollisionCell && Equals((CollisionCell)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = x;
                    hash = (hash * 397) ^ y;
                    return (hash * 397) ^ z;
                }
            }
        }

        private void Awake()
        {
            instance = this;

            enabledOnStart = Config.Bind("General", "EnabledOnStart", true,
                "Whether the minimap is visible when the game starts.");
            toggleKey = Config.Bind("Hotkeys", "ToggleMinimap",
                new KeyboardShortcut(KeyCode.F8),
                "Show/hide the minimap panel.");
            fullMapKey = Config.Bind("Hotkeys", "ToggleFullMap",
                new KeyboardShortcut(KeyCode.M),
                "Cycle: minimap -> 2D full map -> game map -> minimap.");
            escapeKey = Config.Bind("Hotkeys", "BackToMinimap",
                new KeyboardShortcut(KeyCode.Escape),
                "Close any open map overlay and return to the minimap.");
            fullMapScreenShare = Config.Bind("FullMap", "ScreenShare", 0.85f,
                new ConfigDescription(
                    "Full-map panel edge length as a fraction of the smaller screen dimension.",
                    new AcceptableValueRange<float>(0.4f, 1f)));
            panelSize = Config.Bind("Panel", "SizePx", 280,
                new ConfigDescription("Minimap panel edge length in pixels.",
                    new AcceptableValueRange<int>(120, 600)));
            panelMargin = Config.Bind("Panel", "MarginPx", 16,
                new ConfigDescription("Distance from the bottom-right screen edges.",
                    new AcceptableValueRange<int>(0, 200)));
            viewSize = Config.Bind("Camera", "ViewWidth", 34f,
                new ConfigDescription("World units visible across the minimap (zoom).",
                    new AcceptableValueRange<float>(8f, 150f)));
            cameraHeight = Config.Bind("Camera", "Height", 160f,
                new ConfigDescription("How high above the player the minimap camera sits.",
                    new AcceptableValueRange<float>(60f, 1000f)));
            rotateWithPlayer = Config.Bind("Camera", "RotateWithPlayer", false,
                "Rotate the map so up is always the player's facing (Doom-style).");
            revealFullMap = Config.Bind("Reveal", "RevealFullMap", true,
                "No fog of war: mark the entire automap explored on every level load.");
            showEnemies = Config.Bind("Blips", "ShowEnemies", true,
                "Draw red dots for living enemies.");
            showItems = Config.Bind("Blips", "ShowItems", true,
                "Draw cyan dots for pickups.");
            showKeyItems = Config.Bind("Blips", "ShowKeyItems", true,
                "Draw gold dots for key-item pickups (objective items).");
            nativeShowEnemies = Config.Bind("3D Map Blips", "ShowEnemies", true,
                "Show living enemies on the game's native rotatable 3D map.");
            nativeShowItems = Config.Bind("3D Map Blips", "ShowItems", true,
                "Show ordinary pickups on the game's native rotatable 3D map.");
            nativeShowKeyItems = Config.Bind("3D Map Blips", "ShowKeyItems", true,
                "Show key/objective pickups on the game's native rotatable 3D map.");
            blipRefreshInterval = Config.Bind("Blips", "RefreshInterval", 0.75f,
                new ConfigDescription("Seconds between rescans for enemies/pickups.",
                    new AcceptableValueRange<float>(0.25f, 3f)));

            shown = enabledOnStart.Value;

            CreateRenderRig();

            harmony = new Harmony("local.codex.culticminimap");
            harmony.PatchAll();
            Logger.LogInfo("CULTIC Minimap loaded. " + toggleKey.Value + " toggles." +
                (revealFullMap.Value ? " Full map reveal on." : ""));
        }

        private void OnDestroy()
        {
            shown = false;
            mapStage = StageMinimap;
            ApplyInputSuppression(); // hand M and ESC back to the game
            if (harmony != null)
            {
                harmony.UnpatchSelf();
            }
            SaveLearnedMap();
            ResetLearnedMapGeometry();
            ResetNativeMapMarkers(true);
            if (minimapRT != null)
            {
                minimapRT.Release();
                Destroy(minimapRT);
            }
            if (fullMapRT != null)
            {
                fullMapRT.Release();
                Destroy(fullMapRT);
            }
            if (minimapCam != null)
            {
                Destroy(minimapCam.gameObject);
            }
            instance = null;
        }

        private void OnApplicationQuit()
        {
            SaveLearnedMap();
        }

        private void CreateRenderRig()
        {
            int rtSize = 1024;
            minimapRT = new RenderTexture(rtSize, rtSize, 24);
            minimapRT.name = "MinimapRT";
            fullMapRT = new RenderTexture(2048, 2048, 24);
            fullMapRT.name = "MinimapFullRT";

            GameObject go = new GameObject("MinimapCamera");
            UnityEngine.Object.DontDestroyOnLoad(go);
            minimapCam = go.AddComponent<Camera>();
            minimapCam.orthographic = true;
            minimapCam.orthographicSize = viewSize.Value * 0.5f;
            minimapCam.nearClipPlane = 0.1f;
            minimapCam.farClipPlane = cameraHeight.Value * 2.5f;
            minimapCam.clearFlags = CameraClearFlags.SolidColor;
            minimapCam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            minimapCam.cullingMask = 0; // nothing until an automap is found
            minimapCam.targetTexture = minimapRT;
            minimapCam.enabled = false; // we render manually each frame
        }

        private void Update()
        {
            if (toggleKey.Value.IsDown())
            {
                shown = !shown;
                if (!shown)
                {
                    if (mapStage == StageGameMap)
                    {
                        CloseNativeMap();
                    }
                    mapStage = StageMinimap;
                }
                ShowStatus(shown ? "ON" : "OFF");
            }

            if (menuReopenGuard > 0f)
            {
                menuReopenGuard -= Time.unscaledDeltaTime;
            }

            // While our panel is visible we own the game's Map key (M never
            // triggers the native map on its own) and suppress the Menu action
            // while an overlay is up, so ESC closes OUR overlay instead of
            // also opening the pause menu.
            ApplyInputSuppression();

            // Learning and periodic persistence continue even while the panel is
            // hidden, so an omitted passage is not missed because F8 was off.
            LocateAutomap();
            if (pendingReveal && automap != null)
            {
                DoReveal();
                pendingReveal = false;
            }
            TrackGroundedPlayer();
            if (learnedMapDirty && Time.unscaledTime >= learnedMapSaveTime)
            {
                SaveLearnedMap();
            }

            if (!shown)
            {
                SetNativeMarkerVisibility(false);
                return;
            }

            SyncStageWithNativeMap();

            if (fullMapKey.Value.IsDown())
            {
                if (mapStage == StageMinimap)
                {
                    mapStage = StageFull2D;             // 1st M: 2D full-map overlay
                }
                else if (mapStage == StageFull2D)
                {
                    mapStage = StageGameMap;            // 2nd M: native game map
                    OpenNativeMap();
                }
                else
                {
                    CloseNativeMap();                   // 3rd M: back to minimap
                    mapStage = StageMinimap;
                    menuReopenGuard = 0.15f;
                }
            }

            if (escapeKey.Value.IsDown() && mapStage != StageMinimap)
            {
                if (mapStage == StageGameMap)
                {
                    CloseNativeMap();
                }
                mapStage = StageMinimap;
                menuReopenGuard = 0.15f;
            }

            RefreshBlipCaches();
            SyncNativeMapMarkers();

            havePlayer = RenderCurrentView();
        }

        // ------------------------------------------------------------------
        // Native map-key ownership + stage syncing.
        // ------------------------------------------------------------------

        private void CacheNativeActions()
        {
            if (nativeMapAction != null)
            {
                return;
            }

            scrGameControl game = scrGameControl.Instance;
            if (game == null || game.playerControls == null)
            {
                return;
            }

            nativeMapAction = game.playerControls.FindAction("Map", false);
            nativeMenuAction = game.playerControls.FindAction("Menu", false);
        }

        private void ApplyInputSuppression()
        {
            CacheNativeActions();

            if (!shown)
            {
                SetActionEnabled(nativeMapAction, true);
                SetActionEnabled(nativeMenuAction, true);
                return;
            }

            // We own M entirely while the panel is visible.
            SetActionEnabled(nativeMapAction, false);
            // ESC must not open the pause menu while an overlay is up; keep it
            // suppressed for a moment after returning to the minimap so the
            // ESC that closed the overlay cannot also open the pause menu.
            SetActionEnabled(nativeMenuAction,
                mapStage == StageMinimap && menuReopenGuard <= 0f);
        }

        private static void SetActionEnabled(
            UnityEngine.InputSystem.InputAction action, bool enable)
        {
            if (action == null)
            {
                return;
            }

            try
            {
                if (enable && !action.enabled)
                {
                    action.Enable();
                }
                else if (!enable && action.enabled)
                {
                    action.Disable();
                }
            }
            catch
            {
            }
        }

        private void SyncStageWithNativeMap()
        {
            if (automap == null)
            {
                return;
            }

            if (mapStage == StageGameMap &&
                automap.state == scrAutomap.AutomapState.Hidden)
            {
                // Something else closed it (cutscene, level end) - fall back.
                mapStage = StageMinimap;
                menuReopenGuard = 0.15f;
            }
            else if (mapStage != StageGameMap &&
                automap.state != scrAutomap.AutomapState.Hidden)
            {
                // Opened by something else - treat it as the game-map stage.
                mapStage = StageGameMap;
            }
        }

        private void OpenNativeMap()
        {
            try
            {
                if (automap != null)
                {
                    automap.openAutomap();
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning("openAutomap failed: " + ex.Message);
                mapStage = StageFull2D; // fall back to the 2D overlay
            }
        }

        private void CloseNativeMap()
        {
            try
            {
                if (automap != null &&
                    automap.state != scrAutomap.AutomapState.Hidden)
                {
                    automap.closeAutomap();
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning("closeAutomap failed: " + ex.Message);
            }
        }

        private void ShowStatus(string text)
        {
            statusText = "[Minimap] " + text;
            statusUntil = Time.unscaledTime + 2f;
            Logger.LogInfo(text);
        }

        // ------------------------------------------------------------------
        // Automap discovery + full reveal.
        // ------------------------------------------------------------------

        private void LocateAutomap()
        {
            if (automap != null)
            {
                return;
            }

            if (Time.unscaledTime < nextAutomapSearch)
            {
                return;
            }

            nextAutomapSearch = Time.unscaledTime + 1f;
            scrAutomap found = UnityEngine.Object.FindObjectOfType<scrAutomap>();
            if (found != null)
            {
                SaveLearnedMap();
                ResetLearnedMapGeometry();
                ResetNativeMapMarkers(false);
                automap = found;
                revealedForThisScene = false;
                maskDirty = true;
                Logger.LogInfo("Found automap for scene; revealing.");
                DoReveal();
                LoadLearnedMapForCurrentScene();
            }
        }

        private void DoReveal()
        {
            if (automap == null)
            {
                return;
            }

            try
            {
                if (revealFullMap.Value)
                {
                    automap.revealAll();
                    automap.updatePalette(GetMapPaletteIndex());
                    Mesh mapMesh = automap.automapMesh != null ? automap.automapMesh.sharedMesh : null;
                    if (mapMesh != null && automap.colors != null &&
                        automap.colors.Length == mapMesh.vertexCount)
                    {
                        mapMesh.colors = automap.colors;
                    }
                }

                automap.updateObjectives();
                revealedForThisScene = true;
                maskDirty = true;
                CacheCoverageMesh();
                RefreshLearnedMapColors();
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Reveal failed: " + ex.Message);
            }
        }

        /// <summary>The map palette the player picked in the menu (prefsList["mapPalette"]).</summary>
        private static int GetMapPaletteIndex()
        {
            scrGameControl game = scrGameControl.Instance;
            if (game != null && game.prefsList != null && game.prefsList.ContainsKey("mapPalette"))
            {
                try
                {
                    return (int)game.prefsList["mapPalette"];
                }
                catch
                {
                }
            }

            return 0;
        }

        private void EnsureMask()
        {
            if (!maskDirty && Time.unscaledTime < nextMaskCheck)
            {
                return;
            }

            nextMaskCheck = Time.unscaledTime + 5f; // periodic: pick up new markers
            if (automap == null || !revealedForThisScene)
            {
                return;
            }

            maskDirty = false;
            int mask = 0;
            mask |= AddRendererLayers(automap.automapMeshRenderer);
            mask |= AddRendererLayers(automap.automapBackingMeshRenderer);
            if (automap.gridPlane != null)
            {
                mask |= LayerBit(automap.gridPlane.gameObject.layer);
            }
            if (automap.objectiveObjects != null)
            {
                foreach (GameObject go in automap.objectiveObjects)
                {
                    mask |= AddTreeLayers(go);
                }
            }
            if (automap.secretObjects != null)
            {
                foreach (GameObject go in automap.secretObjects)
                {
                    mask |= AddTreeLayers(go);
                }
            }
            if (automap.doorObjects != null)
            {
                foreach (GameObject go in automap.doorObjects)
                {
                    mask |= AddTreeLayers(go);
                }
            }

            if (mask != 0)
            {
                minimapCam.cullingMask = mask;
                // Make sure the map geometry itself is awake; the main camera never
                // renders these layers, so this cannot leak visuals into gameplay.
                SafeActivate(automap.automapMeshRenderer);
                SafeActivate(automap.automapBackingMeshRenderer);
            }
            else
            {
                Logger.LogWarning("Automap found but produced no layers; minimap map disabled.");
            }
        }

        private static void SafeActivate(Renderer r)
        {
            if (r != null && !r.gameObject.activeSelf)
            {
                r.gameObject.SetActive(true);
            }
        }

        private static int LayerBit(int layer)
        {
            return layer >= 0 && layer <= 31 ? 1 << layer : 0;
        }

        private static int AddRendererLayers(Renderer r)
        {
            return r != null ? LayerBit(r.gameObject.layer) : 0;
        }

        private static int AddTreeLayers(GameObject root)
        {
            int mask = 0;
            if (root == null)
            {
                return mask;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length && i < 64; i++)
            {
                mask |= LayerBit(renderers[i].gameObject.layer);
            }
            if (renderers.Length == 0)
            {
                mask |= LayerBit(root.layer);
            }
            return mask;
        }

        // ------------------------------------------------------------------
        // Camera placement + render for the two views (corner minimap / full map).
        // ------------------------------------------------------------------

        private bool RenderCurrentView()
        {
            if (mapStage == StageGameMap)
            {
                return false; // native map is on screen; draw nothing of ours
            }

            scrPlayerControl player = FindLocalPlayerControl();
            if (player == null || player.isDead || automap == null)
            {
                return false;
            }

            EnsureMask();
            if (minimapCam.cullingMask == 0)
            {
                return false;
            }

            playerPos = player.transform.position;

            bool wantFullMap = mapStage == StageFull2D && fullMapRT != null;
            bool ok = wantFullMap ? ConfigureFullMapCamera() : ConfigureMinimapCamera(player);
            if (!ok)
            {
                return false;
            }

            SyncLearnedMapMaterial();
            UpdateMapMaterialProperties(playerPos);
            minimapCam.targetTexture = wantFullMap ? fullMapRT : minimapRT;
            // The native automap shader normally punches a screen-centered
            // peephole using _Mask_Size. That is useful in its 3D tumble view,
            // but leaves a large empty center in our flat RenderTexture. Disable
            // it only for this immediate render, then restore native state.
            Material mapMaterial = automap.automapMeshRenderer != null ?
                automap.automapMeshRenderer.sharedMaterial : null;
            Material backingMaterial = automap.automapBackingMeshRenderer != null ?
                automap.automapBackingMeshRenderer.sharedMaterial : null;
            bool restoreMapMask = mapMaterial != null &&
                mapMaterial.HasProperty(AutomapMaskProperty);
            bool restoreBackingMask = backingMaterial != null &&
                backingMaterial.HasProperty(AutomapMaskProperty);
            Vector4 mapMask = restoreMapMask ?
                mapMaterial.GetVector(AutomapMaskProperty) : Vector4.zero;
            Vector4 backingMask = restoreBackingMask ?
                backingMaterial.GetVector(AutomapMaskProperty) : Vector4.zero;
            try
            {
                if (restoreMapMask) mapMaterial.SetVector(AutomapMaskProperty, Vector4.zero);
                if (restoreBackingMask)
                    backingMaterial.SetVector(AutomapMaskProperty, Vector4.zero);
                minimapCam.Render();
            }
            finally
            {
                if (restoreMapMask) mapMaterial.SetVector(AutomapMaskProperty, mapMask);
                if (restoreBackingMask)
                    backingMaterial.SetVector(AutomapMaskProperty, backingMask);
            }
            return true;
        }

        private void CacheCoverageMesh()
        {
            coverageVerts = null;
            coverageTriangles = null;
            coverageTransform = null;
            try
            {
                if (automap == null || automap.automapMesh == null ||
                    automap.automapMesh.sharedMesh == null)
                {
                    return;
                }
                Mesh source = automap.automapMesh.sharedMesh;
                coverageVerts = source.vertices;
                coverageTriangles = source.triangles;
                coverageTransform = automap.automapMesh.transform;
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Could not cache automap coverage: " + ex.Message);
            }
        }

        private void TrackGroundedPlayer()
        {
            if (automap == null || coverageVerts == null || coverageTriangles == null ||
                coverageTransform == null)
            {
                return;
            }
            scrPlayerControl player = FindLocalPlayerControl();
            if (player == null || player.isDead || !player.isGrounded)
            {
                return;
            }

            Vector3 position = player.transform.position;
            if (haveLearnedSample)
            {
                Vector3 delta = position - lastLearnedSample;
                if (delta.x * delta.x + delta.z * delta.z < LearnedSampleDistanceSqr &&
                    Mathf.Abs(delta.y) < 0.25f)
                {
                    return;
                }
            }
            lastLearnedSample = position;
            haveLearnedSample = true;

            float floorY;
            if (!TryGetStandingFloor(player, out floorY))
            {
                return;
            }
            Vector3 floorPoint = new Vector3(position.x, floorY, position.z);
            if (HasBakedMapCoverage(floorPoint))
            {
                return;
            }

            EnsureLearnedMapRenderer();
            int cellX = Mathf.RoundToInt(position.x / LearnedCellSize);
            int cellY = Mathf.RoundToInt(floorY / 0.5f);
            int cellZ = Mathf.RoundToInt(position.z / LearnedCellSize);
            CollisionCell key = new CollisionCell(cellX, cellY, cellZ);
            if (learnedCells.ContainsKey(key))
            {
                return;
            }

            learnedCells.Add(key, floorY);
            AppendLearnedCellGeometry(key, floorY);
            ApplyLearnedMapMesh();
            learnedMapDirty = true;
            learnedMapSaveTime = Time.unscaledTime + LearnedAutosaveDelay;
        }

        private static bool TryGetStandingFloor(scrPlayerControl player, out float floorY)
        {
            Collider playerCollider = player.GetComponent<Collider>();
            floorY = playerCollider != null ? playerCollider.bounds.min.y : player.transform.position.y;
            int mask = ~LayerBit(player.gameObject.layer);
            mask &= ~LayerBit(29);
            RaycastHit hit;
            Vector3 origin = new Vector3(player.transform.position.x, floorY + 0.6f,
                player.transform.position.z);
            if (Physics.Raycast(origin, Vector3.down, out hit, 1.25f, mask,
                QueryTriggerInteraction.Ignore))
            {
                // Do not persist a world-space patch for a moving platform.
                if (hit.collider != null && hit.collider.attachedRigidbody != null)
                {
                    return false;
                }
                if (hit.normal.y > 0.35f)
                {
                    floorY = hit.point.y;
                }
            }
            return true;
        }

        private bool HasBakedMapCoverage(Vector3 worldPosition)
        {
            if (coverageVerts == null || coverageTriangles == null ||
                coverageTransform == null || coverageTriangles.Length < 3)
            {
                return false;
            }

            Vector3 p = coverageTransform.InverseTransformPoint(worldPosition);
            for (int i = 0; i + 2 < coverageTriangles.Length; i += 3)
            {
                Vector3 a = coverageVerts[coverageTriangles[i]];
                Vector3 b = coverageVerts[coverageTriangles[i + 1]];
                Vector3 c = coverageVerts[coverageTriangles[i + 2]];
                float denominator = (b.z - c.z) * (a.x - c.x) +
                    (c.x - b.x) * (a.z - c.z);
                if (Mathf.Abs(denominator) < 0.00001f)
                {
                    continue;
                }
                float wa = ((b.z - c.z) * (p.x - c.x) +
                    (c.x - b.x) * (p.z - c.z)) / denominator;
                float wb = ((c.z - a.z) * (p.x - c.x) +
                    (a.x - c.x) * (p.z - c.z)) / denominator;
                float wc = 1f - wa - wb;
                if (wa < -0.03f || wb < -0.03f || wc < -0.03f)
                {
                    continue;
                }
                float localY = a.y * wa + b.y * wb + c.y * wc;
                float surfaceY = coverageTransform.TransformPoint(
                    new Vector3(p.x, localY, p.z)).y;
                if (Mathf.Abs(surfaceY - worldPosition.y) <= CoverageHeightTolerance)
                {
                    return true;
                }
            }
            return false;
        }

        private void EnsureLearnedMapRenderer()
        {
            if (learnedMapObject != null || automap == null ||
                automap.automapMeshRenderer == null)
            {
                return;
            }
            learnedMapObject = new GameObject("MinimapLearnedMapPatches");
            learnedMapObject.layer = automap.automapMeshRenderer.gameObject.layer;
            learnedMapObject.transform.SetParent(automap.transform, false);
            MeshFilter filter = learnedMapObject.AddComponent<MeshFilter>();
            learnedMapRenderer = learnedMapObject.AddComponent<MeshRenderer>();
            // Learned cells are ordinary world-space floor patches, not vertices
            // produced by CULTIC's automap baker. The native automap shader can
            // discard such geometry based on its focal/height/mask inputs. Use a
            // dedicated unlit material so a recorded cell is always visible.
            learnedMapMaterial = CreateUnlitMaterial(automap.baseColor,
                "Minimap Learned Floor Material");
            learnedMapRenderer.sharedMaterial = learnedMapMaterial;
            learnedMapRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            learnedMapRenderer.receiveShadows = false;
            // The native automap camera uses occlusion culling, but its map
            // renderers explicitly opt out. Match that setting so our dynamic
            // patches are not hidden behind the actual level's baked occluders.
            learnedMapRenderer.allowOcclusionWhenDynamic = false;
            learnedMapMesh = new Mesh();
            learnedMapMesh.name = "MinimapLearnedMapPatchMesh";
            learnedMapMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            learnedMapMesh.MarkDynamic();
            filter.sharedMesh = learnedMapMesh;
        }

        private void AppendLearnedCellGeometry(CollisionCell key, float worldY)
        {
            if (learnedMapObject == null)
            {
                return;
            }
            float half = LearnedCellSize * 0.5f;
            float worldX = key.x * LearnedCellSize;
            float worldZ = key.z * LearnedCellSize;
            float y = worldY + 0.03f;
            int start = learnedVerts.Count;
            Transform t = learnedMapObject.transform;
            learnedVerts.Add(t.InverseTransformPoint(new Vector3(worldX - half, y, worldZ - half)));
            learnedVerts.Add(t.InverseTransformPoint(new Vector3(worldX - half, y, worldZ + half)));
            learnedVerts.Add(t.InverseTransformPoint(new Vector3(worldX + half, y, worldZ + half)));
            learnedVerts.Add(t.InverseTransformPoint(new Vector3(worldX + half, y, worldZ - half)));
            learnedTriangles.Add(start);
            learnedTriangles.Add(start + 1);
            learnedTriangles.Add(start + 2);
            learnedTriangles.Add(start);
            learnedTriangles.Add(start + 2);
            learnedTriangles.Add(start + 3);
            // The native map can be tumbled above or below a floor. Supply the
            // reverse winding as well so learned cells remain visible from both
            // sides instead of disappearing when viewed from underneath.
            learnedTriangles.Add(start + 2);
            learnedTriangles.Add(start + 1);
            learnedTriangles.Add(start);
            learnedTriangles.Add(start + 3);
            learnedTriangles.Add(start + 2);
            learnedTriangles.Add(start);
            Color color = automap != null ? automap.baseColor : Color.white;
            learnedColors.Add(color);
            learnedColors.Add(color);
            learnedColors.Add(color);
            learnedColors.Add(color);
        }

        private void RebuildLearnedMapGeometry()
        {
            learnedVerts.Clear();
            learnedTriangles.Clear();
            learnedColors.Clear();
            foreach (KeyValuePair<CollisionCell, float> pair in learnedCells)
            {
                AppendLearnedCellGeometry(pair.Key, pair.Value);
            }
            ApplyLearnedMapMesh();
        }

        private void ApplyLearnedMapMesh()
        {
            if (learnedMapMesh == null)
            {
                return;
            }
            learnedMapMesh.Clear();
            learnedMapMesh.SetVertices(learnedVerts);
            learnedMapMesh.SetTriangles(learnedTriangles, 0);
            learnedMapMesh.SetColors(learnedColors);
            learnedMapMesh.RecalculateBounds();
            maskDirty = true;
        }

        private void RefreshLearnedMapColors()
        {
            if (learnedColors.Count == 0 || automap == null)
            {
                return;
            }
            for (int i = 0; i < learnedColors.Count; i++)
            {
                learnedColors[i] = automap.baseColor;
            }
            if (learnedMapMaterial != null)
            {
                learnedMapMaterial.color = automap.baseColor;
            }
            ApplyLearnedMapMesh();
        }

        private void LoadLearnedMapForCurrentScene()
        {
            learnedSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            string fileName = GetLearnedMapFileName(learnedSceneName);
            string bundledPath = Path.Combine(Path.Combine(Paths.PluginPath,
                "CulticMinimapMaps"), fileName);
            string userPath = Path.Combine(GetLearnedMapDirectory(), fileName);
            int before = learnedCells.Count;
            LoadLearnedMapFile(bundledPath, learnedSceneName);
            LoadLearnedMapFile(userPath, learnedSceneName);
            if (learnedCells.Count > 0)
            {
                EnsureLearnedMapRenderer();
                RebuildLearnedMapGeometry();
            }
            learnedMapDirty = false;
            Logger.LogInfo("Learned map patches for " + learnedSceneName + ": " +
                learnedCells.Count + " cell(s) loaded (" +
                (learnedCells.Count - before) + " merged).");
        }

        private void LoadLearnedMapFile(string path, string expectedScene)
        {
            if (!File.Exists(path))
            {
                return;
            }
            try
            {
                Dictionary<CollisionCell, float> loadedCells =
                    new Dictionary<CollisionCell, float>();
                using (FileStream stream = File.OpenRead(path))
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    if (reader.ReadInt32() != LearnedMapMagic ||
                        reader.ReadInt32() != LearnedMapVersion)
                    {
                        throw new InvalidDataException("unsupported header/version");
                    }
                    float cellSize = reader.ReadSingle();
                    string sceneName = reader.ReadString();
                    int count = reader.ReadInt32();
                    if (Mathf.Abs(cellSize - LearnedCellSize) > 0.001f ||
                        sceneName != expectedScene || count < 0 || count > 500000)
                    {
                        throw new InvalidDataException("invalid scene, cell size, or count");
                    }
                    for (int i = 0; i < count; i++)
                    {
                        CollisionCell key = new CollisionCell(
                            reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
                        float y = reader.ReadSingle();
                        if (!float.IsNaN(y) && !float.IsInfinity(y))
                        {
                            loadedCells[key] = y;
                        }
                    }
                    if (stream.Position != stream.Length)
                    {
                        throw new InvalidDataException("unexpected trailing data");
                    }
                }
                foreach (KeyValuePair<CollisionCell, float> pair in loadedCells)
                {
                    learnedCells[pair.Key] = pair.Value;
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Could not load learned map " + path + ": " + ex.Message);
            }
        }

        private void SaveLearnedMap()
        {
            if (!learnedMapDirty || string.IsNullOrEmpty(learnedSceneName))
            {
                return;
            }
            string directory = GetLearnedMapDirectory();
            string path = Path.Combine(directory, GetLearnedMapFileName(learnedSceneName));
            string tempPath = path + ".tmp";
            string backupPath = path + ".bak";
            try
            {
                Directory.CreateDirectory(directory);
                List<CollisionCell> keys = new List<CollisionCell>(learnedCells.Keys);
                keys.Sort(delegate(CollisionCell a, CollisionCell b)
                {
                    int result = a.y.CompareTo(b.y);
                    if (result == 0) result = a.z.CompareTo(b.z);
                    if (result == 0) result = a.x.CompareTo(b.x);
                    return result;
                });
                using (FileStream stream = File.Create(tempPath))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(LearnedMapMagic);
                    writer.Write(LearnedMapVersion);
                    writer.Write(LearnedCellSize);
                    writer.Write(learnedSceneName);
                    writer.Write(keys.Count);
                    for (int i = 0; i < keys.Count; i++)
                    {
                        CollisionCell key = keys[i];
                        writer.Write(key.x);
                        writer.Write(key.y);
                        writer.Write(key.z);
                        writer.Write(learnedCells[key]);
                    }
                    stream.Flush(true);
                }
                if (File.Exists(path))
                {
                    try
                    {
                        File.Replace(tempPath, path, backupPath);
                        if (File.Exists(backupPath)) File.Delete(backupPath);
                    }
                    catch
                    {
                        File.Copy(tempPath, path, true);
                        File.Delete(tempPath);
                    }
                }
                else
                {
                    File.Move(tempPath, path);
                }
                learnedMapDirty = false;
                Logger.LogInfo("Saved " + learnedCells.Count + " learned map cell(s) for " +
                    learnedSceneName + ".");
            }
            catch (Exception ex)
            {
                learnedMapSaveTime = Time.unscaledTime + LearnedAutosaveDelay;
                Logger.LogWarning("Could not save learned map " + path + ": " + ex.Message);
            }
        }

        private static string GetLearnedMapDirectory()
        {
            return Path.Combine(Paths.ConfigPath, "CulticMinimapMaps");
        }

        private static string GetLearnedMapFileName(string sceneName)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalid.Length; i++)
            {
                sceneName = sceneName.Replace(invalid[i], '_');
            }
            return sceneName + ".cmm";
        }

        private void UpdateMapMaterialProperties(Vector3 focalPoint)
        {
            if (automap != null)
            {
                UpdateMapMaterial(automap.automapMeshRenderer != null ?
                    automap.automapMeshRenderer.sharedMaterial : null, focalPoint);
                UpdateMapMaterial(automap.automapBackingMeshRenderer != null ?
                    automap.automapBackingMeshRenderer.sharedMaterial : null, focalPoint);
            }
            if (learnedMapRenderer != null)
            {
                UpdateMapMaterial(learnedMapRenderer.sharedMaterial, focalPoint);
            }
        }

        private static void UpdateMapMaterial(Material material, Vector3 focalPoint)
        {
            if (material == null) return;
            if (material.HasProperty("_Player_Position"))
                material.SetVector("_Player_Position", focalPoint);
            if (material.HasProperty("_FocalPoint"))
                material.SetVector("_FocalPoint", focalPoint);
            if (material.HasProperty("_Player_Y"))
                material.SetFloat("_Player_Y", focalPoint.y);
            if (material.HasProperty("_UnscaledTime"))
                material.SetFloat("_UnscaledTime", Time.unscaledTime);
        }

        private void SyncLearnedMapMaterial()
        {
            if (learnedMapMaterial == null || automap == null)
            {
                return;
            }
            learnedMapMaterial.color = automap.baseColor;
        }

        private static void ClearAutomapCenterMask(MeshRenderer renderer)
        {
            Material material = renderer != null ? renderer.sharedMaterial : null;
            if (material != null && material.HasProperty(AutomapMaskProperty))
            {
                material.SetVector(AutomapMaskProperty, Vector4.zero);
            }
        }

        private static Material CreateUnlitMaterial(Color color, string materialName)
        {
            Shader shader = Shader.Find("Unlit/Color");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }
            if (shader == null)
            {
                return null;
            }
            Material material = new Material(shader);
            material.name = materialName;
            material.color = color;
            return material;
        }

        private void ResetLearnedMapGeometry()
        {
            coverageVerts = null;
            coverageTriangles = null;
            coverageTransform = null;
            learnedCells.Clear();
            learnedVerts.Clear();
            learnedTriangles.Clear();
            learnedColors.Clear();
            haveLearnedSample = false;
            learnedMapDirty = false;
            learnedSceneName = "";
            if (learnedMapObject != null) Destroy(learnedMapObject);
            if (learnedMapMesh != null) Destroy(learnedMapMesh);
            if (learnedMapMaterial != null) Destroy(learnedMapMaterial);
            learnedMapObject = null;
            learnedMapMesh = null;
            learnedMapRenderer = null;
            learnedMapMaterial = null;
        }

        private bool ConfigureMinimapCamera(scrPlayerControl player)
        {
            viewCenter = playerPos;
            float yaw = rotateWithPlayer.Value ? player.transform.eulerAngles.y : 0f;
            camYawRad = yaw * Mathf.Deg2Rad;
            invViewSpan = 1f / Mathf.Max(1f, viewSize.Value);

            Transform t = minimapCam.transform;
            t.position = viewCenter + Vector3.up * cameraHeight.Value;
            t.rotation = Quaternion.Euler(90f, yaw, 0f);

            minimapCam.orthographicSize = viewSize.Value * 0.5f;
            minimapCam.farClipPlane = cameraHeight.Value * 2.5f;
            return true;
        }

        private bool ConfigureFullMapCamera()
        {
            Renderer mapRenderer = automap.automapMeshRenderer;
            if (mapRenderer == null)
            {
                return false;
            }

            Bounds b = mapRenderer.bounds;
            if (learnedMapRenderer != null && learnedMapRenderer.bounds.size.sqrMagnitude > 0.001f)
            {
                b.Encapsulate(learnedMapRenderer.bounds);
            }
            float extent = Mathf.Max(b.size.x, b.size.z);
            if (extent <= 0.001f)
            {
                return false;
            }

            // North-up, centered on the map mesh bounds.
            viewCenter = new Vector3(b.center.x, playerPos.y, b.center.z);
            camYawRad = 0f;
            invViewSpan = 1f / (extent * 1.04f);

            Transform t = minimapCam.transform;
            t.position = viewCenter + Vector3.up * cameraHeight.Value;
            t.rotation = Quaternion.Euler(90f, 0f, 0f);

            minimapCam.orthographicSize = extent * 0.52f;
            minimapCam.farClipPlane = cameraHeight.Value * 2.5f;
            return true;
        }

        private static scrPlayerControl FindLocalPlayerControl()
        {
            scrGameControl game = scrGameControl.Instance;
            if (game != null && game.localPlayerID >= 0)
            {
                try
                {
                    GameObject playerObject = game.getPlayerObject(game.localPlayerID);
                    if (playerObject != null)
                    {
                        scrPlayerControl player = playerObject.GetComponent<scrPlayerControl>();
                        if (player != null)
                        {
                            return player;
                        }
                    }
                }
                catch
                {
                }

                if (game.gamePlayers != null && game.localPlayerID < game.gamePlayers.Length)
                {
                    scrPlayer data = game.gamePlayers[game.localPlayerID];
                    if (data != null && data.playerObject != null)
                    {
                        return data.playerObject.GetComponent<scrPlayerControl>();
                    }
                }
            }

            return UnityEngine.Object.FindObjectOfType<scrPlayerControl>();
        }

        // ------------------------------------------------------------------
        // Blip caches.
        // ------------------------------------------------------------------

        private void RefreshBlipCaches()
        {
            if (Time.unscaledTime < nextBlipRefresh)
            {
                return;
            }

            nextBlipRefresh = Time.unscaledTime + blipRefreshInterval.Value;

            enemyCache.Clear();
            if (showEnemies.Value || nativeShowEnemies.Value)
            {
                scrEnemy[] enemies = UnityEngine.Object.FindObjectsOfType<scrEnemy>();
                for (int i = 0; i < enemies.Length; i++)
                {
                    enemyCache.Add(enemies[i]);
                }
            }

            pickupCache.Clear();
            if (showItems.Value || showKeyItems.Value ||
                nativeShowItems.Value || nativeShowKeyItems.Value)
            {
                scrPickup[] pickups = UnityEngine.Object.FindObjectsOfType<scrPickup>();
                for (int i = 0; i < pickups.Length; i++)
                {
                    pickupCache.Add(pickups[i]);
                }
            }
        }

        // ------------------------------------------------------------------
        // Native rotatable 3D-map markers.
        // ------------------------------------------------------------------

        private void SyncNativeMapMarkers()
        {
            bool visible = shown && mapStage == StageGameMap && automap != null &&
                automap.state != scrAutomap.AutomapState.Hidden;
            if (!visible)
            {
                SetNativeMarkerVisibility(false);
                return;
            }

            EnsureNativeMarkerResources();
            if (nativeMarkerRoot == null || nativeMarkerMesh == null)
            {
                return;
            }
            nativeMarkerRoot.SetActive(true);

            liveMarkerIds.Clear();
            if (nativeShowEnemies.Value)
            {
                for (int i = 0; i < enemyCache.Count; i++)
                {
                    scrEnemy enemy = enemyCache[i];
                    if (enemy == null || enemy.hp <= 0 ||
                        !enemy.gameObject.activeInHierarchy)
                    {
                        continue;
                    }
                    int id = enemy.GetInstanceID();
                    liveMarkerIds.Add(id);
                    GameObject marker = GetOrCreateNativeMarker(nativeEnemyMarkers, id,
                        "Enemy", enemy.isBoss ? nativeBossMaterial : nativeEnemyMaterial);
                    if (marker != null)
                    {
                        float size = enemy.isBoss ? 0.85f : 0.55f;
                        marker.transform.position = enemy.transform.position +
                            Vector3.up * (enemy.isBoss ? 1.15f : 0.8f);
                        marker.transform.localScale = Vector3.one * size;
                    }
                }
            }
            RemoveStaleNativeMarkers(nativeEnemyMarkers, liveMarkerIds);

            liveMarkerIds.Clear();
            for (int i = 0; i < pickupCache.Count; i++)
            {
                scrPickup pickup = pickupCache[i];
                if (pickup == null || !pickup.gameObject.activeInHierarchy)
                {
                    continue;
                }
                bool isKeyItem = !string.IsNullOrEmpty(pickup.objectiveID);
                if ((isKeyItem && !nativeShowKeyItems.Value) ||
                    (!isKeyItem && !nativeShowItems.Value))
                {
                    continue;
                }
                int id = pickup.GetInstanceID();
                liveMarkerIds.Add(id);
                GameObject marker = GetOrCreateNativeMarker(nativePickupMarkers, id,
                    isKeyItem ? "Key Item" : "Item",
                    isKeyItem ? nativeKeyItemMaterial : nativeItemMaterial);
                if (marker != null)
                {
                    marker.transform.position = pickup.transform.position + Vector3.up * 0.55f;
                    marker.transform.localScale = Vector3.one * (isKeyItem ? 0.55f : 0.35f);
                }
            }
            RemoveStaleNativeMarkers(nativePickupMarkers, liveMarkerIds);
        }

        private void EnsureNativeMarkerResources()
        {
            if (automap == null || automap.automapMeshRenderer == null)
            {
                return;
            }
            if (nativeMarkerMesh == null)
            {
                nativeMarkerMesh = CreateMarkerMesh();
            }
            if (nativeEnemyMaterial == null)
                nativeEnemyMaterial = CreateUnlitMaterial(EnemyColor, "Minimap 3D Enemy");
            if (nativeBossMaterial == null)
                nativeBossMaterial = CreateUnlitMaterial(BossColor, "Minimap 3D Boss");
            if (nativeItemMaterial == null)
                nativeItemMaterial = CreateUnlitMaterial(ItemColor, "Minimap 3D Item");
            if (nativeKeyItemMaterial == null)
                nativeKeyItemMaterial = CreateUnlitMaterial(KeyItemColor, "Minimap 3D Key Item");
            if (nativeMarkerRoot == null)
            {
                nativeMarkerRoot = new GameObject("MinimapNative3DBlips");
                nativeMarkerRoot.layer = automap.automapMeshRenderer.gameObject.layer;
                nativeMarkerRoot.transform.SetParent(automap.transform, false);
            }
        }

        private static Mesh CreateMarkerMesh()
        {
            Mesh mesh = new Mesh();
            mesh.name = "Minimap 3D Blip Octahedron";
            mesh.vertices = new Vector3[]
            {
                new Vector3(0f, 0.8f, 0f),
                new Vector3(0f, -0.4f, 0f),
                new Vector3(1f, 0f, 0f),
                new Vector3(-1f, 0f, 0f),
                new Vector3(0f, 0f, 1f),
                new Vector3(0f, 0f, -1f)
            };
            mesh.triangles = new int[]
            {
                0, 4, 2,  0, 3, 4,  0, 5, 3,  0, 2, 5,
                1, 2, 4,  1, 4, 3,  1, 3, 5,  1, 5, 2
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private GameObject GetOrCreateNativeMarker(Dictionary<int, GameObject> markers,
            int id, string label, Material material)
        {
            GameObject marker;
            if (!markers.TryGetValue(id, out marker) || marker == null)
            {
                marker = new GameObject("Minimap 3D " + label + " " + id);
                marker.layer = nativeMarkerRoot.layer;
                marker.transform.SetParent(nativeMarkerRoot.transform, false);
                MeshFilter filter = marker.AddComponent<MeshFilter>();
                filter.sharedMesh = nativeMarkerMesh;
                MeshRenderer renderer = marker.AddComponent<MeshRenderer>();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.allowOcclusionWhenDynamic = false;
                markers[id] = marker;
            }
            MeshRenderer markerRenderer = marker.GetComponent<MeshRenderer>();
            if (markerRenderer != null)
            {
                markerRenderer.sharedMaterial = material;
            }
            return marker;
        }

        private void RemoveStaleNativeMarkers(Dictionary<int, GameObject> markers,
            HashSet<int> liveIds)
        {
            List<int> stale = new List<int>();
            foreach (KeyValuePair<int, GameObject> pair in markers)
            {
                if (!liveIds.Contains(pair.Key))
                {
                    if (pair.Value != null) Destroy(pair.Value);
                    stale.Add(pair.Key);
                }
            }
            for (int i = 0; i < stale.Count; i++)
            {
                markers.Remove(stale[i]);
            }
        }

        private void SetNativeMarkerVisibility(bool visible)
        {
            if (nativeMarkerRoot != null && nativeMarkerRoot.activeSelf != visible)
            {
                nativeMarkerRoot.SetActive(visible);
            }
        }

        private void ResetNativeMapMarkers(bool disposeResources)
        {
            nativeEnemyMarkers.Clear();
            nativePickupMarkers.Clear();
            liveMarkerIds.Clear();
            if (nativeMarkerRoot != null) Destroy(nativeMarkerRoot);
            nativeMarkerRoot = null;
            if (!disposeResources)
            {
                return;
            }
            if (nativeMarkerMesh != null) Destroy(nativeMarkerMesh);
            if (nativeEnemyMaterial != null) Destroy(nativeEnemyMaterial);
            if (nativeBossMaterial != null) Destroy(nativeBossMaterial);
            if (nativeItemMaterial != null) Destroy(nativeItemMaterial);
            if (nativeKeyItemMaterial != null) Destroy(nativeKeyItemMaterial);
            nativeMarkerMesh = null;
            nativeEnemyMaterial = null;
            nativeBossMaterial = null;
            nativeItemMaterial = null;
            nativeKeyItemMaterial = null;
        }

        // ------------------------------------------------------------------
        // Drawing.
        // ------------------------------------------------------------------

        private void OnGUI()
        {
            if (Time.unscaledTime > statusUntil || string.IsNullOrEmpty(statusText))
            {
                // fallthrough - status draws below the panel
            }

            if (statusText != null && Time.unscaledTime <= statusUntil)
            {
                GUI.Label(new Rect(18f, 62f, 520f, 32f), statusText);
            }

            if (!shown || minimapRT == null || !havePlayer || mapStage == StageGameMap)
            {
                return;
            }

            bool fullView = mapStage == StageFull2D && fullMapRT != null;

            Rect outer;
            if (fullView)
            {
                float edge = Mathf.Min(Screen.width, Screen.height) * fullMapScreenShare.Value;
                outer = new Rect((Screen.width - edge) * 0.5f, (Screen.height - edge) * 0.5f, edge, edge);
            }
            else
            {
                float size = panelSize.Value;
                float m = panelMargin.Value;
                outer = new Rect(Screen.width - size - m, Screen.height - size - m, size, size);
            }

            Color oldBg = GUI.backgroundColor;
            GUI.backgroundColor = PanelBg;
            GUI.Box(outer, GUIContent.none);
            GUI.backgroundColor = oldBg;

            Rect inner = new Rect(outer.x + 3f, outer.y + 3f, outer.width - 6f, outer.height - 6f);
            GUI.DrawTexture(inner, fullView ? fullMapRT : minimapRT,
                ScaleMode.StretchToFill, true);

            // Blips are drawn relative to the inner rect; center is the player.
            if (showEnemies.Value)
            {
                for (int i = 0; i < enemyCache.Count; i++)
                {
                    scrEnemy e = enemyCache[i];
                    if (e == null || e.hp <= 0 || !e.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    float px, py, distFactor;
                    if (!TryProject(e.transform.position, inner, out px, out py, out distFactor))
                    {
                        continue;
                    }

                    float radius = e.isBoss ? 5f : 3.5f;
                    DrawDot(px, py, radius, e.isBoss ? BossColor : EnemyColor, distFactor);
                }
            }

            if (showItems.Value || showKeyItems.Value)
            {
                for (int i = 0; i < pickupCache.Count; i++)
                {
                    scrPickup p = pickupCache[i];
                    if (p == null || !p.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    bool key = showKeyItems.Value && !string.IsNullOrEmpty(p.objectiveID);
                    if (!key && !showItems.Value)
                    {
                        continue;
                    }

                    float px, py, distFactor;
                    if (!TryProject(p.transform.position, inner, out px, out py, out distFactor))
                    {
                        continue;
                    }

                    DrawDot(px, py, key ? 4.5f : 2.5f, key ? KeyItemColor : ItemColor, distFactor);
                }
            }

            // Player marker.
            float ppx, ppy, ppd;
            if (TryProject(playerPos, inner, out ppx, out ppy, out ppd))
            {
                DrawDot(ppx, ppy, 4f, PlayerColor, 1f);
            }
        }

        /// <summary>
        /// Projects a world position onto the given map rect. Returns false when the
        /// point is absurdly far away (different map area); out params give the pixel
        /// position (clamped to the panel) and a 0..1 "on panel" factor used to shrink
        /// clamped blips. Projection origin/rotation/scale are the current view's
        /// (viewCenter / camYawRad / invViewSpan), set by the active camera config.
        /// </summary>
        private bool TryProject(Vector3 world, Rect inner, out float px, out float py, out float distFactor)
        {
            float dx = world.x - viewCenter.x;
            float dz = world.z - viewCenter.z;

            // Rotate into camera space (camera yaw = camYawRad, looking down).
            float cosY = Mathf.Cos(camYawRad);
            float sinY = Mathf.Sin(camYawRad);
            float cx = dx * cosY - dz * sinY;
            float cy = dx * sinY + dz * cosY;

            // Normalized -0.5..0.5 across the view width.
            float nx = cx * invViewSpan;
            float ny = cy * invViewSpan;

            if (Mathf.Abs(dx) > viewSize.Value * 40f || Mathf.Abs(dz) > viewSize.Value * 40f)
            {
                px = 0f;
                py = 0f;
                distFactor = 0f;
                return false;
            }

            float pad = BlipClampPad;
            float ux = Mathf.Clamp(nx + 0.5f, pad, 1f - pad);
            float uy = Mathf.Clamp(0.5f - ny, pad, 1f - pad);

            float off = Mathf.Max(Mathf.Abs(nx), Mathf.Abs(ny));
            distFactor = off > 0.5f ? Mathf.Clamp01(1f - (off - 0.5f)) : 1f;

            px = inner.x + ux * inner.width;
            py = inner.y + uy * inner.height;
            return true;
        }

        private static void DrawDot(float cx, float cy, float radius, Color color, float scale)
        {
            Color old = GUI.color;
            GUI.color = new Color(color.r, color.g, color.b, color.a * Mathf.Clamp01(scale));
            float d = radius * 2f * Mathf.Clamp(scale, 0.55f, 1f);
            GUI.DrawTexture(new Rect(cx - d * 0.5f, cy - d * 0.5f, d, d), Texture2D.whiteTexture);
            GUI.color = old;
        }

        // ------------------------------------------------------------------
        // Harmony hooks.
        // ------------------------------------------------------------------

        [HarmonyPatch(typeof(scrAutomap), "populateVertTables")]
        private static class VertTablesPatch
        {
            private static void Postfix()
            {
                // The game rebuilt the map mesh (level start, moving train level):
                // schedule a fresh full reveal on the next plugin Update.
                pendingReveal = true;
            }
        }

        [HarmonyPatch(typeof(scrAutomap), "Update")]
        private static class NativeAutomapMaskPatch
        {
            private static void Postfix(scrAutomap __instance)
            {
                // Remove the same peephole from the native tumble-map stage only
                // when it was opened through this mod's visible map cycle. With
                // the panel hidden, the vanilla map and its mask remain untouched.
                if (instance == null || !instance.shown ||
                    instance.mapStage != StageGameMap || __instance == null ||
                    __instance.state == scrAutomap.AutomapState.Hidden)
                {
                    return;
                }
                instance.SyncLearnedMapMaterial();
                ClearAutomapCenterMask(__instance.automapMeshRenderer);
                ClearAutomapCenterMask(__instance.automapBackingMeshRenderer);
                ClearAutomapCenterMask(instance.learnedMapRenderer);
            }
        }
    }
}
