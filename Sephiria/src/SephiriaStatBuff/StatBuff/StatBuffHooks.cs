using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SephiriaStatBuff
{
    public static class StatBuffHooks
    {
        private static readonly string[] AllowedStats = {
            "AllDamageBonus", "AttackSpeed", "DamageReduction", "HPRegen",
            "MPRegen", "Luck", "AP", "FinalMP", "Critical",
            "CriticalDamageBonus", "Evasion", "CooldownRecoverySpeed"
        };

        private static readonly Dictionary<string, int> Desired = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, int> Applied = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private static PlayerAvatar? activeAvatar;
        private static bool loaded;
        private static bool enabled;

        public static void Tick(PlayerLocalDataStorage storage)
        {
            if (storage == null || !storage.isOwned || !storage.isServer)
                return;

            if (!loaded)
                LoadConfig();

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f8Key.wasPressedThisFrame)
            {
                enabled = !enabled;
                Debug.Log("[Stat Buff] " + (enabled ? "ON" : "OFF"));
            }
            if (keyboard != null && keyboard.f9Key.wasPressedThisFrame)
                LoadConfig();

            var avatar = storage.Avatar;
            if (avatar != activeAvatar)
            {
                // A previous run's player object may already be destroyed.
                activeAvatar = avatar;
                Applied.Clear();
            }
            if (avatar == null)
                return;

            foreach (string stat in AllowedStats)
            {
                int current = Applied.TryGetValue(stat, out int applied) ? applied : 0;
                int target = enabled && Desired.TryGetValue(stat, out int desired) ? desired : 0;
                int delta = target - current;
                if (delta == 0)
                    continue;

                avatar.AddCustomStat(stat, delta);
                Applied[stat] = target;
            }
        }

        private static void LoadConfig()
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "SephiriaStatBuff.cfg"));
            try
            {
                var next = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                bool nextEnabled = false;
                foreach (string raw in File.ReadAllLines(path))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";"))
                        continue;
                    int equals = line.IndexOf('=');
                    if (equals < 1)
                        throw new FormatException("Expected name=value: " + line);
                    string key = line.Substring(0, equals).Trim();
                    string value = line.Substring(equals + 1).Trim();
                    if (key.Equals("Enabled", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!bool.TryParse(value, out nextEnabled))
                            throw new FormatException("Enabled must be true or false.");
                    }
                    else if (Array.Exists(AllowedStats, s => s.Equals(key, StringComparison.OrdinalIgnoreCase)))
                    {
                        if (!int.TryParse(value, out int amount) || amount < 0 || amount > 1000)
                            throw new FormatException(key + " must be an integer from 0 to 1000.");
                        next[key] = amount;
                    }
                    else
                        throw new FormatException("Unknown setting: " + key);
                }
                Desired.Clear();
                foreach (var item in next) Desired[item.Key] = item.Value;
                enabled = nextEnabled;
                loaded = true;
                Debug.Log("[Stat Buff] Config loaded; " + (enabled ? "ON" : "OFF"));
            }
            catch (Exception error)
            {
                loaded = true;
                Debug.LogError("[Stat Buff] Config error; keeping current settings: " + error.Message);
            }
        }
    }
}
