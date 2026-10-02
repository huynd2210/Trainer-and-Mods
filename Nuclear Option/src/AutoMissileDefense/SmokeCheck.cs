using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace NuclearOptionAutoMissileDefense
{
    [HarmonyPatch(typeof(MainMenu), "Awake")]
    static class SmokeMemoryLimit
    {
        static void Postfix()
        {
            if (!Environment.GetCommandLineArgs().Contains("-amd-smoke")) return;
            // QA process only; no PlayerSettings or PlayerPrefs writes.
            QualitySettings.globalTextureMipmapLimit = 3;
            QualitySettings.antiAliasing = 0;
        }
    }
    // Explicit offline QA only. Never enters a mission or runs in normal launches.
    [HarmonyPatch(typeof(MainMenu), "Update")]
    static class SmokeCheck
    {
        static bool done;
        static void Postfix()
        {
            if (done || !Environment.GetCommandLineArgs().Contains("-amd-smoke") || MainMenu.State != MainMenu.LoadingState.Loaded) return;
            done = true;
            new GameObject("AMD offline QA").AddComponent<DefenseCapture>().StartCoroutine(DefenseCapture.Run());
        }
    }

    public sealed class DefenseCapture : MonoBehaviour
    {
        internal static IEnumerator Run()
        {
            var output = Path.Combine(BepInEx.Paths.GameRootPath, "ModSource", "AutoMissileDefense", "evidence");
            Directory.CreateDirectory(output);
            var report = new StringBuilder();
            yield return null;
            yield return null;
            AsyncOperation sceneLoad = null;
            try
            {
                // Load only the empty game scene's authored UI. Suppress gameplay lifecycles in
                // this QA process so no mission, networking session, or player input is started.
                var qa = new Harmony("nuclearoption.automissiledefense.offlineqa");
                var skip = new HarmonyMethod(typeof(DefenseCapture), nameof(SkipLifecycle));
                var callbacks = new[] { "Awake", "OnEnable", "Start", "Update", "LateUpdate", "FixedUpdate" };
                foreach (var type in typeof(CombatHUD).Assembly.GetTypes())
                {
                    if (!typeof(MonoBehaviour).IsAssignableFrom(type) || type.ContainsGenericParameters) continue;
                    foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                        if (callbacks.Contains(method.Name) && !method.IsAbstract && !method.ContainsGenericParameters)
                            qa.Patch(method, prefix: skip);
                }
                sceneLoad = SceneManager.LoadSceneAsync("Assets/Scenes/GameWorld/GameWorld.unity", LoadSceneMode.Additive);
            }
            catch (Exception error) { File.WriteAllText(Path.Combine(output, "smoke-result.txt"), "FAIL loading offline UI: " + error); }
            if (sceneLoad == null) { Application.Quit(); yield break; }
            yield return sceneLoad;
            try
            {
                var options = Resources.FindObjectsOfTypeAll<HUDOptions>().FirstOrDefault(x => x.buttonPrefab != null);
                if (options == null) throw new Exception("Native HUDOptions prefab unavailable");
                var template = options.buttonPrefab.GetComponent<HUDOptions_ToggleButton>();
                report.AppendLine("Native template: " + template.name);
                var hud = Resources.FindObjectsOfTypeAll<CombatHUD>().FirstOrDefault();
                report.AppendLine("CombatHUD template: " + (hud == null ? "unavailable" : hud.name));
                if (hud != null)
                {
                    foreach (var rect in hud.GetComponentsInChildren<RectTransform>(true))
                        report.AppendLine($"HUD rect {rect.name}: anchors {rect.anchorMin}/{rect.anchorMax}, pos {rect.anchoredPosition}, size {rect.sizeDelta}");
                    report.AppendLine("CombatHUD canvas ancestor: " + (hud.GetComponentInParent<Canvas>() != null));
                }
                var ir = Encyclopedia.i.weaponMounts.Where(m => m != null && m.info != null && m.info.weaponPrefab != null && m.info.weaponPrefab.GetComponent<IRSeeker>() != null).ToArray();
                if (!ir.Any(m => m.info.weaponName.Contains("MMR-S3"))) throw new Exception("MMR-S3 not found among native IR mounts");
                foreach (var mount in ir)
                {
                    report.AppendLine("IR mount: " + mount.mountName + "; weapon: " + mount.info.weaponName + "; ammo: " + mount.ammo);
                }
                Render(template, output, false);
                Render(template, output, true);
                report.AppendLine("PASS: native toggle created and both states rendered; real IR mount metadata inspected.");
                File.WriteAllText(Path.Combine(output, "smoke-result.txt"), report.ToString());
                AutoMissileDefensePlugin.Log.LogInfo("AMD offline smoke PASS");
            }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(output, "smoke-result.txt"), report + "\nFAIL: " + error);
                AutoMissileDefensePlugin.Log.LogError(error);
            }
            Application.Quit();
        }

        static bool SkipLifecycle() => false;

        static void Render(HUDOptions_ToggleButton template, string output, bool armed)
        {
            var root = new GameObject("Native AMD canvas", typeof(RectTransform), typeof(Canvas));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.planeDistance = 1f;
            var camera = new GameObject("AMD offscreen camera").AddComponent<Camera>();
            camera.cullingMask = 1 << 30;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.08f, .11f, .14f);
            var texture = new RenderTexture(960, 240, 24);
            camera.targetTexture = texture;
            canvas.worldCamera = camera;
            var button = NativeDefenseButton.Create(template, root.transform);
            button.SetState(armed, "F8");
            if (button.native.status != armed || !button.label.text.Contains(armed ? ": ON" : ": OFF")) throw new Exception("Button state mismatch");
            foreach (var group in root.GetComponentsInChildren<CanvasGroup>(true)) group.alpha = 1;
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 30;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = texture;
            var image = new Texture2D(960, 240, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 960, 240), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(output, armed ? "native-toggle-on.png" : "native-toggle-off.png"), image.EncodeToPNG());
            RenderTexture.active = previous;
            Object.Destroy(root);
            camera.targetTexture = null;
            Object.Destroy(camera.gameObject);
            texture.Release();
            Object.Destroy(texture);
            Object.Destroy(image);
        }
    }
}
