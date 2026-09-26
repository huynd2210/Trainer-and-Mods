using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Mirage;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NuclearOptionAMRAAM
{
    [BepInPlugin("nuclearoption.amraam", "AMRAAM Lofted ARH Missile", "1.1.0")]
    public sealed class AMRAAMPlugin : BaseUnityPlugin
    {
        internal const string Key = "mod_amraam_120";
        internal const float Range = 140000f, TopSpeed = 1372f;
        internal static ManualLogSource Log;
        internal static ConfigEntry<float> Threshold, MaxRise, Terminal;
        static GameObject storage;
        internal static GameObject model;
        internal static MissileDefinition definition;
        static WeaponInfo info;
        static string folder;
        static bool failed;
        internal static readonly FieldInfo KnownPosition = AccessTools.Field(typeof(ARHSeeker), "knownPos");
        internal static readonly FieldInfo KnownVelocity = AccessTools.Field(typeof(ARHSeeker), "knownVel");
        static readonly FieldInfo TargetDistance = AccessTools.Field(typeof(ARHSeeker), "targetDist");
        static readonly FieldInfo GuidanceEnabled = AccessTools.Field(typeof(ARHSeeker), "guidance");

        void Awake()
        {
            Log = Logger;
            folder = Path.GetDirectoryName(Info.Location);
            Threshold = Config.Bind("Trajectory", "LoftBeyondMetres", 20000f,
                new ConfigDescription("Launches beyond this horizontal range use a lofted arc.", new AcceptableValueRange<float>(5000f, 100000f)));
            MaxRise = Config.Bind("Trajectory", "MaximumRiseMetres", 18000f,
                new ConfigDescription("Maximum arc height above the straight launch-to-target altitude profile.", new AcceptableValueRange<float>(1000f, 25000f)));
            Terminal = Config.Bind("Trajectory", "TerminalRangeMetres", 12000f,
                new ConfigDescription("Inside this distance commit to normal ARH interception.", new AcceptableValueRange<float>(2000f, 18000f)));
            new Harmony("nuclearoption.amraam").PatchAll(typeof(AMRAAMPlugin).Assembly);
            Log.LogInfo("AMRAAM hooks installed. Registration runs after the weapon encyclopedia loads.");
            // This game's startup destroys the BepInEx host. Static hooks deliberately survive it.
        }

        internal static void Set(object target, string field, object value)
        {
            var f = AccessTools.Field(target.GetType(), field);
            if (f == null) throw new MissingFieldException(target.GetType().Name, field);
            f.SetValue(target, value);
        }

        static bool IsARH(WeaponMount m) => m != null && m.info != null && m.info.missile &&
            m.info.weaponPrefab != null && m.info.weaponPrefab.GetComponent<ARHSeeker>() != null &&
            m.prefab != null && m.prefab.GetComponentInChildren<MountedMissile>(true) != null;

        internal static void Register(Encyclopedia encyclopedia)
        {
            if (failed || encyclopedia.missiles.Any(d => d.jsonKey == Key)) return;
            try
            {
                var originals = encyclopedia.weaponMounts.Where(IsARH).OrderBy(m => m.jsonKey).ToArray();
                if (originals.Length == 0) throw new InvalidOperationException("No mounted ARH donor exists");
                var donor = originals.FirstOrDefault(m => m.info.weaponName.IndexOf("Scythe", StringComparison.OrdinalIgnoreCase) >= 0) ?? originals[0];
                storage = new GameObject("AMRAAM persistent prefab storage");
                storage.SetActive(false); Object.DontDestroyOnLoad(storage);
                model = ModelAsset.Load(Path.Combine(folder, "amraam.meshbin"), storage.transform);
                var prefab = Object.Instantiate(donor.info.weaponPrefab, storage.transform);
                prefab.name = "AIM-120 AMRAAM"; prefab.SetActive(true);
                var missile = prefab.GetComponent<Missile>();
                prefab.GetComponent<NetworkIdentity>().PrefabHash = 0x414D5231;
                definition = Object.Instantiate((MissileDefinition)missile.definition);
                definition.name = definition.jsonKey = Key; definition.unitName = "AIM-120 AMRAAM";
                definition.code = "AIM-120"; definition.bogeyName = "ARH missile";
                definition.description = "Active radar air-to-air missile. Direct at close range; high loft and terminal dive at long range. 140 km nominal range, Mach 4, $1 million.";
                definition.value = 1f; definition.mass = 161.5f; definition.length = 3.65f;
                definition.width = definition.height = 0.57f; definition.unitPrefab = prefab;
                missile.definition = definition;
                info = Object.Instantiate(donor.info); info.name = Key + "_info";
                info.weaponName = "AIM-120 AMRAAM"; info.shortName = "AMRAAM"; info.description = definition.description;
                info.weaponPrefab = prefab; info.costPerRound = 1f; info.massPerRound = 161.5f; info.maxSpeed = TopSpeed;
                var req = info.targetRequirements; req.maxRange = Range; info.targetRequirements = req;
                Set(missile, "info", info); Set(missile, "mass", 161.5f); Set(missile, "gLimit", 40f);
                Set(missile, "blastYield", 20f);
                // A gameplay boost/sustain motor provides the requested long-range envelope.
                // It is intentionally not a simulation of the classified real motor.
                var motors = (Array)AccessTools.Field(typeof(Missile), "motors").GetValue(missile);
                var nozzle = new GameObject("AMRAAM nozzle").transform;
                nozzle.SetParent(prefab.transform, false); nozzle.localPosition = new Vector3(0f, 0f, -1.837f);
                float stageDelay = 0f;
                for (int i = 0; i < motors.Length; i++)
                {
                    var motor = motors.GetValue(i);
                    float burnTime = i == 0 && motors.Length > 1 ? 8f : 155f;
                    Set(motor, "topSpeed", TopSpeed); Set(motor, "thrust", i == 0 && motors.Length > 1 ? 26000f : 6500f);
                    Set(motor, "burnTime", burnTime);
                    Set(motor, "fuelMass", 55f / motors.Length);
                    foreach (var trail in (TrailEmitter[])AccessTools.Field(motor.GetType(), "trailEmitters").GetValue(motor))
                    {
                        Set(trail, "emitTransform", nozzle); Set(trail, "emitDelay", stageDelay);
                        Set(trail, "emitLifetime", burnTime);
                    }
                    foreach (var particle in (ParticleSystem[])AccessTools.Field(motor.GetType(), "particleSystems").GetValue(motor))
                        particle.transform.position = nozzle.position;
                    foreach (var light in (Light[])AccessTools.Field(motor.GetType(), "lights").GetValue(motor))
                        light.transform.position = nozzle.position;
                    stageDelay += burnTime;
                }
                var capsule = prefab.GetComponent<CapsuleCollider>();
                if (capsule != null) { capsule.center = Vector3.zero; capsule.height = 3.65f; capsule.radius = .089f; capsule.direction = 2; }
                var seeker = prefab.GetComponent<ARHSeeker>();
                Set(seeker, "loftAmount", 0f); Set(seeker, "terminalRange", 18000f);
                Set(seeker, "jinkEvasion", new JinkEvasion());
                prefab.AddComponent<LoftState>(); ModelAsset.Replace(prefab, model);
                Object.DontDestroyOnLoad(definition); Object.DontDestroyOnLoad(info);
                encyclopedia.missiles.Add(definition); Encyclopedia.Lookup.Add(Key, definition);
                ((INetworkDefinition)definition).LookupIndex = encyclopedia.IndexLookup.Count;
                encyclopedia.IndexLookup.Add(definition);
                var replacements = new Dictionary<WeaponMount, WeaponMount>();
                foreach (var original in originals)
                {
                    var mount = Object.Instantiate(original);
                    mount.name = mount.jsonKey = Key + "_" + original.jsonKey;
                    mount.info = info;
                    mount.prefab = Object.Instantiate(original.prefab, storage.transform);
                    mount.prefab.name = mount.name; mount.prefab.SetActive(true);
                    foreach (var weapon in mount.prefab.GetComponentsInChildren<MountedMissile>(true))
                    { weapon.info = info; ModelAsset.Replace(weapon.gameObject, model); }
                    mount.Initialize(); Object.DontDestroyOnLoad(mount);
                    encyclopedia.weaponMounts.Add(mount); Encyclopedia.WeaponLookup.Add(mount.jsonKey, mount);
                    ((INetworkDefinition)mount).LookupIndex = encyclopedia.IndexLookup.Count;
                    encyclopedia.IndexLookup.Add(mount); replacements.Add(original, mount);
                }
                int slots = 0;
                foreach (var aircraft in encyclopedia.aircraft)
                {
                    var wm = aircraft.unitPrefab.GetComponentInChildren<WeaponManager>(true);
                    if (wm == null) continue;
                    foreach (var set in wm.hardpointSets)
                    {
                        foreach (var original in set.weaponOptions.ToArray())
                            if (original != null && replacements.TryGetValue(original, out var addition) && !set.weaponOptions.Contains(addition))
                            { set.weaponOptions.Add(addition); slots++; }
                    }
                }
                Log.LogInfo($"AMRAAM registered: {replacements.Count} mounts, {slots} hardpoint options; cost={info.costPerRound} million, range={req.maxRange} m, mass={info.massPerRound} kg; donor={donor.info.weaponName}");
                foreach (var manager in Resources.FindObjectsOfTypeAll<ClientObjectManager>())
                    manager.RegisterPrefab(prefab.GetComponent<NetworkIdentity>());
            }
            catch (Exception e) { failed = true; Log.LogError("AMRAAM registration failed: " + e); }
        }

        [HarmonyPatch(typeof(Encyclopedia), "AfterLoad", new Type[0])]
        static class RegistryPatch { static void Postfix(Encyclopedia __instance) => Register(__instance); }

        [HarmonyPatch(typeof(ClientObjectManager), "RegisterPrefabs")]
        static class NetworkPrefabPatch
        {
            static void Postfix(ClientObjectManager __instance)
            {
                if (definition != null) __instance.RegisterPrefab(definition.unitPrefab.GetComponent<NetworkIdentity>());
            }
        }

        [HarmonyPatch(typeof(ARHSeeker), "Initialize")]
        static class LaunchPatch
        {
            static void Postfix(ARHSeeker __instance, Unit target)
            {
                var state = __instance.GetComponent<LoftState>();
                if (state == null) return;
                state.Reset();
                // Do not loft toward the vanilla 100-km fallback when no track is available.
                var m = __instance.GetComponent<Missile>();
                state.SearchHeading = m.transform.forward;
                BlindSearch.TrackChaffSubscription(__instance, state, target);
                if (target == null || m.NetworkHQ == null || !m.NetworkHQ.TryGetKnownPosition(target, out var known)) return;
                state.Begin(m.GlobalPosition(), known);
            }
        }

        [HarmonyPatch(typeof(ARHSeeker), "Seek")]
        static class SeekPatch
        {
            static bool Prefix(ARHSeeker __instance)
            {
                var state = __instance.GetComponent<LoftState>();
                if (state == null) return true;
                if (!BlindSearch.BeforeSeek(__instance, state)) return false;
                // Vanilla only updates this inside its own loft code, which we replace.
                var m = __instance.GetComponent<Missile>();
                TargetDistance.SetValue(__instance, ((GlobalPosition)KnownPosition.GetValue(__instance) - m.GlobalPosition()).magnitude);
                return true;
            }
            static void Postfix(ARHSeeker __instance)
            {
                var state = __instance.GetComponent<LoftState>();
                if (state == null || !state.Lofting || state.TerminalCommitted) return;
                var m = __instance.GetComponent<Missile>();
                if (m.disabled || m.targetID.NotValid || !(bool)GuidanceEnabled.GetValue(__instance)) return;
                var known = (GlobalPosition)KnownPosition.GetValue(__instance);
                var current = m.GlobalPosition();
                var delta = known - current;
                float remaining = new Vector2(delta.x, delta.z).magnitude;
                if (delta.magnitude <= Terminal.Value || remaining < 1500f)
                { state.TerminalCommitted = true; Log.LogInfo("AMRAAM terminal dive / normal ARH guidance"); return; }
                state.Progress = LoftProfile.Advance(state.Progress, state.InitialRange, remaining);
                if (state.Progress >= .97f) { state.TerminalCommitted = true; return; }
                float lookAhead = Mathf.Min(remaining, Mathf.Clamp(m.speed * 4f, 1800f, 6000f));
                float t = Mathf.Min(1f, state.Progress + lookAhead / state.InitialRange);
                var aim = current + new Vector3(delta.x, 0, delta.z).normalized * lookAhead;
                aim.y = Mathf.Max(100f, LoftProfile.Height(t, state.StartAltitude, known.y, state.Rise));
                m.SetAimpoint(aim, (Vector3)KnownVelocity.GetValue(__instance));
            }
        }

        [HarmonyPatch(typeof(Missile), "GetTopSpeed")]
        static class TopSpeedPatch
        {
            static void Postfix(Missile __instance, ref float __result)
            {
                if (__instance.definition != null && __instance.definition.jsonKey == Key)
                    __result = Mathf.Min(__result, TopSpeed);
            }
        }

        [HarmonyPatch(typeof(Missile), "CalcRange")]
        static class RangePatch
        {
            static void Postfix(Missile __instance, ref float __result, ref float noEscapeDistance)
            {
                if (__instance.definition == null || __instance.definition.jsonKey != Key) return;
                __result = Mathf.Min(__result, Range);
                noEscapeDistance = Mathf.Min(noEscapeDistance, __result);
            }
        }

        [HarmonyPatch(typeof(Missile), "FixedUpdate")]
        static class SpeedPatch
        {
            static void Prefix(Missile __instance)
            {
                if (__instance.definition == null || __instance.definition.jsonKey != Key || !__instance.LocalSim || __instance.rb == null) return;
                // Cap actual airspeed at Mach 4 using the game's local speed of sound.
                float maximum = 4f * LevelInfo.GetSpeedOfSound(__instance.GlobalPosition().y);
                if (__instance.rb.velocity.sqrMagnitude > maximum * maximum)
                    __instance.rb.velocity = __instance.rb.velocity.normalized * maximum;
            }
        }
    }

    public sealed class LoftState : MonoBehaviour
    {
        internal float InitialRange, StartAltitude, Rise, Progress;
        internal bool Lofting, TerminalCommitted;
        internal bool Searching;
        internal float NextScan;
        internal Vector3 SearchHeading;
        internal Unit ChaffTarget;
        internal Action<RadarChaff> ChaffHandler;
        internal void Reset() { Lofting = TerminalCommitted = Searching = false; Progress = NextScan = 0; }
        void OnDestroy() { if (ChaffTarget != null && ChaffHandler != null) ChaffTarget.onAddRadarChaff -= ChaffHandler; }
        internal void Begin(GlobalPosition start, GlobalPosition target)
        {
            var d = target - start; InitialRange = new Vector2(d.x, d.z).magnitude; StartAltitude = start.y;
            Rise = LoftProfile.Rise(InitialRange, AMRAAMPlugin.Threshold.Value, AMRAAMPlugin.MaxRise.Value);
            Lofting = Rise > 0;
            AMRAAMPlugin.Log.LogInfo($"AMRAAM launch: {(Lofting ? "LOFT" : "DIRECT")}, range={InitialRange:F0}m, rise={Rise:F0}m");
        }
    }
}
