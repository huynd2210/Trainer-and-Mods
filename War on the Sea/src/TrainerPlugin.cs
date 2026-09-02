using System;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using WorldMapStrategyKit;

namespace WoTSTrainer
{
    /// <summary>
    /// War on the Sea trainer.
    ///
    /// Cheats:
    ///   F1  Infinite Command Points  - campaign purchases never fail, pool never depletes.
    ///   F2  Reveal All               - removes fog of war everywhere:
    ///                                    * campaign map: all enemy units visible, always
    ///                                      spotted, exact composition in tooltips;
    ///                                    * tactical battles: all enemy units detected and
    ///                                      identified (map icons, targeting, 3D camera).
    ///
    /// New cheats slot in behind this same hotkey/status-line pattern: add a static bool
    /// here, a hotkey in Update(), a line in OnGUI(), and a patch class in Patches.cs.
    /// </summary>
    [BepInPlugin("com.wots.trainer", "War on the Sea Trainer", "1.3.0")]
    public class TrainerPlugin : BaseUnityPlugin
    {
        /// <summary>Infinite Command Points: purchases never fail and the pool never depletes.</summary>
        public static bool InfiniteCommandPoints;

        /// <summary>Reveal All: campaign map fog of war removed and all battle enemies detected.</summary>
        public static bool Reveal;

        private static readonly KeyCode InfiniteCommandPointsKey = KeyCode.F1;
        private static readonly KeyCode RevealKey = KeyCode.F2;

        private GUIStyle statusStyle;
        private bool styleInitialised;

        // Periodic reveal pass cadence.
        private float revealTimer;
        private const float RevealInterval = 0.5f;

        // Original "unspotted" colour, so toggling Reveal off restores the campaign fog.
        private bool noContactCaptured;
        private Color32 savedNoContactColor;

        private void Awake()
        {
            var harmony = new Harmony("com.wots.trainer");
            harmony.PatchAll();
            Logger.LogInfo("[WoTSTrainer] Trainer loaded. F1 = Infinite Command Points, F2 = Reveal All.");
        }

        private void Update()
        {
            if (Input.GetKeyDown(InfiniteCommandPointsKey))
            {
                InfiniteCommandPoints = !InfiniteCommandPoints;
                Logger.LogInfo("[WoTSTrainer] Infinite Command Points " + (InfiniteCommandPoints ? "ON" : "OFF"));
            }
            if (Input.GetKeyDown(RevealKey))
            {
                ToggleReveal();
            }

            if (Reveal)
            {
                revealTimer += Time.deltaTime;
                if (revealTimer >= RevealInterval)
                {
                    revealTimer = 0f;
                    RevealPass();
                    CombatRevealPass();
                }
            }
        }

        private void ToggleReveal()
        {
            Reveal = !Reveal;
            Logger.LogInfo("[WoTSTrainer] Reveal All (no fog of war) " + (Reveal ? "ON" : "OFF"));
            if (!Reveal)
            {
                // Restore the game's own unspotted colour so campaign fog returns when toggled off.
                if (CampaignInterface.instance != null && noContactCaptured)
                {
                    CampaignInterface.instance.noContactColor = savedNoContactColor;
                }
                noContactCaptured = false;
            }
        }

        /// <summary>
        /// Campaign map reveal: keeps every enemy unit fully spotted, in the contact list,
        /// intel-populated and rendered at full opacity. Runs while Reveal is on.
        /// </summary>
        private void RevealPass()
        {
            CampaignManager cm = CampaignManager.instance;
            if (cm == null || cm.campaignData == null || cm.otherMobile == null) return;
            CampaignInterface ci = CampaignInterface.instance;
            if (ci == null || ci.factionColours == null) return;

            Color32 c = ci.factionColours[1]; // enemy colour
            Color32 visible = new Color32(c.r, c.g, c.b, 255);

            if (!noContactCaptured)
            {
                savedNoContactColor = ci.noContactColor;
                noContactCaptured = true;
            }
            // The game writes this colour directly when a contact is unspotted (e.g. on map
            // load, unit creation) - make it fully visible too.
            ci.noContactColor = visible;

            for (int i = 0; i < cm.otherMobile.Count; i++)
            {
                GameObjectAnimator goa = cm.otherMobile[i];
                if (goa == null) continue;
                MobileMapObject mmo = goa.mobileMapObject;
                if (mmo == null || mmo.currentFaction == 0) continue;

                // Immune to the game's spotting decay (Check5Min) - always fully spotted.
                mmo.spottedTimer = Utilities.GetMaxSpotTime(mmo);
                if (!cm.spottedContacts.Contains(mmo))
                {
                    cm.spottedContacts.Add(mmo);
                }
                // Populate intel once so real unit types are displayed instead of vague contacts.
                if (mmo.type == 0 && (mmo.intelData == null || mmo.intelData.Count < 5))
                {
                    try
                    {
                        cm.GetIntelOnSeaGroup(mmo);
                    }
                    catch (Exception)
                    {
                        // Intel is a nicety; never let it break the reveal pass.
                    }
                }
                SpriteRenderer sr = goa.GetComponentInChildren<SpriteRenderer>();
                if (sr != null)
                {
                    sr.color = visible;
                }
            }
        }

        /// <summary>
        /// Tactical battle reveal: forces every enemy unit to be detected and identified so
        /// they show on the tactical map, can be targeted, and their classes are known. The
        /// SensorManager.CanDectectUnit patch keeps the game's own sensor pipeline from
        /// un-detecting them; this pass makes it instant at battle start and assigns the
        /// contact numbers the game would normally assign.
        /// </summary>
        private void CombatRevealPass()
        {
            EngagementManager em = EngagementManager.instance;
            if (em == null || em.otherUnits == null) return;
            EngagementData ed = EngagementData.instance;

            for (int i = 0; i < em.otherUnits.Count; i++)
            {
                Unit u = em.otherUnits[i];
                if (u == null || u.isDestroyed) continue;

                u.detected = true;
                u.previouslyDetected = true;
                u.identified = true;

                // Number the contact the same way SensorManager.AssignMapName does.
                if (u.detectedIndex == 0 && ed != null && u.mapUnit != null && u.mapUnit.mapUnitText != null)
                {
                    if (u.unitSea != null)
                    {
                        ed.enemySeaDetected++;
                        u.detectedIndex = ed.enemySeaDetected;
                        u.mapUnit.mapUnitText.text = u.detectedIndex + ".";
                    }
                    else if (u.unitAir != null)
                    {
                        ed.enemyAirDetected++;
                        u.detectedIndex = ed.enemyAirDetected;
                        u.mapUnit.mapUnitText.text = u.detectedIndex.ToString();
                    }
                }
            }
        }

        private void OnGUI()
        {
            if (!styleInitialised)
            {
                statusStyle = new GUIStyle(GUI.skin.label);
                statusStyle.fontStyle = FontStyle.Bold;
                styleInitialised = true;
            }
            if (!InfiniteCommandPoints && !Reveal)
            {
                return; // nothing active - keep the screen clean
            }
            statusStyle.normal.textColor = Color.yellow;
            string text = "TRAINER";
            if (InfiniteCommandPoints) text += "   Infinite Command Points [F1]";
            if (Reveal) text += "   Reveal All [F2]";
            GUI.Label(new Rect(10f, 8f, Screen.width - 20f, 22f), text, statusStyle);
        }
    }
}
