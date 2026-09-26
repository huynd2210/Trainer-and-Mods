using System;
using System.IO;
using System.Linq;
using System.Text;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NuclearOptionAMRAAM
{
    // Explicit command-line-only QA. Never runs during normal gameplay.
    [HarmonyPatch(typeof(MainMenu), "Update")]
    static class SmokeCheck
    {
        static bool done;
        static void Postfix()
        {
            if (done || !Environment.GetCommandLineArgs().Contains("-amraam-smoke")) return;
            if (AMRAAMPlugin.definition == null || MainMenu.State != MainMenu.LoadingState.Loaded) return;
            done = true;
            try
            {
                var output = Path.Combine(PathsRoot(), "evidence"); Directory.CreateDirectory(output);
                Render(output, "model-profile.png", new Vector3(6f, .35f, 0));
                Render(output, "model-perspective.png", new Vector3(5f, 3f, 3f));
                var missile = AMRAAMPlugin.definition.unitPrefab.GetComponent<Missile>();
                var info = missile.GetWeaponInfo();
                if (info.costPerRound != 1f || info.massPerRound != 161.5f || info.targetRequirements.maxRange != 140000f)
                    throw new Exception("Weapon metadata mismatch");
                float estimate = missile.CalcRange(250f, 1000f, 1000f, 140000f, 0f, out float noEscape);
                var report = new StringBuilder($"Registered {info.weaponName}\nCost {UnitConverter.ValueReading(info.costPerRound)}\nMass {info.massPerRound}\nMax engagement range {info.targetRequirements.maxRange}\nVanilla range estimate (capped) {estimate}\nNo escape estimate (capped) {noEscape}\n");
                var mounts = Encyclopedia.i.weaponMounts.Where(m => m.jsonKey.StartsWith(AMRAAMPlugin.Key + "_")).ToArray();
                foreach (var mount in mounts)
                {
                    var weapons = mount.prefab.GetComponentsInChildren<MountedMissile>(true);
                    if (weapons.Length != mount.ammo || weapons.Any(w => w.info != info)) throw new Exception("Mount/ammo mismatch: " + mount.name);
                    foreach (var w in weapons)
                    {
                        var visual = w.transform.Find("AMRAAM custom visual");
                        if (visual == null) throw new Exception("Missing mounted model");
                        var scale = visual.lossyScale;
                        if ((scale - Vector3.one).sqrMagnitude > .01f) throw new Exception("Unexpected mount scaling: " + scale);
                    }
                    report.AppendLine($"Mount {mount.mountName}; rounds {mount.ammo}; loaded mass {mount.mass}; rack cost {mount.emptyCost}");
                }
                foreach (var aircraft in Encyclopedia.i.aircraft)
                {
                    var wm = aircraft.unitPrefab.GetComponentInChildren<WeaponManager>(true);
                    if (wm != null && wm.hardpointSets.Any(s => s.weaponOptions.Any(m => m != null && mounts.Contains(m))))
                        report.AppendLine("Compatible aircraft: " + aircraft.unitName);
                    if(wm==null) continue;
                    foreach(var set in wm.hardpointSets)
                    {
                        var available=new System.Collections.Generic.List<WeaponMount>();
                        WeaponChecker.GetAvailableWeaponsNonAlloc(null,set,null,null,false,available);
                        var amraams=available.Where(MountOptions.IsAMRAAM).ToArray();
                        if(amraams.GroupBy(m=>m.ammo).Any(g=>g.Count()>1)) throw new Exception("Duplicate AMRAAM option: "+aircraft.unitName+" "+set.name);
                        if(amraams.Length>0) report.AppendLine("Chooser "+aircraft.unitName+" / "+set.name+": "+string.Join(", ",amraams.Select(m=>m.mountName)));
                    }
                }
                report.AppendLine("Native WeaponSelector prefabs available: "+Resources.FindObjectsOfTypeAll<WeaponSelector>().Length);
                MenuSmokeCapture.Render(output,report);
                SearchSmokeTests.Run(report);
                var motors = (Array)AccessTools.Field(typeof(Missile), "motors").GetValue(missile);
                foreach (var motor in motors)
                {
                    report.AppendLine("Motor " + UnityEngine.JsonUtility.ToJson(motor));
                    foreach (var trail in (TrailEmitter[])AccessTools.Field(motor.GetType(), "trailEmitters").GetValue(motor))
                    {
                        var emitter = (Transform)AccessTools.Field(typeof(TrailEmitter), "emitTransform").GetValue(trail);
                        var local = missile.transform.InverseTransformPoint(emitter.position);
                        if ((local-new Vector3(0,0,-1.837f)).sqrMagnitude>.0001f) throw new Exception("Exhaust misaligned");
                        report.AppendLine("Trail nozzle " + local + "; duration " + AccessTools.Field(typeof(TrailEmitter), "emitLifetime").GetValue(trail));
                    }
                }
                var effects = (Transform)AccessTools.Field(typeof(Missile), "effectsTransform").GetValue(missile);
                if (effects != null) report.AppendLine("Donor effects local position: " + effects.localPosition);
                foreach (var collider in missile.GetComponentsInChildren<Collider>(true))
                {
                    report.AppendLine("Collider: " + collider.GetType().Name + " at " + collider.transform.localPosition);
                    if (collider is CapsuleCollider capsule) report.AppendLine($"Capsule center {capsule.center}, height {capsule.height}, radius {capsule.radius}, direction {capsule.direction}");
                }
                foreach (var t in missile.GetComponentsInChildren<Transform>(true))
                    if (t.name.IndexOf("exhaust", StringComparison.OrdinalIgnoreCase)>=0 || t.name.IndexOf("trail", StringComparison.OrdinalIgnoreCase)>=0)
                        report.AppendLine("Effect " + t.name + " at missile-local " + missile.transform.InverseTransformPoint(t.position));
                File.WriteAllText(Path.Combine(output, "runtime-check.txt"), report.ToString());
                AMRAAMPlugin.Log.LogInfo("AMRAAM_SMOKE_PASS");
            }
            catch (Exception ex)
            {
                File.WriteAllText(Path.Combine(PathsRoot(),"evidence","smoke-failure.txt"),ex.ToString());
                AMRAAMPlugin.Log.LogError("AMRAAM_SMOKE_FAIL " + ex);
                Application.Quit();
            }
        }
        static string PathsRoot() => Path.Combine(BepInEx.Paths.GameRootPath, "ModSource", "AMRAAM");
        static void Render(string output, string name, Vector3 eye)
        {
            var root = new GameObject("AMRAAM render check"); root.transform.position = new Vector3(0, -5000, 0);
            var visual = Object.Instantiate(AMRAAMPlugin.model, root.transform, false);
            foreach (var t in visual.GetComponentsInChildren<Transform>()) t.gameObject.layer = 30;
            var lamp = new GameObject("QA light").AddComponent<Light>(); lamp.type = LightType.Directional;
            lamp.transform.rotation = Quaternion.Euler(45, -45, 0); lamp.intensity = 2.2f; lamp.cullingMask = 1 << 30;
            var camera = new GameObject("QA camera").AddComponent<Camera>();
            camera.transform.position = root.transform.position + eye; camera.transform.LookAt(root.transform.position);
            camera.orthographic = true; camera.orthographicSize = .9f; camera.aspect = 1600f / 700f;
            camera.cullingMask = 1 << 30; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.065f, .085f, .11f); camera.nearClipPlane = .01f; camera.farClipPlane = 30;
            var rt = new RenderTexture(1600, 700, 24); camera.targetTexture = rt;
            camera.Render(); var old = RenderTexture.active; RenderTexture.active = rt;
            var image = new Texture2D(1600, 700, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0,0,1600,700),0,0); image.Apply();
            File.WriteAllBytes(Path.Combine(output,name), image.EncodeToPNG()); RenderTexture.active = old;
            camera.targetTexture = null; rt.Release(); Object.Destroy(rt); Object.Destroy(image);
            Object.Destroy(root); Object.Destroy(camera.gameObject); Object.Destroy(lamp.gameObject);
        }
    }
}
