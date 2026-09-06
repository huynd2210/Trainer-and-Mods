using System.Collections.Generic;
using FallenAces;
using FallenAces.AI;
using FallenAces.PlayerHands;
using UnityEngine;
using Key = UnityEngine.InputSystem.Key;

namespace FallenAcesTrainer
{
    // Flags the Health.TakeDamage patch reads. They live here rather than on the patch
    // so the cheats that own them stay self-contained.
    internal static class CombatOverrides
    {
        public static bool GodMode;
        public static bool OneHitKills;

        public static void Clear()
        {
            GodMode = false;
            OneHitKills = false;
        }
    }

    // Every cheat in the trainer. Adding one means adding an element here; the input
    // loop, the menu and the ambient strip all iterate this list and never name a cheat.
    internal static class TrainerCheats
    {
        public const int LootPerPress = 500;
        private const int ToughnessMax = 100;

        private static Vector3 markPosition;
        private static Vector3 markFacing;
        private static bool marked;

        public static Feature[] Build()
        {
            return new Feature[]
            {
                // --- Survival -------------------------------------------------------
                new ToggleFeature("Survival", "GodMode", "God mode",
                    "Block all damage to the player.",
                    Key.F5, on => CombatOverrides.GodMode = on),

                new ToggleFeature("Survival", "InfiniteStamina", "Infinite stamina",
                    "Sprinting, kicking and dodging never run the bar down.",
                    Key.None, on =>
                    {
                        if (Game.TryPlayer(out var player) && player.Stamina != null)
                            player.Stamina.InfiniteStamina = on;
                    }),

                new ToggleFeature("Survival", "Invisible", "Invisible to enemies",
                    "Enemies stop noticing the player. Existing alerts still play out.",
                    // Written unconditionally rather than guarded on the getter: the
                    // getter is true whenever *any* ignore flag is set, so while noclip
                    // holds its own flag a guarded write would never set ours, and
                    // invisibility would quietly lapse the moment noclip ended. Apply
                    // only runs on a state change, so this costs no repeated events.
                    Key.None, on => Awareness.IgnorePlayer = on),

                new ActionFeature("Survival", "HealToFull", "Heal to full",
                    "Refill health and toughness.",
                    Key.F10, HealToFull),

                // --- Combat ---------------------------------------------------------
                new ToggleFeature("Combat", "InfiniteAmmo", "Infinite ammo",
                    "Keep every weapon in the inventory topped up.",
                    Key.F7, apply: null, whileOn: InfiniteAmmo.TopUp),

                new ToggleFeature("Combat", "OneHitKills", "One-hit kills",
                    "Anything but the player dies in one hit. Bosses still lose one health division per hit.",
                    Key.None, on => CombatOverrides.OneHitKills = on),

                new ToggleFeature("Combat", "SuperPunch", "Super punch",
                    "The game's own superpunch cheat.",
                    Key.None, on => { if (Game.TryPlayer(out var player)) player.SuperPunch = on; }),

                new ToggleFeature("Combat", "Roundhouser", "Roundhouse kick",
                    "The game's own roundhouser cheat.",
                    Key.None, on => { if (Game.TryPlayer(out var player)) player.Roundhouser = on; }),

                new ActionFeature("Combat", "NeutraliseAll", "Neutralise all enemies",
                    "Kill every enemy in the level. Scripted encounters may not expect this.",
                    Key.None, NeutraliseAll),

                // --- Movement -------------------------------------------------------
                // F8 is deliberately avoided: the Kill Tracker mod in this folder binds it.
                new ToggleFeature("Movement", "Noclip", "Noclip",
                    "Fly through geometry. Jump/crouch move up and down, sprint goes faster.",
                    Key.F11, SetNoclip),

                new ActionFeature("Movement", "MarkPosition", "Mark position",
                    "Remember where the player is standing.",
                    Key.None, MarkPosition),

                new ActionFeature("Movement", "RecallPosition", "Recall position",
                    "Teleport back to the marked position.",
                    Key.None, RecallPosition),

                // --- World ----------------------------------------------------------
                new ScaleFeature("World", "GameSpeed", "Game speed",
                    "Slow the world down or speed it up.",
                    () => TimeScaleManager.Instance != null ? TimeScaleManager.Instance.UnpausedTimescale : 1f,
                    value => { if (TimeScaleManager.Instance != null) TimeScaleManager.Instance.UnpausedTimescale = value; },
                    step: 0.25f, min: 0.1f, max: 4f, neutral: 1f, format: "0.00",
                    decrease: Key.None, increase: Key.None, reset: Key.None),

                new ToggleFeature("World", "InfiniteLighter", "Infinite lighter",
                    "The game's own permalight cheat.",
                    Key.None, on => { if (Game.TryPlayer(out var player)) player.InfiniteLighter = on; }),

                new ActionFeature("World", "AddLoot", "Add $" + LootPerPress,
                    "Pay the player, the same way picking up loot does.",
                    Key.None, AddLoot),
            };
        }

        private static void HealToFull()
        {
            if (!Game.TryPlayer(out var player)) return;
            if (player.TryGetHealth(out var health) && health != null) health.Heal(health.Max);
            if (player.Toughness != null) player.Toughness.Set(ToughnessMax);
            Game.Notify("Healed");
        }

        // Mirrors the game's own seriousbomb command, but scoped to Enemy.All so that
        // civilians and other NPCs are left alone.
        private static void NeutraliseAll()
        {
            int count = 0;
            var enemies = Enemy.All;
            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive) continue;
                var health = enemy.Health;
                if (health == null || health.IsOut) continue;
                health.TakeDamage(new DamageInfo(health.Max, lethal: true, canGib: true, DamageType.Crush));
                count++;
            }
            Game.Notify(count == 0 ? "No enemies to neutralise" : "Neutralised " + count + " enemies");
        }

        // The game owns noclip as a movement state, so drive its toggle rather than
        // duplicating the state, and only when the current state disagrees. The state
        // machine refuses the transition in some states (dead, in a vehicle), so say so
        // instead of leaving the menu claiming a noclip that never happened.
        private static void SetNoclip(bool on)
        {
            if (!Game.TryMovement(out var movement)) return;
            if (movement.IsNoclipping == on) return;
            if (movement.TryToggleNoclipState() == 0) Game.Notify("Noclip not available right now");
        }

        private static void MarkPosition()
        {
            if (!Game.TryPlayer(out var player)) return;
            markPosition = player.transform.position;
            markFacing = player.Head != null ? player.Head.forward : player.transform.forward;
            marked = true;
            Game.Notify("Position marked");
        }

        private static void RecallPosition()
        {
            if (!marked) { Game.Notify("No position marked"); return; }
            if (!Game.TryPlayer(out var player)) return;
            var body = player.Rigidbody;
            if (body != null)
            {
                body.position = markPosition;
                body.linearVelocity = Vector3.zero;
            }
            player.transform.position = markPosition;
            var flat = Vector3.Scale(markFacing, new Vector3(1f, 0f, 1f));
            if (flat.sqrMagnitude > 0.0001f)
                player.SetBodyRotation(Quaternion.LookRotation(flat).eulerAngles.y);
            Game.Notify("Recalled to marked position");
        }

        private static void AddLoot()
        {
            if (!Game.TryPlayer(out var player) || player.Inventory == null) return;
            player.Inventory.PickupLoot(LootPerPress);
            Game.Notify("Gave $" + LootPerPress);
        }

        internal static class InfiniteAmmo
        {
            // Tops every stored weapon back up to its magazine size. Writing the field
            // directly does not tell the HUD, which refreshes on the hands AmmoChanged
            // event, so raise that event whenever something actually changed.
            public static void TopUp()
            {
                if (!Game.TryPlayer(out var player)) return;
                var inventory = player.Inventory;
                if (inventory == null) return;
                bool changed = false;
                for (int slot = 0; slot < inventory.Capacity; slot++)
                {
                    if (!inventory.TryGetItemInSlot(slot, out var item)) continue;
                    if (item.IsNullOrDestroyed()) continue;
                    if (item.Type != Inventory.IItem.ItemType.Weapon) continue;
                    if (!item.WeaponInfo.TryGetFirstPersonWeapon(out var weapon)) continue;
                    if (!weapon.UsesAmmo || item.WeaponInfo.AmmoLeft >= weapon.Ammo) continue;
                    item.WeaponInfo.AmmoLeft = weapon.Ammo;
                    changed = true;
                }
                if (changed) RefreshHud();
            }

            private static void RefreshHud()
            {
                if (!GlobalEventManager.TryGet(out var events)) return;
                if (!events.TryGetHandsEventManager(out var hands) || hands == null) return;
                hands.TriggerEvent((int)PlayerHandsEventManager.Event.AmmoChanged);
            }
        }
    }
}
