using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace pp.RaftMods.AutoCrafter
{
    /// <summary>
    /// Bare-bones IMGUI AutoCrafter dialog.
    /// Keeps interaction simple: one main window + lightweight pickers.
    /// </summary>
    public class CUICrafterDialog : MonoBehaviour
    {
        private const int WINDOW_ID = 840021;
        private const int RECIPE_WINDOW_ID = 840022;
        private const int CONTAINER_WINDOW_ID = 840023;

        private const float WINDOW_WIDTH = 440f;
        private const float WINDOW_HEIGHT = 620f;
        private const float COMPACT_WINDOW_WIDTH = 360f;
        private const float COMPACT_WINDOW_HEIGHT = 520f;
        private const float COLLAPSED_HEIGHT = 56f;
        private const float COMPACT_COLLAPSED_HEIGHT = 52f;
        private const float WINDOW_MARGIN = 8f;
        private const float MIN_VISIBLE_WIDTH = 180f;
        private const float COMPACT_MIN_VISIBLE_WIDTH = 150f;
        private const float MIN_VISIBLE_HEADER = 28f;

        private const string PREF_WINDOW_X = "AutoCrafter.IMGUI.WindowX";
        private const string PREF_WINDOW_Y = "AutoCrafter.IMGUI.WindowY";
        private const string PREF_COLLAPSED = "AutoCrafter.IMGUI.Collapsed";

        private enum PickerMode
        {
            None,
            Recipe,
            OutputContainer,
            InputContainer
        }

        private struct RecipeCostEntry
        {
            public string Name;
            public int Have;
            public int Need;
            public bool Met;
        }

        public uint CurrentObjectIndex { get; private set; }

        private CrafterBehaviour mi_behaviour;
        private Rect mi_windowRect;
        private Rect mi_recipeRect;
        private Rect mi_containerRect;
        private bool mi_visible;
        private bool mi_collapsed;
        private bool mi_compactMode;

        private bool mi_downgradeConfirmPending;
        private float mi_downgradeConfirmTimer;

        private PickerMode mi_pickerMode = PickerMode.None;
        private int mi_pickerSlotIndex = -1;

        private Vector2 mi_mainScroll;
        private Vector2 mi_recipeScroll;
        private Vector2 mi_containerScroll;

        private string mi_chestNameBuffer = string.Empty;
        private string mi_recipeSearch = string.Empty;

        private List<Item_Base> mi_recipeCache = new List<Item_Base>();
        private readonly Dictionary<int, string> mi_countBuffer = new Dictionary<int, string>();

        private Texture2D mi_windowBgTex;
        private Texture2D mi_panelBgTex;
        private Texture2D mi_ingredientsBgTex;
        private Texture2D mi_slotsBgTex;
        private GUIStyle mi_windowStyle;
        private GUIStyle mi_modalStyle;
        private GUIStyle mi_panelStyle;
        private GUIStyle mi_sectionTitleStyle;

        public void Build(Transform parent)
        {
            EnsureStyles();
            RefreshCompactMode();
            float initialWidth = GetWindowWidth();
            float initialHeight = GetMaxWindowHeight();

            float defaultX = Mathf.Max(WINDOW_MARGIN, Screen.width - initialWidth - 20f);
            float defaultY = Mathf.Max(WINDOW_MARGIN, (Screen.height - initialHeight) * 0.5f);

            mi_windowRect = new Rect(
                PlayerPrefs.GetFloat(PREF_WINDOW_X, defaultX),
                PlayerPrefs.GetFloat(PREF_WINDOW_Y, defaultY),
                initialWidth,
                initialHeight);

            mi_collapsed = PlayerPrefs.GetInt(PREF_COLLAPSED, 0) == 1;
            mi_windowRect.height = mi_collapsed ? GetCollapsedHeight() : ResolveExpandedHeight();
            mi_windowRect = ClampWindowRect(mi_windowRect);

            float pickerWidth = GetPickerWidth();
            float pickerHeight = GetPickerHeight();
            mi_recipeRect = new Rect(
                Screen.width - pickerWidth - WINDOW_MARGIN,
                Mathf.Max(WINDOW_MARGIN, (Screen.height - pickerHeight) * 0.5f),
                pickerWidth,
                pickerHeight);
            mi_containerRect = mi_recipeRect;

            mi_visible = false;
        }

        public void Show(CrafterBehaviour behaviour)
        {
            if (behaviour == null)
                return;

            mi_behaviour = behaviour;
            CurrentObjectIndex = behaviour.ObjectIndex;
            mi_chestNameBuffer = AutoCrafter.DataManager?.GetChestName(CurrentObjectIndex) ?? string.Empty;
            mi_visible = true;
            RefreshCompactMode();
            mi_windowRect.width = GetWindowWidth();
            mi_windowRect.height = mi_collapsed ? GetCollapsedHeight() : ResolveExpandedHeight();
            PositionMainWindowRight();
            mi_windowRect = ClampWindowRect(mi_windowRect);
            ClosePicker();
        }

        public void Hide()
        {
            SaveWindowState();
            ClosePicker();
            mi_visible = false;
            mi_behaviour = null;
            ResetDowngradeConfirm();
        }

        public void RefreshStatus(uint objectIndex)
        {
            if (mi_behaviour == null || mi_behaviour.ObjectIndex != objectIndex)
                return;
        }

        private void OnDestroy()
        {
            SaveWindowState();
        }

        private void Update()
        {
            if (mi_visible && mi_behaviour != null)
                HandleHotkeys();

            if (!mi_downgradeConfirmPending)
                return;

            mi_downgradeConfirmTimer -= Time.deltaTime;
            if (mi_downgradeConfirmTimer <= 0f)
                ResetDowngradeConfirm();
        }

        private void OnGUI()
        {
            if (!mi_visible || mi_behaviour == null)
                return;

            EnsureStyles();

            RefreshCompactMode();
            mi_windowRect.width = GetWindowWidth();
            mi_windowRect.height = mi_collapsed ? GetCollapsedHeight() : ResolveExpandedHeight();
            mi_windowRect = GUI.Window(WINDOW_ID, mi_windowRect, DrawMainWindow, "AutoCrafter", mi_windowStyle);
            mi_windowRect = ClampWindowRect(mi_windowRect);

            if (mi_pickerMode == PickerMode.Recipe)
            {
                mi_recipeRect.width = GetPickerWidth();
                mi_recipeRect.height = GetPickerHeight();
                mi_recipeRect = GUI.Window(RECIPE_WINDOW_ID, mi_recipeRect, DrawRecipeWindow, "Select Recipe", mi_modalStyle);
                mi_recipeRect = ClampModalRect(mi_recipeRect);
            }
            else if (mi_pickerMode == PickerMode.OutputContainer || mi_pickerMode == PickerMode.InputContainer)
            {
                string title = mi_pickerMode == PickerMode.OutputContainer ? "Select Output Container" : "Select Input Container";
                mi_containerRect.width = GetPickerWidth();
                mi_containerRect.height = GetPickerHeight();
                mi_containerRect = GUI.Window(CONTAINER_WINDOW_ID, mi_containerRect, DrawContainerWindow, title, mi_modalStyle);
                mi_containerRect = ClampModalRect(mi_containerRect);
            }

            ConsumeMouseEventsIfOverUI();
        }

        private void DrawMainWindow(int id)
        {
            DrawHeaderRow();

            if (!mi_collapsed)
            {
                mi_mainScroll = GUILayout.BeginScrollView(mi_mainScroll, GUILayout.Height(mi_windowRect.height - 70f));

                DrawChestMeta();
                DrawUpgradeSection();
                DrawSlotsSection();

                GUILayout.EndScrollView();
            }

            // Drag lock while pickers are open.
            if (mi_pickerMode == PickerMode.None)
                GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
        }

        private void DrawHeaderRow()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Chest #" + CurrentObjectIndex, GUILayout.Width(mi_compactMode ? 100f : 130f));

            string collapseText = mi_collapsed ? "+" : "-";
            if (GUILayout.Button(collapseText, GUILayout.Width(28f)))
            {
                mi_collapsed = !mi_collapsed;
                SaveWindowState();
            }
            GUILayout.EndHorizontal();
        }

        private void DrawChestMeta()
        {
            CCrafterData data = mi_behaviour.Data;
            GUILayout.Space(4f);

            BeginSection(mi_panelBgTex, "Chest");
            GUILayout.Label("Level: " + data.UpgradeLevel + " / " + CModConfig.MAX_LEVEL);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Name", GUILayout.Width(44f));
            string newName = GUILayout.TextField(mi_chestNameBuffer ?? string.Empty, GUILayout.MinWidth(mi_compactMode ? 120f : 160f));
            if (!string.Equals(newName, mi_chestNameBuffer, StringComparison.Ordinal))
            {
                mi_chestNameBuffer = newName;
                AutoCrafter.DataManager?.SetChestName(CurrentObjectIndex, mi_chestNameBuffer);
            }
            GUILayout.EndHorizontal();
            EndSection();
        }

        private void DrawUpgradeSection()
        {
            CCrafterData data = mi_behaviour.Data;
            GUILayout.Space(6f);

            BeginSection(mi_ingredientsBgTex, "Ingredients");

            if (data.UpgradeLevel < CModConfig.MAX_LEVEL)
            {
                int nextLevel = data.UpgradeLevel + 1;
                CUpgradeCost[] costs = CModConfig.GetCostsForLevel(nextLevel);
                GUILayout.Label("Next: Level " + nextLevel);

                if (costs != null)
                {
                    Network_Player player = ComponentManager<Network_Player>.Value;
                    for (int i = 0; i < costs.Length; i++)
                    {
                        CUpgradeCost cost = costs[i];
                        int have = player != null ? player.Inventory.GetItemCount(cost.ItemName) : 0;
                        string itemName = cost.ResolvedItem != null && cost.ResolvedItem.settings_Inventory != null
                            ? cost.ResolvedItem.settings_Inventory.DisplayName
                            : cost.ItemName;

                        Color old = GUI.contentColor;
                        GUI.contentColor = have >= cost.Amount
                            ? new Color(0.48f, 0.90f, 0.53f)
                            : new Color(0.95f, 0.47f, 0.43f);
                        GUILayout.Label("- " + itemName + ": " + have + " / " + cost.Amount);
                        GUI.contentColor = old;
                    }
                }
            }
            else
            {
                GUILayout.Label("Maximum level reached.");
            }

            GUILayout.BeginHorizontal();
            GUI.enabled = data.UpgradeLevel < CModConfig.MAX_LEVEL;
            if (GUILayout.Button("Upgrade"))
                OnUpgradeClicked();

            GUI.enabled = data.IsUpgraded;
            string downLabel = mi_downgradeConfirmPending ? "Confirm Downgrade" : "Downgrade";
            if (GUILayout.Button(downLabel))
                OnDowngradeClicked();
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            EndSection();
        }

        private void DrawSlotsSection()
        {
            CCrafterData data = mi_behaviour.Data;
            GUILayout.Space(8f);

            BeginSection(mi_slotsBgTex, "Slots");

            if (!data.IsUpgraded || data.Slots == null || data.Slots.Count == 0)
            {
                GUILayout.Label("Upgrade the chest to unlock slots.");
                EndSection();
                return;
            }

            for (int i = 0; i < data.Slots.Count; i++)
                DrawSlot(i, data.Slots[i]);

            EndSection();
        }

        private void DrawSlot(int slotIndex, CCrafterSlot slot)
        {
            if (slot == null)
                return;

            GUILayout.BeginVertical("box");
            string recipeName = slot.HasRecipe && slot.CachedItem != null
                ? GetRecipeDisplayName(slot.CachedItem)
                : "No recipe";

            GUILayout.BeginHorizontal();
            GUILayout.Label("S" + (slotIndex + 1), GUILayout.Width(24f));
            GUILayout.Label(recipeName, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Recipe", GUILayout.Width(mi_compactMode ? 56f : 64f)))
                OpenRecipePicker(slotIndex);
            if (GUILayout.Button("X", GUILayout.Width(24f)))
            {
                mi_behaviour.SetSlotRecipe(slotIndex, null);
                mi_behaviour.SetSlotActive(slotIndex, false);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            bool newActive = GUILayout.Toggle(slot.IsActive, "On", GUILayout.Width(42f));
            if (newActive != slot.IsActive)
                mi_behaviour.SetSlotActive(slotIndex, newActive);

            bool newInfinite = GUILayout.Toggle(slot.IsInfinite, "Loop", GUILayout.Width(54f));
            if (newInfinite != slot.IsInfinite)
                mi_behaviour.SetSlotInfinite(slotIndex, newInfinite);

            if (!slot.IsInfinite)
            {
                string currentCount;
                if (!mi_countBuffer.TryGetValue(slotIndex, out currentCount))
                    currentCount = Mathf.Max(1, slot.RemainingCount).ToString();

                GUILayout.Label("Count", GUILayout.Width(38f));
                string nextCount = GUILayout.TextField(currentCount, GUILayout.Width(mi_compactMode ? 56f : 70f));

                if (!string.Equals(nextCount, currentCount, StringComparison.Ordinal))
                    mi_countBuffer[slotIndex] = nextCount;

                int parsed;
                if (int.TryParse(nextCount, out parsed) && parsed > 0 && parsed != slot.RemainingCount)
                    mi_behaviour.SetSlotCount(slotIndex, parsed);
            }
            GUILayout.EndHorizontal();

            DrawContainerRow("Output", slot.HasOutputContainer,
                mi_behaviour.GetResolvedOutputStorageForSlot(slotIndex),
                () => OpenContainerPicker(slotIndex, PickerMode.OutputContainer),
                () => mi_behaviour.SetSlotOutputContainer(slotIndex, null));

            DrawContainerRow("Input", slot.HasInputContainer,
                mi_behaviour.GetResolvedInputStorageForSlot(slotIndex),
                () => OpenContainerPicker(slotIndex, PickerMode.InputContainer),
                () => mi_behaviour.SetSlotInputContainer(slotIndex, null));

            SlotStatusViewModel status = mi_behaviour.GetSlotStatusViewModel(slotIndex);
            if (status != null)
            {
                Color previous = GUI.color;
                GUI.color = GetStatusColor(status.State);
                GUILayout.Label("Status: " + status.DisplayText);
                GUI.color = previous;
            }

            GUILayout.EndVertical();
        }

        private void DrawContainerRow(string label, bool hasContainer, Storage_Small storage,
            Action onPick, Action onClear)
        {
            string text = hasContainer
                ? BuildContainerLabel(storage)
                : "Own Chest";

            GUILayout.BeginHorizontal();
            GUILayout.Label(label + ": " + text, GUILayout.MinWidth(mi_compactMode ? 130f : 180f));
            if (GUILayout.Button("Pick", GUILayout.Width(mi_compactMode ? 50f : 60f)))
                onPick();
            GUI.enabled = hasContainer;
            if (GUILayout.Button("Own", GUILayout.Width(mi_compactMode ? 50f : 60f)))
                onClear();
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        private void DrawRecipeWindow(int id)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Search", GUILayout.Width(52f));
            mi_recipeSearch = GUILayout.TextField(mi_recipeSearch ?? string.Empty);
            if (GUILayout.Button("X", GUILayout.Width(26f)))
                mi_recipeSearch = string.Empty;
            if (GUILayout.Button("Close", GUILayout.Width(56f)))
                ClosePicker();
            GUILayout.EndHorizontal();

            string needle = (mi_recipeSearch ?? string.Empty).Trim().ToLowerInvariant();
            List<Item_Base> items = BuildFilteredRecipeList(mi_pickerSlotIndex, needle, 200);
            Inventory previewInventory = mi_behaviour != null ? mi_behaviour.GetSlotInputInventoryForPreview(mi_pickerSlotIndex) : null;
            mi_recipeScroll = GUILayout.BeginScrollView(mi_recipeScroll, GUILayout.Height(mi_recipeRect.height - 84f));

            if (GUILayout.Button("Clear Recipe", GUILayout.Height(26f)))
            {
                if (mi_pickerSlotIndex >= 0)
                    mi_behaviour.SetSlotRecipe(mi_pickerSlotIndex, null);
                ClosePicker();
            }

            for (int i = 0; i < items.Count; i++)
            {
                Item_Base item = items[i];
                if (item == null)
                    continue;

                ItemInstance_Recipe recipe = item.settings_recipe;
                string label = GetRecipeDisplayName(item);
                bool craftable = IsCraftableInPreview(previewInventory, recipe);
                List<RecipeCostEntry> costs = BuildRecipeCosts(recipe, previewInventory);

                Color oldColor = GUI.contentColor;
                GUI.contentColor = craftable
                    ? new Color(0.75f, 0.95f, 0.75f)
                    : new Color(1.00f, 0.82f, 0.82f);

                if (GUILayout.Button(label, GUILayout.Height(24f)))
                {
                    if (mi_pickerSlotIndex >= 0)
                        mi_behaviour.SetSlotRecipe(mi_pickerSlotIndex, item);
                    ClosePicker();
                }

                GUI.contentColor = oldColor;

                if (costs.Count > 0)
                {
                    GUILayout.BeginHorizontal();
                    for (int c = 0; c < costs.Count; c++)
                    {
                        RecipeCostEntry cost = costs[c];
                        Color prev = GUI.contentColor;
                        GUI.contentColor = cost.Met
                            ? new Color(0.48f, 0.90f, 0.53f)
                            : new Color(0.95f, 0.47f, 0.43f);

                        GUILayout.Label(cost.Name + " " + cost.Have + "/" + cost.Need);
                        GUI.contentColor = prev;

                        if (c < costs.Count - 1)
                            GUILayout.Label("|", GUILayout.Width(8f));
                    }
                    GUILayout.EndHorizontal();
                }

                GUILayout.Space(2f);
            }

            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
        }

        private void DrawContainerWindow(int id)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Select target container", GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Close", GUILayout.Width(60f)))
                ClosePicker();
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Use Own Chest", GUILayout.Height(26f)))
            {
                AssignContainer(null);
                ClosePicker();
            }

            List<Storage_Small> storages = StorageManager.allStorages
                .Where(s => s != null && s.ObjectIndex != CurrentObjectIndex)
                .OrderBy(s => mi_behaviour != null ? Vector3.Distance(s.transform.position, mi_behaviour.transform.position) : 0f)
                .ToList();

            mi_containerScroll = GUILayout.BeginScrollView(mi_containerScroll, GUILayout.Height(mi_containerRect.height - 90f));
            for (int i = 0; i < storages.Count; i++)
            {
                Storage_Small storage = storages[i];
                string label = BuildContainerLabel(storage);
                if (GUILayout.Button(label, GUILayout.Height(24f)))
                {
                    AssignContainer(storage);
                    ClosePicker();
                }
            }
            GUILayout.EndScrollView();

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
        }

        private void OnUpgradeClicked()
        {
            if (mi_behaviour == null)
                return;

            CUpgradeResult result = mi_behaviour.Upgrade();
            if (result.Success)
            {
                AudioFeedbackAdapter.PlayClick();
                AutoCrafter.ModUI?.Toast?.Show("Upgraded to Level " + mi_behaviour.Data.UpgradeLevel + "!", CRaftStyleHelper.ColBtnGreen, 2.5f);
                return;
            }

            AudioFeedbackAdapter.PlayFail();
            if (result.MissingItems != null && result.MissingItems.Length > 0)
                AutoCrafter.ModUI?.Toast?.Show("Missing: " + string.Join(" | ", result.MissingItems), CRaftStyleHelper.ColBtnRed, 4f);
            else
                AutoCrafter.ModUI?.Toast?.Show(result.ErrorMessage, CRaftStyleHelper.ColBtnRed, 3f);
        }

        private void OnDowngradeClicked()
        {
            if (mi_behaviour == null)
                return;

            if (!mi_downgradeConfirmPending)
            {
                mi_downgradeConfirmPending = true;
                mi_downgradeConfirmTimer = 3f;
                AudioFeedbackAdapter.PlayFail();
                return;
            }

            ResetDowngradeConfirm();
            mi_behaviour.Downgrade();
            AudioFeedbackAdapter.PlayClick();
            Hide();
        }

        private void ResetDowngradeConfirm()
        {
            mi_downgradeConfirmPending = false;
            mi_downgradeConfirmTimer = 0f;
        }

        private void OpenRecipePicker(int slotIndex)
        {
            mi_pickerMode = PickerMode.Recipe;
            mi_pickerSlotIndex = slotIndex;
            mi_recipeSearch = string.Empty;
            mi_recipeScroll = Vector2.zero;
            mi_recipeRect.width = GetPickerWidth();
            mi_recipeRect.height = GetPickerHeight();
            PositionRecipeWindowRight();
            EnsureRecipeCache();
        }

        private void OpenContainerPicker(int slotIndex, PickerMode mode)
        {
            mi_pickerMode = mode;
            mi_pickerSlotIndex = slotIndex;
            mi_containerScroll = Vector2.zero;
            mi_containerRect.width = GetPickerWidth();
            mi_containerRect.height = GetPickerHeight();
            PositionContainerWindowRight();
        }

        private void AssignContainer(Storage_Small storage)
        {
            if (mi_behaviour == null || mi_pickerSlotIndex < 0)
                return;

            if (mi_pickerMode == PickerMode.OutputContainer)
                mi_behaviour.SetSlotOutputContainer(mi_pickerSlotIndex, storage);
            else if (mi_pickerMode == PickerMode.InputContainer)
                mi_behaviour.SetSlotInputContainer(mi_pickerSlotIndex, storage);
        }

        private void ClosePicker()
        {
            mi_pickerMode = PickerMode.None;
            mi_pickerSlotIndex = -1;
        }

        private void EnsureRecipeCache()
        {
            if (mi_recipeCache.Count > 0)
                return;

            mi_recipeCache = ItemManager.GetAllItems()
                .Where(item => item != null)
                .Where(item => item.settings_Inventory != null)
                .Where(item => !string.IsNullOrEmpty(item.settings_Inventory.DisplayName))
                .Where(item => item.settings_Inventory.DisplayName != "An item")
                .Where(item => item.settings_recipe != null)
                .Where(item => item.settings_recipe.NewCost != null && item.settings_recipe.NewCost.Length > 0)
                .Where(item => item.settings_recipe.CraftingCategory != CraftingCategory.Nothing)
                .Where(item => item.settings_recipe.CraftingCategory != CraftingCategory.Hidden)
                .OrderBy(item => item.settings_Inventory.DisplayName)
                .ToList();
        }

        private List<Item_Base> BuildFilteredRecipeList(int slotIndex, string needle, int max)
        {
            EnsureRecipeCache();

            CCrafterSlot slot = null;
            if (mi_behaviour != null && mi_behaviour.Data != null && slotIndex >= 0 && slotIndex < mi_behaviour.Data.Slots.Count)
                slot = mi_behaviour.Data.Slots[slotIndex];

            int currentRecipeIndex = slot != null ? slot.RecipeItemIndex : -1;
            Inventory previewInventory = mi_behaviour != null ? mi_behaviour.GetSlotInputInventoryForPreview(slotIndex) : null;

            IEnumerable<Item_Base> query = mi_recipeCache;
            if (!string.IsNullOrEmpty(needle))
                query = query.Where(item => GetRecipeDisplayName(item).ToLowerInvariant().Contains(needle));

            List<Item_Base> result = query
                .Select(item => new
                {
                    Item = item,
                    IsCurrent = item != null && item.UniqueIndex == currentRecipeIndex,
                    IsCraftable = IsCraftableInPreview(previewInventory, item != null ? item.settings_recipe : null),
                    Name = GetRecipeDisplayName(item)
                })
                .OrderByDescending(x => x.IsCurrent)
                .ThenByDescending(x => x.IsCraftable)
                .ThenBy(x => x.Name)
                .Take(max)
                .Select(x => x.Item)
                .ToList();

            return result;
        }

        private bool IsCraftableInPreview(Inventory inventory, ItemInstance_Recipe recipe)
        {
            if (mi_behaviour == null || inventory == null || recipe == null)
                return false;

            int required = mi_behaviour.GetIngredientRequirementForRecipe(recipe);
            if (required <= 0)
                return false;

            int available = mi_behaviour.GetIngredientAvailabilityForRecipe(inventory, recipe);
            return available >= required;
        }

        private List<RecipeCostEntry> BuildRecipeCosts(ItemInstance_Recipe recipe, Inventory inventory)
        {
            List<RecipeCostEntry> entries = new List<RecipeCostEntry>();
            if (recipe == null || recipe.NewCost == null || recipe.NewCost.Length == 0)
                return entries;

            for (int i = 0; i < recipe.NewCost.Length; i++)
            {
                CostMultiple cost = recipe.NewCost[i];
                if (cost == null || cost.items == null || cost.items.Length == 0)
                    continue;

                int need = Mathf.Max(0, cost.amount);
                int have = 0;
                string name = "Unknown";

                for (int itemIndex = 0; itemIndex < cost.items.Length; itemIndex++)
                {
                    Item_Base alt = cost.items[itemIndex];
                    if (alt == null)
                        continue;

                    if (name == "Unknown")
                    {
                        if (alt.settings_Inventory != null && !string.IsNullOrEmpty(alt.settings_Inventory.DisplayName))
                            name = alt.settings_Inventory.DisplayName;
                        else
                            name = alt.UniqueName;
                    }

                    if (inventory != null)
                        have += inventory.GetItemCount(alt.UniqueName);
                }

                entries.Add(new RecipeCostEntry
                {
                    Name = name,
                    Have = have,
                    Need = need,
                    Met = have >= need
                });
            }

            return entries;
        }

        private void HandleHotkeys()
        {
            if (mi_pickerMode == PickerMode.None)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                    Hide();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                ClosePicker();
                return;
            }

            bool enterPressed = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
            if (!enterPressed)
                return;

            if (mi_pickerMode == PickerMode.Recipe && mi_pickerSlotIndex >= 0)
            {
                string needle = (mi_recipeSearch ?? string.Empty).Trim().ToLowerInvariant();
                List<Item_Base> top = BuildFilteredRecipeList(mi_pickerSlotIndex, needle, 1);
                if (top.Count > 0)
                {
                    mi_behaviour.SetSlotRecipe(mi_pickerSlotIndex, top[0]);
                    ClosePicker();
                }
            }
        }

        private static string GetRecipeDisplayName(Item_Base item)
        {
            if (item == null || item.settings_Inventory == null)
                return "Unknown";

            string name = item.settings_Inventory.DisplayName;
            int outAmount = item.settings_recipe != null ? Mathf.Max(1, item.settings_recipe.AmountToCraft) : 1;
            return outAmount > 1 ? name + " x" + outAmount : name;
        }

        private string BuildContainerLabel(Storage_Small storage)
        {
            if (storage == null)
                return "Missing";

            string userName = AutoCrafter.DataManager?.GetChestName(storage.ObjectIndex);
            if (!string.IsNullOrEmpty(userName))
                return userName + " (#" + storage.ObjectIndex + ")";

            return "Chest #" + storage.ObjectIndex;
        }

        private float ResolveExpandedHeight()
        {
            float maxHeight = GetMaxWindowHeight();
            float minHeight = mi_compactMode ? 360f : 420f;
            return Mathf.Clamp(Screen.height - 24f, minHeight, maxHeight);
        }

        private Rect ClampWindowRect(Rect rect)
        {
            float minVisibleWidth = mi_compactMode ? COMPACT_MIN_VISIBLE_WIDTH : MIN_VISIBLE_WIDTH;
            float minX = -(rect.width - minVisibleWidth) + WINDOW_MARGIN;
            float maxX = Screen.width - minVisibleWidth - WINDOW_MARGIN;
            float minY = WINDOW_MARGIN;
            float maxY = Screen.height - MIN_VISIBLE_HEADER - WINDOW_MARGIN;

            if (minX > maxX)
            {
                float x = (minX + maxX) * 0.5f;
                minX = x;
                maxX = x;
            }

            if (minY > maxY)
            {
                float y = (minY + maxY) * 0.5f;
                minY = y;
                maxY = y;
            }

            rect.x = Mathf.Clamp(rect.x, minX, maxX);
            rect.y = Mathf.Clamp(rect.y, minY, maxY);
            return rect;
        }

        private static Rect ClampModalRect(Rect rect)
        {
            float margin = 8f;
            rect.x = Mathf.Clamp(rect.x, margin, Mathf.Max(margin, Screen.width - rect.width - margin));
            rect.y = Mathf.Clamp(rect.y, margin, Mathf.Max(margin, Screen.height - rect.height - margin));
            return rect;
        }

        private void PositionMainWindowRight()
        {
            mi_windowRect.x = Screen.width - mi_windowRect.width - 20f;
            mi_windowRect.y = Mathf.Clamp(mi_windowRect.y, WINDOW_MARGIN, Mathf.Max(WINDOW_MARGIN, Screen.height - mi_windowRect.height - WINDOW_MARGIN));
        }

        private void PositionRecipeWindowRight()
        {
            mi_recipeRect.x = Screen.width - mi_recipeRect.width - WINDOW_MARGIN;
            mi_recipeRect.y = Mathf.Clamp(mi_windowRect.y + 24f, WINDOW_MARGIN, Mathf.Max(WINDOW_MARGIN, Screen.height - mi_recipeRect.height - WINDOW_MARGIN));
            mi_recipeRect = ClampModalRect(mi_recipeRect);
        }

        private void PositionContainerWindowRight()
        {
            mi_containerRect.x = Screen.width - mi_containerRect.width - WINDOW_MARGIN;
            mi_containerRect.y = Mathf.Clamp(mi_windowRect.y + 24f, WINDOW_MARGIN, Mathf.Max(WINDOW_MARGIN, Screen.height - mi_containerRect.height - WINDOW_MARGIN));
            mi_containerRect = ClampModalRect(mi_containerRect);
        }

        private void ConsumeMouseEventsIfOverUI()
        {
            Event e = Event.current;
            if (e == null)
                return;

            if (e.type != EventType.MouseDown && e.type != EventType.MouseUp &&
                e.type != EventType.MouseDrag && e.type != EventType.ScrollWheel)
                return;

            bool overMain = mi_windowRect.Contains(e.mousePosition);
            bool overRecipe = mi_pickerMode == PickerMode.Recipe && mi_recipeRect.Contains(e.mousePosition);
            bool overContainer = (mi_pickerMode == PickerMode.OutputContainer || mi_pickerMode == PickerMode.InputContainer) &&
                                 mi_containerRect.Contains(e.mousePosition);

            if (overMain || overRecipe || overContainer)
                e.Use();
        }

        private void RefreshCompactMode()
        {
            mi_compactMode = Screen.width < 1280 || Screen.height < 800;
        }

        private float GetWindowWidth()
        {
            return mi_compactMode ? COMPACT_WINDOW_WIDTH : WINDOW_WIDTH;
        }

        private float GetCollapsedHeight()
        {
            return mi_compactMode ? COMPACT_COLLAPSED_HEIGHT : COLLAPSED_HEIGHT;
        }

        private float GetMaxWindowHeight()
        {
            return mi_compactMode ? COMPACT_WINDOW_HEIGHT : WINDOW_HEIGHT;
        }

        private float GetPickerWidth()
        {
            return mi_compactMode ? 360f : 420f;
        }

        private float GetPickerHeight()
        {
            return Mathf.Min(mi_compactMode ? 460f : 520f, Screen.height - 40f);
        }

        private static Color GetStatusColor(SlotCraftabilityState state)
        {
            switch (state)
            {
                case SlotCraftabilityState.Ready:
                    return new Color(0.48f, 0.90f, 0.53f);
                case SlotCraftabilityState.OutputFull:
                    return new Color(0.94f, 0.73f, 0.33f);
                case SlotCraftabilityState.NoOutputContainer:
                case SlotCraftabilityState.MissingIngredients:
                    return new Color(0.95f, 0.47f, 0.43f);
                case SlotCraftabilityState.Inactive:
                default:
                    return new Color(0.82f, 0.82f, 0.82f);
            }
        }

        private void EnsureStyles()
        {
            if (mi_windowStyle != null)
                return;

            mi_windowBgTex = CreateSolidTex(new Color(0.07f, 0.09f, 0.12f, 1f));
            mi_panelBgTex = CreateSolidTex(new Color(0.12f, 0.16f, 0.21f, 1f));
            mi_ingredientsBgTex = CreateSolidTex(new Color(0.16f, 0.14f, 0.09f, 1f));
            mi_slotsBgTex = CreateSolidTex(new Color(0.10f, 0.12f, 0.17f, 1f));

            mi_windowStyle = new GUIStyle(GUI.skin.window);
            mi_windowStyle.normal.background = mi_windowBgTex;
            mi_windowStyle.onNormal.background = mi_windowBgTex;
            mi_windowStyle.fixedWidth = 0f;
            mi_windowStyle.fixedHeight = 0f;
            mi_windowStyle.stretchWidth = true;
            mi_windowStyle.stretchHeight = true;

            mi_modalStyle = new GUIStyle(GUI.skin.window);
            mi_modalStyle.normal.background = mi_windowBgTex;
            mi_modalStyle.onNormal.background = mi_windowBgTex;
            mi_modalStyle.fixedWidth = 0f;
            mi_modalStyle.fixedHeight = 0f;
            mi_modalStyle.stretchWidth = true;
            mi_modalStyle.stretchHeight = true;

            mi_panelStyle = new GUIStyle(GUI.skin.box);
            mi_panelStyle.normal.background = mi_panelBgTex;
            mi_panelStyle.onNormal.background = mi_panelBgTex;
            mi_panelStyle.padding = new RectOffset(8, 8, 6, 8);
            mi_panelStyle.margin = new RectOffset(2, 2, 4, 4);

            mi_sectionTitleStyle = new GUIStyle(GUI.skin.label);
            mi_sectionTitleStyle.fontStyle = FontStyle.Bold;
            mi_sectionTitleStyle.normal.textColor = new Color(0.80f, 0.92f, 0.98f);
        }

        private static Texture2D CreateSolidTex(Color color)
        {
            Texture2D tex = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }

        private void BeginSection(Texture2D bg, string title)
        {
            if (mi_panelStyle == null)
            {
                GUILayout.BeginVertical("box");
            }
            else
            {
                Texture2D oldBg = mi_panelStyle.normal.background;
                mi_panelStyle.normal.background = bg;
                mi_panelStyle.onNormal.background = bg;
                GUILayout.BeginVertical(mi_panelStyle);
                mi_panelStyle.normal.background = oldBg;
                mi_panelStyle.onNormal.background = oldBg;
            }

            if (mi_sectionTitleStyle != null)
                GUILayout.Label(title, mi_sectionTitleStyle);
            else
                GUILayout.Label(title);
        }

        private static void EndSection()
        {
            GUILayout.EndVertical();
        }

        private void SaveWindowState()
        {
            PlayerPrefs.SetFloat(PREF_WINDOW_X, mi_windowRect.x);
            PlayerPrefs.SetFloat(PREF_WINDOW_Y, mi_windowRect.y);
            PlayerPrefs.SetInt(PREF_COLLAPSED, mi_collapsed ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
