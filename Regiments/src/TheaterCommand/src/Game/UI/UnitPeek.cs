using System.Collections.Generic;
using System.Linq;
using birdseye.Regiments;
using birdseye.Regiments.GameModes;
using birdseye.Regiments.SaveStates;
using HarmonyLib;
using TheaterCommand.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheaterCommand.Game.UI
{
    /// <summary>
    /// What is inside a formation, unfolded beside its counter while the pointer rests on it, in
    /// the game's own platoon symbols. The player's formations list every platoon with how many
    /// are left. Enemy battlegroups have no fixed roster (the game draws their mix per battle), so
    /// they list the platoon types they field, most common first.
    /// </summary>
    public static class UnitPeek
    {
        private const int Columns = 5;
        private const float CellW = 100f;
        private const float CellH = 76f;
        private const float Pad = 14f;
        private const float HeadH = 64f;
        private const float MinWidth = 380f;
        private const int MaxEnemyTypes = 10;

        private static readonly Dictionary<UnitKind, string> KindNames = new Dictionary<UnitKind, string>
        {
            { UnitKind.Mechanized, "Mechanised" },
            { UnitKind.Armour, "Armoured" },
            { UnitKind.Infantry, "Infantry" },
            { UnitKind.Recon, "Reconnaissance" },
            { UnitKind.Airborne, "Airborne" },
        };

        private static readonly Dictionary<UnitSize, string> SizeNames = new Dictionary<UnitSize, string>
        {
            { UnitSize.Battalion, "battalion" },
            { UnitSize.Regiment, "regiment" },
            { UnitSize.Brigade, "brigade" },
            { UnitSize.Division, "division" },
        };

        private struct Entry
        {
            public PlatoonParameters plt;
            // Platoons left and the stack's full size; -1 where the game keeps no count.
            public int count;
            public int max;
            public eVeterancy xp;
        }

        // Rosters by the saved roster they were read from, enemy pools by battlegroup: both only
        // change when that source does.
        private static readonly Dictionary<string, List<Entry>> rosters = new Dictionary<string, List<Entry>>();
        private static readonly Dictionary<PresetForceSetup, List<Entry>> pools = new Dictionary<PresetForceSetup, List<Entry>>();

        /// <summary>The card for a formation, or null if there is nothing to show inside it.</summary>
        public static RectTransform Build(Transform layer, Theater t, Formation f, Campaign c, ProcOpSaveSlotRecord slot)
        {
            string head = KindNames[f.kind] + " " + SizeNames[f.size] + "   ·   strength " + Mathf.RoundToInt(f.strength01 * 100f) + "%";
            BlueForce bf = f.side == Side.Blue ? c.Force(f.id) : null;
            if (bf != null)
            {
                List<Entry> roster = Roster(bf);
                int left = roster.Where(e => e.count > 0).Sum(e => e.count);
                return Draw(layer, f.name, head + "   ·   " + left + " platoons", roster);
            }
            if (f.side == Side.Red)
            {
                FrontForceParameters eny = CampaignFactory.EnemyFront(slot);
                PresetForceSetup fs = eny.natRecords[f.forceRef % eny.natRecords.Count].atkFS;
                return Draw(layer, f.name, head + "   ·   fields, most common first", Pool(fs));
            }
            return null;
        }

        private static List<Entry> Roster(BlueForce bf)
        {
            if (rosters.TryGetValue(bf.playerSave, out List<Entry> cached))
            {
                return cached;
            }
            ProcOpPlayerSaveState ps = GameForces.Load(bf);
            var list = new List<Entry>();
            List<CustomTaskForceParameters> tfs = ps.forceSave.GetBoughtTFs();
            for (int i = 0; i < tfs.Count && i < ps.forceSave.tfStacks.Length; i++)
            {
                StackSaveState[] stacks = ps.forceSave.tfStacks[i]?.stacks;
                for (int j = 0; stacks != null && j < stacks.Length && j < tfs[i].pltArticles.Count; j++)
                {
                    StackSaveState s = stacks[j];
                    if (s == null)
                    {
                        continue;
                    }
                    bool counted = s.maxSize >= 0;
                    list.Add(new Entry { plt = tfs[i].pltArticles[j].platoonParameters, count = counted ? s.totalCount : -1, max = counted ? s.maxSize : -1, xp = s.xpLevel });
                }
            }
            if (rosters.Count > 64)
            {
                rosters.Clear();
            }
            rosters[bf.playerSave] = list;
            return list;
        }

        private static List<Entry> Pool(PresetForceSetup fs)
        {
            if (pools.TryGetValue(fs, out List<Entry> cached))
            {
                return cached;
            }
            var weights = new Dictionary<PlatoonParameters, float>();
            foreach (SpawnArticle a in SpawnLists(fs).SelectMany(sl => sl.GetSpawnArticles()))
            {
                if (a.platoon != null)
                {
                    weights[a.platoon] = (weights.TryGetValue(a.platoon, out float w) ? w : 0f) + a.weigth;
                }
            }
            List<Entry> list = weights.OrderByDescending(kv => kv.Value).Take(MaxEnemyTypes)
                .Select(kv => new Entry { plt = kv.Key, count = -1, max = -1, xp = eVeterancy.Regular }).ToList();
            pools[fs] = list;
            return list;
        }

        // A battlegroup is either one preset force or a random pick among several.
        private static IEnumerable<SpawnList> SpawnLists(PresetForceSetup fs)
        {
            if (fs is RandomPresetForce r)
            {
                return r.forces.Where(x => x != null && x.isAllowed).SelectMany(SpawnLists);
            }
            return (AccessTools.Field(typeof(PresetForceSetup), "spawnLists").GetValue(fs) as List<SpawnList>)?.Where(x => x != null) ?? Enumerable.Empty<SpawnList>();
        }

        private static RectTransform Draw(Transform layer, string title, string sub, List<Entry> entries)
        {
            int cols = Mathf.Clamp(entries.Count, 1, Columns);
            int rows = Mathf.Max(1, Mathf.CeilToInt(entries.Count / (float)cols));
            var size = new Vector2(Mathf.Max(MinWidth, cols * CellW + 2f * Pad), HeadH + rows * CellH + Pad);
            RectTransform card = Style.Rect(layer, "UnitPeek").Place(Vector2.zero, size);
            // Opaque, like the battle tags: the map's roads and counters must not show through.
            Image bg = Style.Image(card, "Bg", "UIElement_Square", new Color(0.08f, 0.085f, 0.09f, 0.97f), sliced: true);
            ((RectTransform)bg.transform).Fill();

            TextMeshProUGUI tl = Style.Label(card, "Title", title, 24f, Style.Gold, isLight: true, align: TextAlignmentOptions.Left, style: FontStyles.SmallCaps);
            tl.overflowMode = TextOverflowModes.Ellipsis;
            Top((RectTransform)tl.transform, size, Pad - 4f, 30f);
            TextMeshProUGUI sl = Style.Label(card, "Sub", sub, 16f, Style.TextDim, align: TextAlignmentOptions.Left);
            Top((RectTransform)sl.transform, size, Pad + 26f, 22f);

            for (int i = 0; i < entries.Count; i++)
            {
                var at = new Vector2(-size.x * 0.5f + Pad + (i % cols + 0.5f) * CellW, size.y * 0.5f - HeadH - (i / cols + 0.5f) * CellH);
                Platoon(card, entries[i], at);
            }
            return card;
        }

        private static void Top(RectTransform rt, Vector2 card, float y, float h)
        {
            rt.Place(new Vector2(0f, card.y * 0.5f - y - h * 0.5f), new Vector2(card.x - 2f * Pad, h));
        }

        // One platoon as the regiment screen draws it: the symbol on its black plate, veterancy
        // above, platoons left beside it, the name beneath.
        private static void Platoon(RectTransform card, Entry e, Vector2 at)
        {
            PlatoonParameters p = e.plt;
            bool gone = e.max >= 0 && e.count <= 0;
            float alpha = gone ? 0.35f : 1f;
            var iconAt = at + new Vector2(e.count >= 0 ? -16f : 0f, 14f);
            var iconSize = new Vector2(46f, 32f);

            Image plate = Style.Image(card, "Plate", null, new Color(0f, 0f, 0f, alpha));
            plate.sprite = p.isCompany && !p.isPseudoCompany ? PlatoonIcons.active.bkgCoy : PlatoonIcons.active.bkg;
            plate.preserveAspect = true;
            ((RectTransform)plate.transform).Place(iconAt, iconSize);
            Image icon = Style.Image(card, "Icon", null, Style.WithAlpha(Color.white, alpha));
            icon.sprite = PlatoonIcons.active.GetByType(p.displayType);
            icon.preserveAspect = true;
            ((RectTransform)icon.transform).Place(iconAt, iconSize);

            if (e.xp > eVeterancy.Regular)
            {
                Image xp = Style.Image(card, "XP", null, PlatoonIcons.active.GetXPColor(e.xp));
                xp.sprite = PlatoonIcons.active.GetXPSprite(e.xp);
                xp.preserveAspect = true;
                ((RectTransform)xp.transform).Place(iconAt + new Vector2(-iconSize.x * 0.5f + 6f, iconSize.y * 0.5f + 2f), new Vector2(14f, 12f));
            }

            if (e.count >= 0)
            {
                Color col = e.count <= 0 ? Style.Red : e.count < e.max ? Style.Gold : Style.Text;
                string max = "<size=70%><color=#" + ColorUtility.ToHtmlStringRGB(Style.TextDim) + ">/" + e.max + "</color></size>";
                TextMeshProUGUI n = Style.Label(card, "Count", e.count + max, 20f, col, align: TextAlignmentOptions.Left, style: FontStyles.Bold);
                ((RectTransform)n.transform).Place(iconAt + new Vector2(iconSize.x * 0.5f + 4f + 22f, 0f), new Vector2(44f, 26f));
            }

            string name = p.isCarrier ? p.displayName + " - " + p.carryPlt.displayName : p.displayName;
            TextMeshProUGUI l = Style.Label(card, "Name", name, 13f, Style.WithAlpha(Style.Text, gone ? 0.45f : 0.9f));
            l.enableWordWrapping = true;
            l.overflowMode = TextOverflowModes.Ellipsis;
            l.lineSpacing = -18f;
            ((RectTransform)l.transform).Place(at + new Vector2(0f, -20f), new Vector2(CellW - 6f, 32f));
        }
    }
}
