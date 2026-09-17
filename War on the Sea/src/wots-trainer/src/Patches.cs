using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace WoTSTrainer
{
    /// <summary>
    /// Command-points economy (see CampaignInterface.cs in the decompiled source):
    ///   available = commandPoints[0] + commandBonusPoints[0] - commandPointSpent[0]
    /// Purchases are gated by the private CheckSufficientCommandPoints() and, on success,
    /// add to commandPointSpent[0] before SetCommandTotal() re-renders the counter.
    ///
    /// IMPORTANT: commandPoints[0] is shared with the enemy AI budget
    /// (CampaignAI.GetForceAvailable() = commandPoints[0] + bonus - commandPointSpent[1]),
    /// so we must NOT inflate commandPoints[0] — that would unbalance the AI. Instead we
    /// never block purchases (patch below) and keep commandPointSpent[0] at zero whenever
    /// the total is displayed, which is player-only data (enemy uses index 1).
    /// </summary>

    /// <summary>Never let a purchase fail for lack of command points.</summary>
    [HarmonyPatch(typeof(CampaignInterface), "CheckSufficientCommandPoints")]
    public static class Patch_CheckSufficientCommandPoints
    {
        private static bool Prefix(ref bool __result)
        {
            if (!TrainerPlugin.InfiniteCommandPoints)
            {
                return true; // run the original check
            }
            __result = true;
            return false; // skip the original
        }
    }

    /// <summary>Keep the player's spent command points at zero so the pool never depletes.</summary>
    [HarmonyPatch(typeof(CampaignInterface), "SetCommandTotal")]
    public static class Patch_SetCommandTotal
    {
        private static void Prefix()
        {
            if (!TrainerPlugin.InfiniteCommandPoints)
            {
                return;
            }
            if (CampaignManager.instance == null || CampaignManager.instance.campaignData == null)
            {
                return;
            }
            CampaignManager.instance.campaignData.commandPointSpent[0] = 0;
        }
    }

    /// <summary>
    /// Reveal Map: render enemy campaign contacts at full opacity. The game fades a contact's
    /// sprite by its spottedTimer and force-hides it (forceZero) after engagements - with this
    /// prefix every colour update for an enemy unit is pinned to full alpha instead.
    /// </summary>
    [HarmonyPatch(typeof(CampaignInterface), "SetContactColor")]
    public static class Patch_SetContactColor
    {
        private static bool Prefix(MobileMapObject mmo)
        {
            if (!TrainerPlugin.Reveal || mmo == null || mmo.currentFaction == 0)
            {
                return true; // run the original colour logic
            }
            CampaignInterface ci = CampaignInterface.instance;
            if (ci == null || ci.factionColours == null)
            {
                return true;
            }
            SpriteRenderer sr = mmo.GetComponentInChildren<SpriteRenderer>();
            if (sr == null)
            {
                return true;
            }
            Color32 c = ci.factionColours[1]; // enemy colour
            sr.color = new Color32(c.r, c.g, c.b, 255);
            return false; // skip the original fade/hide
        }
    }

    /// <summary>
    /// Exact Intel: with Reveal Map on, rebuild a sea contact's intel from the true unit
    /// prefabs instead of the game's fuzzed version. The game's GetIntelOnSeaGroup shuffles
    /// ship counts between types based on visibility and randomises the reported total; the
    /// tooltip re-fetches it on hover, so this prefix at the single generation point makes
    /// every display (contact panel, tooltip) show the exact composition.
    /// </summary>
    [HarmonyPatch(typeof(CampaignManager), "GetIntelOnSeaGroup")]
    public static class Patch_GetIntelOnSeaGroup
    {
        private static bool Prefix(MobileMapObject mmo, ref List<string> __result)
        {
            if (!TrainerPlugin.Reveal || mmo == null || mmo.unitPrefabs == null || mmo.unitPrefabs.Count == 0)
            {
                return true; // run the original (fuzzed) intel
            }
            CampaignManager cm = CampaignManager.instance;
            if (cm == null || LanguageManager.instance == null || CampaignInterface.instance == null)
            {
                return true;
            }
            try
            {
                __result = BuildExactIntel(cm, mmo);
                return false;
            }
            catch (Exception)
            {
                return true; // on any edge case, fall back to the game's own intel
            }
        }

        private static List<string> BuildExactIntel(CampaignManager cm, MobileMapObject mmo)
        {
            List<string> list = new List<string>();
            int[] designations = cm.GetShipDesignations(mmo); // exact counts per unit type

            // Same traversal order the game uses for the composition text.
            UnitSubType[] order = new UnitSubType[]
            {
                UnitSubType.Aircraft_Carrier,
                UnitSubType.Light_Carrier,
                UnitSubType.Battleship,
                UnitSubType.Battlecruiser,
                UnitSubType.Heavy_Cruiser,
                UnitSubType.Light_Cruiser,
                UnitSubType.Merchant,
                UnitSubType.Oiler,
                UnitSubType.Destroyer,
                UnitSubType.Destroyer_Escort,
                UnitSubType.Submarine
            };

            int totalListed = 0;
            int typesShown = 0;
            string breakdown = string.Empty;
            string spriteIndex = "0";
            for (int k = 0; k < order.Length; k++)
            {
                int count = designations[(int)order[k]];
                if (count <= 0)
                {
                    continue;
                }
                breakdown = breakdown + count + " " + LanguageManager.instance.unitTypeDisplayAbbreviations[(int)order[k]] + ", ";
                if (totalListed == 0)
                {
                    spriteIndex = cm.GetSpriteIndexFromShipSubtype(order[k]).ToString();
                }
                totalListed += count;
                typesShown++;
                if (typesShown == 2)
                {
                    break;
                }
            }

            int total = totalListed;
            if (totalListed == 0)
            {
                // No recognised types - mirror the original's fallback.
                total = 1;
                int fallback = UnityEngine.Random.Range(0, mmo.unitPrefabs.Count);
                breakdown = breakdown + "1 " + LanguageManager.instance.unitTypeDisplayAbbreviations[(int)EngagementManager.instance.unitDataDictionary[mmo.unitPrefabs[fallback]].unitSubtype] + " ,";
            }
            total = Mathf.Clamp(total, 1, 10);

            string[] template = LanguageManager.instance.GetDictionaryString(LanguageManager.generalDictionary, "IntelSea").Split('|');
            string totalLine = total + " " + template[0];
            if (designations[10] + designations[9] > 3)
            {
                totalLine = totalLine + " " + template[4];
            }

            list.Add(totalLine);                                              // [0] ship count
            list.Add(template[1] + " course");                                // [1]
            list.Add(CampaignInterface.instance.GetMobileSeaSpeedDisplay(mmo.speed)); // [2]
            list.Add(spriteIndex);                                            // [3] sprite index
            list.Add(template[3] + " " + breakdown.Substring(0, breakdown.Length - 2)); // [4] composition

            mmo.intelData = list;

            // Mirror the original SetSeaGroupSpriteFromIntel call (dominant-type sprite).
            SpriteRenderer sr = mmo.GetComponentInChildren<SpriteRenderer>();
            if (sr != null && CampaignInterface.instance.mobileMapSprites != null)
            {
                int idx;
                if (int.TryParse(spriteIndex, out idx) && idx >= 0 && idx < CampaignInterface.instance.mobileMapSprites.Length)
                {
                    sr.sprite = CampaignInterface.instance.mobileMapSprites[idx];
                }
            }
            return list;
        }
    }

    /// <summary>
    /// Tactical battles: with Reveal on, player observers always detect enemy units. This
    /// funnels through the game's own sensor pipeline (detectedBy, ContactUpdate, map
    /// contact numbers), so the enemy never drops off the tactical map or camera and the AI
    /// reacts exactly as if you'd detected them. Enemy observers of player units are left
    /// untouched - the cheat is player-side only.
    /// </summary>
    [HarmonyPatch(typeof(SensorManager), "CanDectectUnit")]
    public static class Patch_CanDectectUnit
    {
        private static bool Prefix(Unit observingUnit, Unit contactUnit, ref bool __result)
        {
            if (TrainerPlugin.Reveal && observingUnit != null && contactUnit != null &&
                observingUnit.faction == Faction.Player && contactUnit.faction != Faction.Player)
            {
                __result = true;
                return false; // skip the range/radar/sonar calculation
            }
            return true; // run the original detection logic
        }
    }
}
