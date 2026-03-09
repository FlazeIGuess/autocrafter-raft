using System;
using UnityEngine;

namespace pp.RaftMods.AutoCrafter
{
    /// <summary>
    /// Data for one crafting slot inside an upgraded chest.
    /// Serialized to JSON for save/load. Runtime caches are marked NonSerialized.
    /// </summary>
    [Serializable]
    public class CCrafterSlot
    {
        /// <summary>UniqueIndex of the item to craft. -1 means no recipe selected.</summary>
        public int RecipeItemIndex = -1;

        /// <summary>If true, craft indefinitely. If false, only craft RemainingCount times.</summary>
        public bool IsInfinite = true;

        /// <summary>How many craft operations remain. Only used when IsInfinite = false.</summary>
        public int RemainingCount = 0;

        /// <summary>Whether this slot is currently enabled and should craft.</summary>
        public bool IsActive = false;

        /// <summary>
        /// ObjectIndex of the output Storage_Small chest.
        /// -1 = no separate output container (craft into own chest).
        /// </summary>
        public int OutputContainerIndex = -1;

        /// <summary>
        /// ObjectIndex of the input Storage_Small chest.
        /// -1 = no separate input container (use own chest's inventory).
        /// </summary>
        public int InputContainerIndex = -1;

        // --- Runtime caches, not serialized ---

        [NonSerialized] public Item_Base CachedItem;
        [NonSerialized] public string LastStatusMessage = string.Empty;
        [NonSerialized] public bool HasMissingIngredients = false;

        /// <summary>Runtime cache of the output storage. Null if no output assigned.</summary>
        [NonSerialized] public Storage_Small CachedOutputStorage;

        /// <summary>Runtime cache of the input storage. Null if no input assigned.</summary>
        [NonSerialized] public Storage_Small CachedInputStorage;

        /// <summary>True when the output container has no room for crafted items.</summary>
        [NonSerialized] public bool OutputFull = false;

        // --- Computed properties ---

        public bool HasRecipe => RecipeItemIndex >= 0 && CachedItem != null;
        public bool HasOutputContainer => OutputContainerIndex >= 0;
        public bool HasInputContainer => InputContainerIndex >= 0;

        public bool CanAttemptCraft =>
            IsActive &&
            HasRecipe &&
            (IsInfinite || RemainingCount > 0);

        /// <summary>Resolves the cached Item_Base from the stored index.</summary>
        public void ResolveItem()
        {
            if (RecipeItemIndex < 0) { CachedItem = null; return; }
            CachedItem = ItemManager.GetItemByIndex(RecipeItemIndex);
            if (CachedItem == null)
                Debug.LogWarning("[AutoCrafter] Could not resolve item index " + RecipeItemIndex);
        }

        /// <summary>Resolves the output Storage_Small from the stored ObjectIndex.</summary>
        public void ResolveOutputStorage()
        {
            CachedOutputStorage = null;
            OutputFull = false;
            if (OutputContainerIndex <= 0) { OutputContainerIndex = -1; return; }
            CachedOutputStorage = FindStorageByIndex(OutputContainerIndex);
        }

        /// <summary>Resolves the input Storage_Small from the stored ObjectIndex.</summary>
        public void ResolveInputStorage()
        {
            CachedInputStorage = null;
            if (InputContainerIndex <= 0) { InputContainerIndex = -1; return; }
            CachedInputStorage = FindStorageByIndex(InputContainerIndex);
        }

        private static Storage_Small FindStorageByIndex(int objectIndex)
        {
            foreach (Storage_Small storage in StorageManager.allStorages)
            {
                if (storage != null && storage.ObjectIndex == (uint)objectIndex)
                    return storage;
            }
            return null;
        }

        /// <summary>Sets a new recipe for this slot and resets status.</summary>
        public void SetRecipe(Item_Base item)
        {
            CachedItem = item;
            RecipeItemIndex = item != null ? item.UniqueIndex : -1;
            IsActive = item != null;
            LastStatusMessage = string.Empty;
            HasMissingIngredients = false;
            OutputFull = false;
        }

        /// <summary>Removes the recipe from this slot.</summary>
        public void ClearRecipe()
        {
            CachedItem = null;
            RecipeItemIndex = -1;
            IsActive = false;
            LastStatusMessage = "No recipe selected.";
            HasMissingIngredients = false;
            OutputFull = false;
        }

        /// <summary>Sets the output container for this slot.</summary>
        public void SetOutputContainer(Storage_Small storage)
        {
            OutputContainerIndex = storage != null ? (int)storage.ObjectIndex : -1;
            CachedOutputStorage = storage;
            OutputFull = false;
        }

        /// <summary>Sets the input container for this slot.</summary>
        public void SetInputContainer(Storage_Small storage)
        {
            InputContainerIndex = storage != null ? (int)storage.ObjectIndex : -1;
            CachedInputStorage = storage;
        }
    }
}
