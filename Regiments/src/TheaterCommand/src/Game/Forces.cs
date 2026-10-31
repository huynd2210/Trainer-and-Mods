using System;
using System.Collections.Generic;
using System.Linq;
using birdseye;
using birdseye.Localization;
using birdseye.Regiments;
using birdseye.Regiments.GameModes;
using birdseye.Regiments.SaveStates;
using TheaterCommand.Core;
using UnityEngine;

namespace TheaterCommand.Game
{
    /// <summary>
    /// Ties theater formations to real forces. A Blue formation is a full War Path roster: losses
    /// and refits change its platoon stacks, and its theater strength is read back from them. Red
    /// formations stay abstract; their battlegroup decides who shows up when a battle is fought.
    /// </summary>
    public class GameForces : IForceHooks
    {
        public const float FullStrength = 1000f;

        private readonly Campaign campaign;
        private readonly ProcOpSaveSlotRecord slot;
        private readonly AbstractForceHooks abstractHooks = new AbstractForceHooks();

        public GameForces(Campaign campaign, ProcOpSaveSlotRecord slot)
        {
            this.campaign = campaign;
            this.slot = slot;
        }

        public static ProcOpPlayerSaveState Load(BlueForce bf) => JsonUtility.FromJson<ProcOpPlayerSaveState>(bf.playerSave);

        public static void Store(BlueForce bf, ProcOpPlayerSaveState ps) => bf.playerSave = JsonUtility.ToJson(ps);

        /// <summary>Price of everything still in the roster, deployed or not.</summary>
        public static float Value(ProcOpPlayerSaveState ps)
        {
            float v = 0f;
            List<CustomTaskForceParameters> tfs = ps.forceSave.GetBoughtTFs();
            for (int i = 0; i < tfs.Count && i < ps.forceSave.tfStacks.Length; i++)
            {
                StackSaveState[] stacks = ps.forceSave.tfStacks[i]?.stacks;
                if (stacks == null)
                {
                    continue;
                }
                for (int j = 0; j < stacks.Length && j < tfs[i].pltArticles.Count; j++)
                {
                    StackSaveState s = stacks[j];
                    if (s == null || s.stackCount < 0)
                    {
                        continue;
                    }
                    v += (s.stackCount + Math.Max(0, s.deployedCount)) * tfs[i].pltArticles[j].price;
                }
            }
            return v;
        }

        public static void SyncStrength(Formation f, BlueForce bf)
        {
            f.strength = bf.fullValue <= 0f ? 0f : FullStrength * Value(Load(bf)) / bf.fullValue;
        }

        public static IEnumerable<StackSaveState> LimitedStacks(ProcOpPlayerSaveState ps)
        {
            int count = ps.forceSave.boughtTFs.Count;
            for (int i = 0; i < count && i < ps.forceSave.tfStacks.Length; i++)
            {
                StackSaveState[] stacks = ps.forceSave.tfStacks[i]?.stacks;
                if (stacks == null)
                {
                    continue;
                }
                foreach (StackSaveState s in stacks.Where(s => s != null && s.stackCount >= 0))
                {
                    yield return s;
                }
            }
        }

        public void ApplyLoss(Theater t, Formation f, float fraction, Battle b)
        {
            BlueForce bf = f.side == Side.Blue ? campaign.Force(f.id) : null;
            if (bf == null)
            {
                abstractHooks.ApplyLoss(t, f, fraction, b);
                return;
            }
            if (b == null || b.foughtBy != f.id)
            {
                ProcOpPlayerSaveState ps = Load(bf);
                System.Random rng = Dice.For(t, 5003, f.id + (b != null ? b.id * 7919 : 0));
                foreach (StackSaveState s in LimitedStacks(ps))
                {
                    int lost = (int)Math.Floor(s.stackCount * fraction + rng.NextDouble());
                    lost = Math.Min(lost, s.stackCount);
                    s.stackCount -= lost;
                    s.totalLosses += lost;
                }
                Store(bf, ps);
            }
            SyncStrength(f, bf);
        }

        public void Refit(Theater t, Formation f)
        {
            BlueForce bf = f.side == Side.Blue ? campaign.Force(f.id) : null;
            if (bf == null)
            {
                abstractHooks.Refit(t, f);
                return;
            }
            // Same as the War Path "Rest" order: a few platoons back to every stack, some supply.
            ProcOpPlayerSaveState ps = Load(bf);
            System.Random rng = Dice.For(t, 5011, f.id);
            foreach (StackSaveState s in LimitedStacks(ps))
            {
                s.stackCount = Mathf.Clamp(s.stackCount + 1 + Mathf.FloorToInt(s.maxSize / 8f), 0, s.maxSize);
            }
            ps.SPs += 150 + rng.Next(6) * 10;
            Store(bf, ps);
            SyncStrength(f, bf);
        }

        public Formation Reinforce(Theater t, Side side, int cell)
        {
            if (side == Side.Red)
            {
                Formation r = abstractHooks.Reinforce(t, side, cell);
                CampaignFactory.NameRed(campaign, slot, r, t.formations.Count(x => x.side == Side.Red) - 1);
                r.maxMp = r.mp = Combat.Kinds[r.kind].mp;
                return r;
            }
            ProcOpForceList.ForceRecord rec = CampaignFactory.PickReinforcement(campaign, slot);
            var f = new Formation { id = t.nextFormationId++, side = Side.Blue, cell = cell };
            if (!CampaignFactory.Raise(campaign, slot, rec, f))
            {
                return abstractHooks.Reinforce(t, side, cell);
            }
            f.maxMp = f.mp = Combat.Kinds[f.kind].mp;
            t.formations.Add(f);
            return f;
        }
    }

    /// <summary>Builds a campaign from the War Path run the player just set up.</summary>
    public static class CampaignFactory
    {
        public const int BlueFormations = 12;
        public const int RedFormations = 14;

        public static readonly eProcOpMissionTypes[] NeededMissions = { eProcOpMissionTypes.Attack, eProcOpMissionTypes.Defend };

        public static Campaign Create(ProcOpSaveSlotRecord slot, ProcOpForceList.ForceRecord playerRecord, int turns)
        {
            var c = new Campaign { runSeed = slot.procOpSave.rngSeed };
            var rng = new System.Random(slot.procOpSave.rngSeed);
            List<ProcOpForceList.ForceRecord> all = ProcOpForceList.active.forces;

            // Maps that can host both kinds of theater battle; each town is fought over on one.
            // Maps named after a place lend the biggest towns their names; the rest get local ones.
            List<int> maps = Enumerable.Range(0, ProcOpMapList.active.mapList.Count)
                .Where(i => NeededMissions.All(m => ProcOpMapList.active.mapList[i].map.validMissionGenRecords.Exists(r => r.missionDesc.missionType == m)))
                .OrderBy(_ => rng.Next()).ToList();
            var usedNames = new HashSet<string>();
            List<int> placeMaps = maps.Where(m => Classify.IsPlaceName(MapName(m)) && usedNames.Add(MapName(m))).ToList();
            List<int> otherMaps = maps.Except(placeMaps).DefaultIfEmpty(maps[0]).ToList();

            // Regiments for the player's side: their own first, then the same nation, then the rest
            // of the coalition, in a fixed but campaign-specific order.
            List<ProcOpForceList.ForceRecord> blue = new List<ProcOpForceList.ForceRecord> { playerRecord };
            blue.AddRange(all.Where(r => r != playerRecord && r.force.coalition == playerRecord.force.coalition)
                .OrderBy(r => r.force.nation == playerRecord.force.nation ? 0 : 1).ThenBy(_ => rng.Next()).Take(BlueFormations - 1));

            var settings = new GeneratorSettings
            {
                maxTurns = turns,
                blueFormations = Math.Min(BlueFormations, blue.Count),
                redFormations = RedFormations,
                townName = i => i < placeMaps.Count ? MapName(placeMaps[i]) : TownNames.Default(i - placeMaps.Count),
            };
            Theater t = TheaterGenerator.Generate(slot.procOpSave.rngSeed, settings);
            t.aiAggression = slot.runData.difficulty == eModeDifficulty.Easy ? 1.1f : slot.runData.difficulty == eModeDifficulty.Medium ? 1.3f : 1.5f;
            c.theater = t;
            foreach (Town town in t.towns)
            {
                town.mapIndex = town.id < placeMaps.Count ? placeMaps[town.id] : otherMaps[(town.id - placeMaps.Count) % otherMaps.Count];
            }

            // The generator deploys formations in index order: the first holds the HQ, the next
            // ones the front. The regiment the player picked goes to the front.
            List<Formation> blues = t.formations.Where(f => f.side == Side.Blue).ToList();
            for (int i = 0; i < blues.Count; i++)
            {
                int r = i == 0 ? 1 : i == 1 ? 0 : i;
                if (r == 0)
                {
                    // The run already raised the player's regiment; use exactly that roster.
                    ProcOpPlayerSaveState ps = slot.srcPhaseSaves[0].playerSave;
                    var bf = new BlueForce { formation = blues[i].id, fullValue = GameForces.Value(ps), playerSave = JsonUtility.ToJson(ps) };
                    c.forces.Add(bf);
                    blues[i].forceRef = all.IndexOf(playerRecord);
                    Name(blues[i], RegimentName(playerRecord), playerRecord);
                    blues[i].strength = blues[i].maxStrength = GameForces.FullStrength;
                }
                else if (!Raise(c, slot, blue[r], blues[i]))
                {
                    Mod.Log("could not raise " + blue[r].force.name + "; it stays abstract");
                }
            }
            List<Formation> reds = t.formations.Where(f => f.side == Side.Red).ToList();
            for (int i = 0; i < reds.Count; i++)
            {
                NameRed(c, slot, reds[i], i);
            }
            foreach (Formation f in t.formations)
            {
                f.maxMp = f.mp = Combat.Kinds[f.kind].mp;
            }
            t.Log("Theater Command begins");
            return c;
        }

        // Name a formation and read its NATO branch and size from the name.
        private static void Name(Formation f, string name, ProcOpForceList.ForceRecord rec)
        {
            f.name = name;
            string all = name + " " + (rec != null ? rec.force.name : "");
            f.kind = Classify.Kind(all);
            f.size = Classify.Size(all);
        }
        public static bool Raise(Campaign c, ProcOpSaveSlotRecord slot, ProcOpForceList.ForceRecord rec, Formation f)
        {
            ProcOpRunDataSaveState rd = slot.runData;
            if (!new ProcOpForceConverter(rd.GetUnitCountMod(), rd.UseCampaignPrices == 1).TryConvertForce(rec, out CustomForceSetupParameters conv))
            {
                return false;
            }
            var temp = new ProcOpPhaseSaveState();
            temp.Init(conv, slot.procOpSave);
            ProcOpForcePostProcesser.PostProcessForce(temp, rd.regCondition);
            c.forces.Add(new BlueForce { formation = f.id, fullValue = GameForces.Value(temp.playerSave), playerSave = JsonUtility.ToJson(temp.playerSave) });
            f.forceRef = ProcOpForceList.active.forces.IndexOf(rec);
            Name(f, RegimentName(rec), rec);
            f.strength = f.maxStrength = GameForces.FullStrength;
            return true;
        }

        public static ProcOpForceList.ForceRecord PickReinforcement(Campaign c, ProcOpSaveSlotRecord slot)
        {
            List<ProcOpForceList.ForceRecord> all = ProcOpForceList.active.forces;
            ForceSetupParameters own = slot.runData.GetSourceForce();
            var inUse = new HashSet<int>(c.theater.formations.Where(f => f.side == Side.Blue && !f.destroyed).Select(f => f.forceRef));
            List<ProcOpForceList.ForceRecord> pool = all.Where(r => r.force.coalition == own.coalition).ToList();
            List<ProcOpForceList.ForceRecord> fresh = pool.Where(r => !inUse.Contains(all.IndexOf(r))).ToList();
            var rng = Dice.For(c.theater, 5021);
            List<ProcOpForceList.ForceRecord> from = fresh.Count > 0 ? fresh : pool;
            return from[rng.Next(from.Count)];
        }

        public static void NameRed(Campaign c, ProcOpSaveSlotRecord slot, Formation f, int index)
        {
            FrontForceParameters eny = EnemyFront(slot);
            int n = eny.natRecords.Count;
            f.forceRef = index % n;
            FrontForceParameters.BattlegroupRecord bg = eny.natRecords[f.forceRef];
            Name(f, Ordinal(index / n + 1) + " " + LocalizationManager.GetString(bg.atkFS.locID) + " Battlegroup", null);
            f.kind = Classify.PactKind(index);
            c.red.RemoveAll(r => r.formation == f.id);
            c.red.Add(new RedForce { formation = f.id });
        }

        public static FrontForceParameters EnemyFront(ProcOpSaveSlotRecord slot) => GUIDManager.GuidToSO<FrontForceParameters>(slot.runData.enyForceGuid);

        public static string RegimentName(ProcOpForceList.ForceRecord rec) => LocalizationManager.GetString(rec.force.mainBody.nameLocID);

        public static string MapName(int mapIndex)
        {
            string n = LocalizationManager.GetString(ProcOpMapList.active.mapList[mapIndex].map.mapDesc.nameLocID);
            return n.Replace(" (Alt)", "").Trim();
        }

        private static string Ordinal(int n)
        {
            int m = n % 100;
            string suffix = m >= 11 && m <= 13 ? "th" : (n % 10) == 1 ? "st" : (n % 10) == 2 ? "nd" : (n % 10) == 3 ? "rd" : "th";
            return n + suffix;
        }
    }
}
