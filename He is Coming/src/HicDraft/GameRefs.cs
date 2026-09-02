using System;
using Il2CppInterop.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HicDraft
{
    /// <summary>
    /// Lookups for the game's singletons. Everything the game exposes derives from
    /// Singleton&lt;T&gt; : StaticInstance&lt;T&gt;, whose static Instance property lives on an IL2CPP
    /// generic instantiation - awkward to reach from a managed plugin - so these resolve by scene
    /// search instead and cache the result until the object is destroyed (scene change, run reset).
    /// </summary>
    internal static class GameRefs
    {
        private static PlayerControlsManager _controls;
        private static ItemManager _items;
        private static OverworldUIManager _overworld;
        private static ItemCompendium _compendium;

        public static PlayerControlsManager Controls => Cache(ref _controls);
        public static ItemManager Items => Cache(ref _items);
        public static OverworldUIManager Overworld => Cache(ref _overworld);

        /// <summary>
        /// The compendium is not a singleton; PlayerControlsManager holds the scene reference to it.
        /// Falls back to a scene search (including inactive objects, since it is hidden until opened).
        /// </summary>
        public static ItemCompendium Compendium
        {
            get
            {
                if (_compendium != null) return _compendium;
                var controls = Controls;
                if (controls != null && controls.itemCompendium != null)
                    _compendium = controls.itemCompendium;
                else
                    _compendium = Find<ItemCompendium>();
                return _compendium;
            }
        }

        public static void Invalidate()
        {
            _controls = null;
            _items = null;
            _overworld = null;
            _compendium = null;
        }

        private static T Cache<T>(ref T slot) where T : Component
        {
            if (slot != null) return slot;
            slot = Find<T>();
            return slot;
        }

        private static T Find<T>() where T : Object
        {
            try
            {
                var found = Object.FindObjectOfType(Il2CppType.Of<T>(), true);
                return found == null ? null : found.TryCast<T>();
            }
            catch (Exception e)
            {
                Plugin.Log?.LogWarning($"[Draft] Could not find {typeof(T).Name}: {e.Message}");
                return null;
            }
        }
    }
}
