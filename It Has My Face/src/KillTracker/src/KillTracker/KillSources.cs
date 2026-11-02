using System.Collections.Generic;
using DoubleMe.Core.AI;
using DoubleMe.Core.GameAssessment;
using DoubleMe.Core.Human;
using DoubleMe.Core.Player;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace KillTracker
{
    /// <summary>
    /// Feeds the ledger with the player's kills. Only kills the game itself credits to the player count:
    /// the player is the killer, or (for people) the game marked the death as an environment kill.
    /// Single-player only; multiplayer kills belong to whichever player made them.
    /// </summary>
    internal sealed class KillSources : MonoBehaviour
    {
        private const float SaveEverySeconds = 1f;

        // Display names for the game's job ids. A job not listed here shows its raw id.
        private static readonly Dictionary<string, string> JobKinds = new Dictionary<string, string>
        {
            { "OtherClone", "Other clone" },
            { "Police", "Police" },
            { "ElitePolice", "Elite police" },
            { "Cannibal", "Cannibal" },
        };

        internal static event System.Action<string> Killed;

        private WorldAIManager world;
        private UnityAction<AgentAI, GameObject> onAgentKilled;
        private UnityAction<TentacleMonster> onMonsterSpawned;
        private float nextSave;

        internal static string Mode => PlayerData.IsEndlessMode ? "endless" : "story";

        private void Awake()
        {
            onAgentKilled = AgentKilled;
            onMonsterSpawned = MonsterSpawned;
        }

        private void Update()
        {
            WorldAIManager current = WorldAIManager.instance;
            if (current != world)
            {
                // A new WorldAIManager is a new level.
                if (world != null)
                {
                    world.OnAnyAgentKilled.RemoveListener(onAgentKilled);
                    world.OnAnyTentacleMonsterSpawned.RemoveListener(onMonsterSpawned);
                }

                world = current;
                Plugin.Ledger.StartLevel();
                if (world != null)
                {
                    world.OnAnyAgentKilled.AddListener(onAgentKilled);
                    world.OnAnyTentacleMonsterSpawned.AddListener(onMonsterSpawned);
                }
            }

            if (Time.unscaledTime >= nextSave)
            {
                nextSave = Time.unscaledTime + SaveEverySeconds;
                Plugin.Ledger.SaveIfDirty();
            }
        }

        private void OnApplicationQuit()
        {
            Plugin.Ledger.SaveIfDirty();
        }

        internal static void Count(string kind)
        {
            if (PlayerData.IsMultiplayer)
            {
                return;
            }

            DifficultyManager difficulty = DifficultyManager.instance;
            int? level = difficulty != null ? difficulty.FinishedLevelCount + 1 : (int?)null;
            Plugin.Ledger.Record(kind, Mode, SceneManager.GetActiveScene().name, level);
            Killed?.Invoke(kind);
        }

        internal static bool IsPlayer(GameObject source) => source != null && source.CompareTag("Player");

        // Same rule the game uses for its own kill statistics in WorldAIManager.AgentDied.
        private static void AgentKilled(AgentAI agent, GameObject source)
        {
            if (agent == null || !(IsPlayer(source) || (source == null && agent.HasData("EnvironmentKill"))))
            {
                return;
            }

            Count(HumanKind(agent));
        }

        private static string HumanKind(AgentAI agent)
        {
            if (agent.IsTarget)
            {
                return "Clone";
            }

            if (agent.MyJob == null)
            {
                return "Innocent";
            }

            string job = agent.MyJob.JobName;
            return JobKinds.TryGetValue(job, out string kind) ? kind : job;
        }

        private static void MonsterSpawned(TentacleMonster monster)
        {
            if (monster == null)
            {
                return;
            }

            // OnDeath2 fires on every hit below zero health once the round is decided, so count it once.
            bool counted = false;
            monster.OnDeath2.AddListener((damage, killer, constantDamage) =>
            {
                if (!counted && IsPlayer(killer))
                {
                    counted = true;
                    Count("Tentacle monster");
                }
            });
        }
    }

    // Worms raise no event of their own, so watch their health cross zero.
    [HarmonyPatch(typeof(WormAI), nameof(WormAI.Hurt))]
    internal static class WormKillPatch
    {
        private static void Prefix(WormAI __instance, out bool __state)
        {
            __state = !__instance.IsDead();
        }

        private static void Postfix(WormAI __instance, GameObject killer, bool __state)
        {
            if (__state && __instance.IsDead() && KillSources.IsPlayer(killer))
            {
                KillSources.Count("Worm");
            }
        }
    }

    // The game calls this when a run ends (result screen, new chapter); the next kill starts a fresh run.
    [HarmonyPatch(typeof(DifficultyManager), nameof(DifficultyManager.ResetDifficulty))]
    internal static class RunResetPatch
    {
        private static void Postfix()
        {
            if (!PlayerData.IsMultiplayer)
            {
                Plugin.Ledger.StartRun(KillSources.Mode);
            }
        }
    }
}
