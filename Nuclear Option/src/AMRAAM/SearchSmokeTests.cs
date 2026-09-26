using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using Mirage;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NuclearOptionAMRAAM
{
    // Inactive fixtures used only by -amraam-smoke. Tests production search code
    // and native seeker geometry/LOS; controlled radar echoes isolate selection.
    public sealed class SearchTestAircraft : Aircraft, IRadarReturn
    {
        internal float Signal = 100f;
        float IRadarReturn.GetRadarReturn(Vector3 source, Radar radar, Unit emitter, float dist, float clutter, RadarParams p, bool warning) => Signal;
        float IRadarReturn.GetECMIntensity() => 0f;
    }

    internal static class SearchSmokeTests
    {
        internal static void Run(StringBuilder report)
        {
            var root = new GameObject("Inactive AMRAAM test fixtures"); root.SetActive(false);
            var originalUnits = new List<Unit>(UnitRegistry.allUnits);
            GameObject obstacle = null;
            int checks = 0;
            void Check(bool pass, string label) { if (!pass) throw new Exception("Search test: " + label); checks++; }
            try
            {
                var friendlyHQ = NewHQ(root.transform, "friendly"); var enemyHQ = NewHQ(root.transform, "enemy");
                var go = Object.Instantiate(AMRAAMPlugin.definition.unitPrefab, root.transform);
                go.transform.position = new Vector3(0,10000,0); go.transform.rotation = Quaternion.identity;
                var missile = go.GetComponent<Missile>(); var seeker = go.GetComponent<ARHSeeker>(); var state = go.GetComponent<LoftState>();
                missile.NetworkHQ = friendlyHQ; missile.SetRB(go.GetComponent<Rigidbody>()); missile.SetLocalSim(true); missile.disabled=false;
                AMRAAMPlugin.Set(missile,"<timeSinceSpawn>k__BackingField",2f);
                AMRAAMPlugin.Set(seeker,"guidance",true); AMRAAMPlugin.Set(seeker,"armed",true);
                var radar = seeker.GetRadarParams(); float range = radar.maxRange;
                report.AppendLine($"Blind search native seeker: max range {range}m, half-angle {AccessTools.Field(typeof(ARHSeeker),"maxTrackingAngle").GetValue(seeker)} degrees");
                var friend = NewAircraft(root.transform, friendlyHQ, 90001, new Vector3(0,10000,1000));
                var first = NewAircraft(root.transform, enemyHQ, 90002, new Vector3(0,10000,Mathf.Min(range*.3f,4000f)));
                var second = NewAircraft(root.transform, enemyHQ, 90003, new Vector3(0,10000,500));
                var neutral = NewAircraft(root.transform, null, 90004, new Vector3(0,10000,1000));
                UnitRegistry.allUnits.Clear(); UnitRegistry.allUnits.Add(friend); UnitRegistry.allUnits.Add(neutral);
                Check(!BlindSearch.Eligible(missile,friend),"exclude friendly");
                Check(!BlindSearch.Eligible(missile,neutral),"exclude neutral/unassigned");
                Check(!BlindSearch.BeforeSeek(seeker,state) && state.Searching && missile.targetID.NotValid,"unlocked flight remains searching");
                Check(missile.seekerMode == Missile.SeekerMode.activeSearch,"active search mode");
                var aim=(GlobalPosition)AccessTools.Field(typeof(Missile),"aimPoint").GetValue(missile);
                Check((aim-missile.GlobalPosition()-Vector3.forward*10000f).magnitude<.1f,"continue forward");
                first.transform.position = new Vector3(0,10000,range+100);
                Check(BlindSearch.Detect(seeker,missile,first)<=radar.minSignal,"reject out of range");
                first.transform.position = new Vector3(0,10000,-1000);
                Check(BlindSearch.Detect(seeker,missile,first)<=radar.minSignal,"reject behind seeker");
                first.transform.position = new Vector3(0,10000,4000);
                first.Signal=0;
                Check(BlindSearch.Detect(seeker,missile,first)<=radar.minSignal,"reject weak radar echo"); first.Signal=100;
                obstacle=GameObject.CreatePrimitive(PrimitiveType.Cube); obstacle.layer=6;
                obstacle.transform.position=new Vector3(0,10000,2000); obstacle.transform.localScale=new Vector3(500,500,50);
                Physics.SyncTransforms();
                Check(BlindSearch.Detect(seeker,missile,first)<=radar.minSignal,"terrain obstruction rejects target");
                obstacle.SetActive(false); Physics.SyncTransforms();
                Check(BlindSearch.Detect(seeker,missile,first)>radar.minSignal,"visible radar target detected");
                Check(BlindSearch.Detect(seeker,missile,second)>radar.minSignal,"initial acquisition inside stock reacquire minimum");
                UnitRegistry.allUnits.Add(first); UnitRegistry.allUnits.Add(second); state.NextScan=0;
                Check(BlindSearch.BeforeSeek(seeker,state) && missile.targetID==first.persistentID,"first detected enemy wins, not nearest");
                Check(!state.Searching && missile.seekerMode==Missile.SeekerMode.activeLock,"transition to active lock");
                AMRAAMPlugin.Set(seeker,"topSpeed",1372f);
                seeker.Seek();
                Check(missile.targetID==first.persistentID,"patched native Seek accepts autonomous lock");
                Check(BlindSearch.BeforeSeek(seeker,state) && missile.targetID==first.persistentID,"keep target despite nearer enemy");
                first.disabled=true; state.NextScan=0;
                Check(BlindSearch.BeforeSeek(seeker,state) && missile.targetID==second.persistentID,"resume search after target disabled");
                Check(state.TerminalCommitted && !state.Lofting,"self-acquired target uses direct terminal homing");
                report.AppendLine($"PASS: {checks} in-game search assertions (controlled radar echoes; native cone/range/LOS).");
            }
            finally
            {
                UnitRegistry.allUnits.Clear(); UnitRegistry.allUnits.AddRange(originalUnits);
                if(obstacle!=null) Object.Destroy(obstacle); Object.Destroy(root);
            }
        }
        static FactionHQ NewHQ(Transform parent,string name)
        {
            var go=new GameObject(name); go.transform.SetParent(parent,false); go.AddComponent<NetworkIdentity>();
            return go.AddComponent<FactionHQ>();
        }
        static SearchTestAircraft NewAircraft(Transform parent,FactionHQ hq,uint id,Vector3 position)
        {
            var go=new GameObject("Search fixture "+id); go.transform.SetParent(parent,false);
            go.AddComponent<NetworkIdentity>(); var unit=go.AddComponent<SearchTestAircraft>();
            unit.NetworkHQ=hq; unit.persistentID=new PersistentID{Id=id}; unit.transform.position=position; unit.disabled=false;
            unit.radarAlt=10000; unit.maxRadius=5; return unit;
        }
    }
}
