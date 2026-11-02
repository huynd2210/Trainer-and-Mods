using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SephiriaTrainer
{
    public static class TrainerHooks
    {
        public static void Tick(PlayerLocalDataStorage storage)
        {
            if (storage == null || !storage.isOwned)
                return;

            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.f6Key.wasPressedThisFrame)
                GiveSapphires(storage);

            if (keyboard.f7Key.wasPressedThisFrame)
                GiveGold(storage);
        }

        private static void GiveSapphires(PlayerLocalDataStorage storage)
        {
            var profile = SaveManager.Current;
            if (profile == null || SaveManager.CurrentRun == null)
            {
                Debug.LogWarning("[Sephiria Trainer] Load a save before giving saphires.");
                return;
            }

            int current = profile.GetInt("Sapphire", 0);
            int amount = Math.Min(1000, int.MaxValue - Math.Max(0, current));
            int updated = Math.Max(0, current) + amount;
            profile.SetInt("Sapphire", updated);
            storage.Networksapphire = updated;
            SaveManager.Save(saveCurrent: true, saveCurrentRun: false);
            Debug.Log("[Sephiria Trainer] Gave " + amount + " saphires. Total: " + updated);
        }

        private static void GiveGold(PlayerLocalDataStorage storage)
        {
            var avatar = storage.Avatar;
            if (avatar == null)
            {
                Debug.LogWarning("[Sephiria Trainer] Enter the game before giving gold.");
                return;
            }

            int amount = Math.Min(10000, int.MaxValue - Math.Max(0, avatar.Money));
            avatar.AddMoney(amount);
            Debug.Log("[Sephiria Trainer] Gave " + amount + " gold.");
        }
    }
}
