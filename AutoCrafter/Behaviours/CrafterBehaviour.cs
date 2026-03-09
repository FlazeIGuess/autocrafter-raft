using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace pp.RaftMods.AutoCrafter
{
    /// <summary>
    /// MonoBehaviour attached to every Storage_Small chest.
    /// Handles upgrade/downgrade logic and per-tick crafting.
    /// Routes crafted items to output containers when assigned.
    /// Only added once per chest (DisallowMultipleComponent).
    /// </summary>
    [DisallowMultipleComponent]
    public class CrafterBehaviour : MonoBehaviour
    {
        public uint ObjectIndex { get; private set; }
        public CCrafterData Data { get; private set; }
        public bool IsOpen => mi_storage != null && mi_storage.IsOpen;

        private Storage_Small mi_storage;
        private CDataManager mi_dataManager;

        // --- Initialization ---

        /// <summary>
        /// Called by CStorageManager after AddComponent.
        /// Loads existing data or creates a fresh CCrafterData for this chest.
        /// </summary>
        public void Initialize(Storage_Small storage, CCrafterData existingData, CDataManager dataManager)
        {
            mi_storage = storage;
            mi_dataManager = dataManager;
            ObjectIndex = storage.ObjectIndex;

            Data = existingData ?? new CCrafterData(ObjectIndex);
            Data.ResolveItems();

            Debug.Log("[AutoCrafter] Initialize: ObjectIndex=" + ObjectIndex
                + " UpgradeLevel=" + Data.UpgradeLevel);
            UpdateVisuals();
        }

        // --- Craft logic (called by AutoCrafter.CraftLoop on host only) ---

        /// <summary>
        /// Attempts one craft cycle on all active slots.
        /// Yields between slots so the game does not freeze on large crafting queues.
        /// Routes crafted items to output containers when assigned.
        /// Pauses crafting when the output container is full.
        /// </summary>
        public IEnumerator TryCraft()
        {
            if (!Data.IsUpgraded) yield break;

            Inventory inv = mi_storage?.GetInventoryReference();
            if (inv == null) yield break;

            for (int i = 0; i < Data.Slots.Count; i++)
            {
                CCrafterSlot slot = Data.Slots[i];
                if (slot == null || !slot.CanAttemptCraft) continue;
                if (!slot.HasRecipe || slot.CachedItem == null || slot.CachedItem.settings_recipe == null)
                {
                    slot.HasMissingIngredients = true;
                    slot.OutputFull = false;
                    slot.LastStatusMessage = "Recipe data missing!";
                    yield return new WaitForEndOfFrame();
                    continue;
                }

                ItemInstance_Recipe recipe = slot.CachedItem.settings_recipe;
                if (recipe.NewCost == null || recipe.NewCost.Length == 0)
                {
                    slot.HasMissingIngredients = true;
                    slot.OutputFull = false;
                    slot.LastStatusMessage = "Recipe ingredients missing!";
                    yield return new WaitForEndOfFrame();
                    continue;
                }

                if (!ValidateRecipeCostStructure(recipe, out string invalidCostMessage))
                {
                    slot.HasMissingIngredients = true;
                    slot.OutputFull = false;
                    slot.LastStatusMessage = invalidCostMessage;
                    yield return new WaitForEndOfFrame();
                    continue;
                }

                // Determine target inventory for crafted items
                Inventory targetInv = GetOutputInventory(slot, inv);

                // Full-check: make sure target has room before we consume ingredients
                if (IsInventoryFull(targetInv, slot.CachedItem, recipe.AmountToCraft))
                {
                    slot.OutputFull = true;
                    slot.HasMissingIngredients = false;
                    slot.LastStatusMessage = slot.HasOutputContainer
                        ? "Output container full!"
                        : "Storage full!";
                    yield return new WaitForEndOfFrame();
                    continue;
                }
                slot.OutputFull = false;

                // Determine source inventory for ingredients
                Inventory inputInv = GetInputInventory(slot, inv);

                // Check ingredients in the input inventory
                CheckIngredients(inputInv, recipe, out bool canCraft, out string missingText);

                if (canCraft)
                {
                    // Remove ingredients from input inventory
                    inputInv.RemoveCostMultiple(recipe.NewCost);

                    // Add crafted item to output inventory
                    targetInv.AddItem(slot.CachedItem.UniqueName, recipe.AmountToCraft);

                    if (!slot.IsInfinite)
                    {
                        slot.RemainingCount--;
                        if (slot.RemainingCount <= 0)
                            slot.IsActive = false;
                    }

                    slot.HasMissingIngredients = false;
                    string craftedItemName = slot.CachedItem.settings_Inventory != null && !string.IsNullOrEmpty(slot.CachedItem.settings_Inventory.DisplayName)
                        ? slot.CachedItem.settings_Inventory.DisplayName
                        : slot.CachedItem.UniqueName;
                    slot.LastStatusMessage = "Crafted: " + craftedItemName;
                    mi_dataManager.Save();
                }
                else
                {
                    slot.HasMissingIngredients = true;
                    slot.LastStatusMessage = "Missing: " + missingText;
                    if (slot.HasInputContainer && slot.CachedInputStorage == null)
                        slot.LastStatusMessage = "Input container missing!";
                }

                yield return new WaitForEndOfFrame();
            }

            // Refresh UI status if chest is currently open
            if (IsOpen)
                AutoCrafter.ModUI?.RefreshSlotStatus(ObjectIndex);
        }

        // --- Upgrade / Downgrade ---

        /// <summary>
        /// Upgrades this chest by one level. Checks and removes costs from the local player.
        /// Returns a CUpgradeResult describing success or the exact resources that are missing.
        /// </summary>
        public CUpgradeResult Upgrade()
        {
            if (Data.UpgradeLevel >= CModConfig.MAX_LEVEL)
                return CUpgradeResult.Fail("Already at maximum upgrade level!");

            int nextLevel = Data.UpgradeLevel + 1;
            CUpgradeCost[] costs = CModConfig.GetCostsForLevel(nextLevel);
            Network_Player localPlayer = ComponentManager<Network_Player>.Value;
            if (localPlayer == null)
                return CUpgradeResult.Fail("Player not found.");

            // Collect all shortfalls in one pass
            var missing = new List<string>();
            foreach (var cost in costs)
            {
                int have = localPlayer.Inventory.GetItemCount(cost.ItemName);
                if (have < cost.Amount)
                {
                    string displayName = cost.ResolvedItem != null
                        ? cost.ResolvedItem.settings_Inventory.DisplayName
                        : cost.ItemName;
                    missing.Add(displayName + " (have " + have + ", need " + cost.Amount + ")");
                }
            }

            if (missing.Count > 0)
                return CUpgradeResult.Fail(missing.ToArray());

            // All costs met - deduct
            foreach (var cost in costs)
                localPlayer.Inventory.RemoveItem(cost.ItemName, cost.Amount);

            Data.UpgradeLevel = nextLevel;
            Data.EnsureSlotCount(nextLevel);
            Data.SaveName = SaveAndLoad.CurrentGameFileName;
            mi_dataManager.SetData(Data);
            UpdateVisuals();
            return CUpgradeResult.Ok();
        }

        /// <summary>
        /// Fully downgrades this chest back to a plain storage.
        /// Returns a fraction of ALL upgrade costs across all levels.
        /// </summary>
        public void Downgrade()
        {
            Network_Player localPlayer = ComponentManager<Network_Player>.Value;
            if (localPlayer == null) return;

            // Return a fraction of all costs paid so far
            for (int level = 1; level <= Data.UpgradeLevel; level++)
            {
                CUpgradeCost[] costs = CModConfig.GetCostsForLevel(level);
                if (costs == null) continue;
                foreach (var cost in costs)
                {
                    int returnAmount = Mathf.FloorToInt(cost.Amount * CModConfig.ReturnMultiplier);
                    if (returnAmount > 0)
                        localPlayer.Inventory.AddItem(cost.ItemName, returnAmount);
                }
            }

            Data.UpgradeLevel = 0;
            Data.Slots.Clear();
            mi_dataManager.RemoveData(ObjectIndex);
            UpdateVisuals();
        }

        // --- Slot configuration helpers (called by UI) ---

        /// <summary>Sets the recipe for a specific slot and saves.</summary>
        public void SetSlotRecipe(int slotIndex, Item_Base item)
        {
            if (slotIndex < 0 || slotIndex >= Data.Slots.Count) return;
            Data.Slots[slotIndex].SetRecipe(item);
            mi_dataManager.SetData(Data);
        }

        /// <summary>Toggles infinite mode for a slot and saves.</summary>
        public void SetSlotInfinite(int slotIndex, bool infinite)
        {
            if (slotIndex < 0 || slotIndex >= Data.Slots.Count) return;
            Data.Slots[slotIndex].IsInfinite = infinite;
            mi_dataManager.SetData(Data);
        }

        /// <summary>Sets the remaining count for a slot and saves.</summary>
        public void SetSlotCount(int slotIndex, int count)
        {
            if (slotIndex < 0 || slotIndex >= Data.Slots.Count) return;
            Data.Slots[slotIndex].RemainingCount = Mathf.Max(0, count);
            mi_dataManager.SetData(Data);
        }

        /// <summary>Toggles the active state of a slot and saves.</summary>
        public void SetSlotActive(int slotIndex, bool active)
        {
            if (slotIndex < 0 || slotIndex >= Data.Slots.Count) return;
            Data.Slots[slotIndex].IsActive = active;
            mi_dataManager.SetData(Data);
        }

        /// <summary>Assigns an output container for a specific slot and saves.</summary>
        public void SetSlotOutputContainer(int slotIndex, Storage_Small outputStorage)
        {
            if (slotIndex < 0 || slotIndex >= Data.Slots.Count) return;
            Data.Slots[slotIndex].SetOutputContainer(outputStorage);
            mi_dataManager.SetData(Data);
            if (IsOpen)
                AutoCrafter.ModUI?.RefreshSlotStatus(ObjectIndex);
        }

        /// <summary>Assigns an input container for a specific slot and saves.</summary>
        public void SetSlotInputContainer(int slotIndex, Storage_Small inputStorage)
        {
            if (slotIndex < 0 || slotIndex >= Data.Slots.Count) return;
            Data.Slots[slotIndex].SetInputContainer(inputStorage);
            mi_dataManager.SetData(Data);
            if (IsOpen)
                AutoCrafter.ModUI?.RefreshSlotStatus(ObjectIndex);
        }

        /// <summary>
        /// Returns the canonical craftability state for one slot.
        /// </summary>
        public SlotStatusViewModel GetSlotStatusViewModel(int slotIndex)
        {
            if (Data == null || slotIndex < 0 || slotIndex >= Data.Slots.Count)
                return SlotStatusViewModel.Create(SlotCraftabilityState.Inactive, "Slot unavailable");

            CCrafterSlot slot = Data.Slots[slotIndex];
            if (slot == null)
                return SlotStatusViewModel.Create(SlotCraftabilityState.Inactive, "Slot unavailable");

            if (!slot.HasRecipe)
                return SlotStatusViewModel.Create(SlotCraftabilityState.Inactive, "No recipe assigned");

            if (!slot.IsActive)
                return SlotStatusViewModel.Create(SlotCraftabilityState.Inactive, "Slot paused");

            if (slot.HasOutputContainer && slot.CachedOutputStorage == null)
                return SlotStatusViewModel.Create(SlotCraftabilityState.NoOutputContainer, "Assigned output container is missing");

            if (slot.OutputFull)
                return SlotStatusViewModel.Create(
                    SlotCraftabilityState.OutputFull,
                    string.IsNullOrEmpty(slot.LastStatusMessage) ? "Output inventory is full" : slot.LastStatusMessage);

            if (slot.HasInputContainer && slot.CachedInputStorage == null)
                return SlotStatusViewModel.Create(SlotCraftabilityState.MissingIngredients, "Assigned input container is missing");

            if (slot.HasMissingIngredients)
                return SlotStatusViewModel.Create(
                    SlotCraftabilityState.MissingIngredients,
                    string.IsNullOrEmpty(slot.LastStatusMessage) ? "Required ingredients are missing" : slot.LastStatusMessage);

            return SlotStatusViewModel.Create(
                SlotCraftabilityState.Ready,
                string.IsNullOrEmpty(slot.LastStatusMessage) ? "Ready to craft" : slot.LastStatusMessage);
        }

        /// <summary>
        /// Resolves ingredient source inventory for UI previews using the same container rules as crafting.
        /// </summary>
        public Inventory GetSlotInputInventoryForPreview(int slotIndex)
        {
            Inventory fallback = mi_storage?.GetInventoryReference();
            if (Data == null || slotIndex < 0 || slotIndex >= Data.Slots.Count)
                return fallback;

            CCrafterSlot slot = Data.Slots[slotIndex];
            if (slot == null)
                return fallback;

            return GetInputInventory(slot, fallback);
        }

        /// <summary>
        /// Returns the selected recipe for one slot, or null when unavailable.
        /// </summary>
        public ItemInstance_Recipe GetSlotRecipeForPreview(int slotIndex)
        {
            if (Data == null || slotIndex < 0 || slotIndex >= Data.Slots.Count)
                return null;

            CCrafterSlot slot = Data.Slots[slotIndex];
            if (slot == null || !slot.HasRecipe || slot.CachedItem == null)
                return null;

            return slot.CachedItem.settings_recipe;
        }

        /// <summary>
        /// Returns the current output assignment for a slot with stale-cache recovery.
        /// </summary>
        public Storage_Small GetResolvedOutputStorageForSlot(int slotIndex)
        {
            if (Data == null || slotIndex < 0 || slotIndex >= Data.Slots.Count)
                return null;

            CCrafterSlot slot = Data.Slots[slotIndex];
            if (slot == null || !slot.HasOutputContainer)
                return null;

            if (slot.CachedOutputStorage == null)
                slot.ResolveOutputStorage();

            return slot.CachedOutputStorage;
        }

        /// <summary>
        /// Returns the current input assignment for a slot with stale-cache recovery.
        /// </summary>
        public Storage_Small GetResolvedInputStorageForSlot(int slotIndex)
        {
            if (Data == null || slotIndex < 0 || slotIndex >= Data.Slots.Count)
                return null;

            CCrafterSlot slot = Data.Slots[slotIndex];
            if (slot == null || !slot.HasInputContainer)
                return null;

            if (slot.CachedInputStorage == null)
                slot.ResolveInputStorage();

            return slot.CachedInputStorage;
        }

        /// <summary>
        /// Counts available ingredient units for a recipe in a specific inventory.
        /// Uses the same OR-cost aggregation logic as crafting checks.
        /// </summary>
        public int GetIngredientAvailabilityForRecipe(Inventory inventory, ItemInstance_Recipe recipe)
        {
            if (inventory == null || recipe == null || recipe.NewCost == null)
                return 0;

            int available = 0;
            for (int costIndex = 0; costIndex < recipe.NewCost.Length; costIndex++)
            {
                CostMultiple cost = recipe.NewCost[costIndex];
                if (cost == null || cost.items == null)
                    continue;

                int have = 0;
                for (int itemIndex = 0; itemIndex < cost.items.Length; itemIndex++)
                {
                    Item_Base item = cost.items[itemIndex];
                    if (item == null)
                        continue;

                    have += inventory.GetItemCount(item.UniqueName);
                }

                available += have;
            }

            return available;
        }

        /// <summary>
        /// Counts required ingredient units for one recipe.
        /// </summary>
        public int GetIngredientRequirementForRecipe(ItemInstance_Recipe recipe)
        {
            if (recipe == null || recipe.NewCost == null)
                return 0;

            int required = 0;
            for (int i = 0; i < recipe.NewCost.Length; i++)
            {
                CostMultiple cost = recipe.NewCost[i];
                if (cost == null)
                    continue;

                required += Mathf.Max(0, cost.amount);
            }

            return required;
        }

        // --- Private helpers ---

        /// <summary>
        /// Returns the inventory to place crafted items into.
        /// </summary>
        private Inventory GetOutputInventory(CCrafterSlot slot, Inventory fallback)
        {
            if (!slot.HasOutputContainer) return fallback;
            if (slot.CachedOutputStorage == null)
            {
                slot.ResolveOutputStorage();
                if (slot.CachedOutputStorage == null) return fallback;
            }
            return slot.CachedOutputStorage.GetInventoryReference() ?? fallback;
        }

        /// <summary>
        /// Returns the inventory to pull ingredients from.
        /// </summary>
        private Inventory GetInputInventory(CCrafterSlot slot, Inventory fallback)
        {
            if (!slot.HasInputContainer) return fallback;
            if (slot.CachedInputStorage == null)
            {
                slot.ResolveInputStorage();
                if (slot.CachedInputStorage == null) return fallback;
            }
            return slot.CachedInputStorage.GetInventoryReference() ?? fallback;
        }

        /// <summary>
        /// Checks whether the target inventory has room for the crafted item.
        /// Returns true if the inventory cannot fit the given amount.
        /// </summary>
        private bool IsInventoryFull(Inventory inv, Item_Base item, int amount)
        {
            if (inv == null || item == null || item.settings_Inventory == null) return true;

            int canFit = 0;
            int stackSize = item.settings_Inventory.StackSize;

            foreach (Slot slot in inv.allSlots)
            {
                if (!slot.active) continue;

                if (slot.IsEmpty)
                {
                    canFit += stackSize;
                }
                else if (slot.itemInstance.UniqueIndex == item.UniqueIndex && !slot.StackIsFull())
                {
                    canFit += stackSize - slot.itemInstance.Amount;
                }

                if (canFit >= amount) return false;
            }

            return true;
        }

        /// <summary>
        /// Checks whether all ingredients for a recipe are present in the given inventory.
        /// Returns the missing items as a formatted string if any are missing.
        /// </summary>
        private void CheckIngredients(Inventory inv, ItemInstance_Recipe recipe,
            out bool canCraft, out string missingText)
        {
            if (inv == null || recipe == null || recipe.NewCost == null || recipe.NewCost.Length == 0)
            {
                canCraft = false;
                missingText = "recipe data missing";
                return;
            }

            if (!ValidateRecipeCostStructure(recipe, out string invalidCostMessage))
            {
                canCraft = false;
                missingText = invalidCostMessage;
                return;
            }

            canCraft = true;
            var missingParts = new List<string>();

            foreach (var cost in recipe.NewCost)
            {
                if (cost == null)
                    continue;

                int requiredAmount = Mathf.Max(0, cost.amount);
                if (requiredAmount == 0)
                    continue;

                // Sum all alternative items (OR-logic)
                int have = 0;
                if (cost.items != null)
                {
                    foreach (var item in cost.items)
                    {
                        if (item != null)
                            have += inv.GetItemCount(item.UniqueName);
                    }
                }

                if (have < requiredAmount)
                {
                    canCraft = false;
                    int missing = requiredAmount - have;
                    string displayName = cost.items != null && cost.items.Length > 0 && cost.items[0] != null
                        ? (cost.items[0].settings_Inventory != null ? cost.items[0].settings_Inventory.DisplayName : cost.items[0].UniqueName)
                        : "Unknown";
                    missingParts.Add(missing + "x " + displayName);
                }
            }

            missingText = missingParts.Count > 0 ? string.Join(", ", missingParts) : string.Empty;
        }

        /// <summary>
        /// Ensures recipe.NewCost is structurally valid. Invalid recipe data must fail closed.
        /// </summary>
        private bool ValidateRecipeCostStructure(ItemInstance_Recipe recipe, out string message)
        {
            message = "Invalid recipe cost data!";

            if (recipe == null || recipe.NewCost == null || recipe.NewCost.Length == 0)
                return false;

            bool hasAtLeastOneValidCost = false;

            for (int i = 0; i < recipe.NewCost.Length; i++)
            {
                CostMultiple cost = recipe.NewCost[i];
                if (cost == null || cost.amount <= 0 || cost.items == null || cost.items.Length == 0)
                    return false;

                bool hasValidAlternative = false;
                for (int itemIndex = 0; itemIndex < cost.items.Length; itemIndex++)
                {
                    Item_Base item = cost.items[itemIndex];
                    if (item == null || string.IsNullOrEmpty(item.UniqueName))
                        continue;

                    hasValidAlternative = true;
                    break;
                }

                if (!hasValidAlternative)
                    return false;

                hasAtLeastOneValidCost = true;
            }

            return hasAtLeastOneValidCost;
        }

        /// <summary>
        /// Applies or removes the cyan tint on the chest's mesh renderers.
        /// Only active when CModConfig.ChangeColor is true.
        /// </summary>
        private void UpdateVisuals()
        {
            if (!CModConfig.ChangeColor) return;
            bool upgraded = Data.IsUpgraded;
            Color targetColor = upgraded ? new Color(0.4f, 0.85f, 1f) : Color.white;

            MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>(true);
            foreach (var r in renderers)
            {
                Material[] mats = r.materials;
                foreach (var mat in mats)
                {
                    if (mat == null) continue;
                    if (mat.HasProperty("_Color"))
                        mat.color = targetColor;
                }
                r.materials = mats;
            }
        }

        private void ShowNotification(string message)
        {
            Debug.Log("[AutoCrafter] " + message);
        }

        private void OnDestroy()
        {
            // Visual cleanup: restore white on destroy
            if (Data != null && Data.IsUpgraded && CModConfig.ChangeColor)
            {
                MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>(true);
                foreach (var r in renderers)
                {
                    Material[] mats = r.materials;
                    foreach (var mat in mats)
                    {
                        if (mat != null && mat.HasProperty("_Color"))
                            mat.color = Color.white;
                    }
                    r.materials = mats;
                }
            }
        }
    }
}
