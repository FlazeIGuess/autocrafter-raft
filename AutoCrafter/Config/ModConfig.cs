using UnityEngine;

namespace pp.RaftMods.AutoCrafter
{
    /// <summary>
    /// Static configuration for the AutoCrafter mod.
    /// All upgrade costs and runtime settings are defined here.
    /// </summary>
    public static class CModConfig
    {
        /// <summary>Seconds between craft attempts per upgraded chest.</summary>
        public static int CheckIntervalSeconds = 3;

        /// <summary>Fraction of upgrade costs returned on downgrade (0.0 - 1.0).</summary>
        public static float ReturnMultiplier = 0.5f;

        /// <summary>If true, upgraded chests are tinted cyan to distinguish them.</summary>
        public static bool ChangeColor = true;

        /// <summary>Maximum recipe entries shown in the picker at once.</summary>
        public static int MaxRecipePickerResults = 60;

        /// <summary>Number of recipe rows spawned per frame when building picker results.</summary>
        public static int RecipePickerBatchSize = 12;

        /// <summary>Number of container rows spawned per frame when building picker results.</summary>
        public static int ContainerPickerBatchSize = 12;

        /// <summary>If true, slot panel refresh prefers incremental sync before full rebuild.</summary>
        public static bool UseIncrementalSlotRefresh = true;

        /// <summary>Maximum number of upgrade levels (= max crafting slots).</summary>
        public const int MAX_LEVEL = 3;

        /// <summary>
        /// Upgrade costs per level. Index 0 = Level 1, Index 1 = Level 2, Index 2 = Level 3.
        /// Each entry is an array of CUpgradeCost required to upgrade to that level.
        /// </summary>
        public static readonly CUpgradeCost[][] UpgradeCostsByLevel =
        {
            // Level 1: unlock first crafting slot
            new[]
            {
                new CUpgradeCost("Plastic",      20),
                new CUpgradeCost("Scrap",        10),
                new CUpgradeCost("CircuitBoard",  6),
                new CUpgradeCost("Battery",       1)
            },
            // Level 2: unlock second slot
            new[]
            {
                new CUpgradeCost("Plastic",      15),
                new CUpgradeCost("Scrap",         8),
                new CUpgradeCost("CircuitBoard",  4)
            },
            // Level 3: unlock third slot
            new[]
            {
                new CUpgradeCost("Plastic",      15),
                new CUpgradeCost("Scrap",         8),
                new CUpgradeCost("CircuitBoard",  4)
            }
        };

        /// <summary>Returns costs for the given upgrade level (1-based). Null if out of range.</summary>
        public static CUpgradeCost[] GetCostsForLevel(int level)
        {
            if (level < 1 || level > MAX_LEVEL) return null;
            return UpgradeCostsByLevel[level - 1];
        }

        /// <summary>Resolves all item references in upgrade costs. Call once after world load.</summary>
        public static void ResolveAllCosts()
        {
            foreach (var levelCosts in UpgradeCostsByLevel)
                foreach (var cost in levelCosts)
                    cost.Resolve();
        }

        /// <summary>Formats upgrade costs for a level into a readable string for the UI.</summary>
        public static string FormatCostsForLevel(int level)
        {
            var costs = GetCostsForLevel(level);
            if (costs == null) return "Max level reached";
            var parts = new System.Collections.Generic.List<string>();
            foreach (var c in costs)
                parts.Add(c.Amount + "x " + c.ItemName);
            return string.Join(", ", parts);
        }
    }

    /// <summary>
    /// Represents a single item cost for upgrading a chest.
    /// Resolves the item reference at runtime from the item name.
    /// </summary>
    [System.Serializable]
    public class CUpgradeCost
    {
        public string ItemName;
        public int Amount;

        [System.NonSerialized]
        public Item_Base ResolvedItem;

        public CUpgradeCost() { }

        public CUpgradeCost(string itemName, int amount)
        {
            ItemName = itemName;
            Amount = amount;
        }

        /// <summary>Looks up the Item_Base reference by name.</summary>
        public void Resolve()
        {
            ResolvedItem = ItemManager.GetItemByName(ItemName);
            if (ResolvedItem == null)
                Debug.LogWarning("[AutoCrafter] Upgrade cost item not found: " + ItemName);
        }

        /// <summary>Returns true if the player has enough of this item.</summary>
        public bool PlayerHasEnough(Network_Player player)
        {
            return ResolvedItem != null &&
                   player.Inventory.GetItemCount(ResolvedItem.UniqueName) >= Amount;
        }
    }
}
