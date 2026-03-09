using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace pp.RaftMods.AutoCrafter
{
    /// <summary>
    /// Popup scroll list for valid recipes with explicit output and ingredient counts.
    /// User picks a recipe which is then applied to the target slot.
    /// </summary>
    public class CUICrafterRecipePicker : MonoBehaviour
    {
        private const float POPUP_WIDTH  = 340f;
        private const float ITEM_HEIGHT  = 88f;
        private const float ICON_SIZE    = 36f;
        private const string RECIPE_POLICY_TEXT = "Recipe list includes all valid craft recipes. Use search to filter quickly.";

        private Action<Item_Base> mi_onSelected;
        private Action mi_onHidden;
        private GameObject mi_root;
        private InputField mi_searchField;
        private Transform mi_listContent;
        private ScrollRect mi_scrollRect;
        private Text mi_statusLabel;
        private Text mi_policyLabel;

        private List<Item_Base> mi_allRecipes = new List<Item_Base>();
        private List<RecipeListItemViewModel> mi_allRecipeViews = new List<RecipeListItemViewModel>();
        private List<GameObject> mi_rows = new List<GameObject>();

        private CrafterBehaviour mi_contextBehaviour;
        private int mi_contextSlotIndex = -1;
        private Coroutine mi_populateRoutine;
        private int mi_populationGeneration;

        public void Build(Transform parent, Action<Item_Base> onSelected, Action onHidden)
        {
            mi_onSelected = onSelected;
            mi_onHidden = onHidden;

            // Panel - anchored to left side of the parent dialog
            mi_root = new GameObject("AC_PickerPanel");
            mi_root.transform.SetParent(parent, false);
            Image panelImg = mi_root.AddComponent<Image>();
            panelImg.color = UIStyleTokens.PickerPanel;
            RectTransform rt = mi_root.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-10f, 0f);
            rt.sizeDelta = new Vector2(POPUP_WIDTH, 0f);

            // Header
            CreateLabel(mi_root.transform, "AC_PickerHeader",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -8f), new Vector2(POPUP_WIDTH - 20f, 22f),
                "Select Recipe", 14, TextAnchor.UpperCenter)
                .color = CRaftStyleHelper.ColAccent;

            mi_policyLabel = CreateLabel(mi_root.transform, "AC_PickerPolicy",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -24f), new Vector2(POPUP_WIDTH - 20f, 14f),
                RECIPE_POLICY_TEXT, 9, TextAnchor.UpperCenter);
            mi_policyLabel.color = UIStyleTokens.PickerPolicy;

            // Search field (right edge pulls back to leave room for the X button)
            mi_searchField = CreateInputField(mi_root.transform, "AC_Search",
                "Search recipe or ingredient...");
            UIFactory.AddInputFocusGuard(mi_searchField);
            mi_searchField.onValueChanged.AddListener(OnSearchChanged);
            SetRectAnchored(mi_searchField.gameObject, 0f, 1f, 1f, 1f, 8f, -34f, -46f, -72f);

            // Clear search button (X)
            Text clearBtnLabel;
            CreateButton(mi_root.transform, "AC_SearchClear",
                new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-8f, -61f), new Vector2(20f, 20f),
                "X", () => { mi_searchField.text = ""; }, out clearBtnLabel);
            clearBtnLabel.color = new Color(1f, 0.45f, 0.45f);

            // Close button
            CreateButton(mi_root.transform, "AC_PickerClose",
                new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-4f, -4f), new Vector2(20f, 20f),
                "X", () => Hide(), out _);

            // Scroll view
            GameObject scrollGO = new GameObject("AC_Scroll");
            scrollGO.transform.SetParent(mi_root.transform, false);
            Image scrollBg = scrollGO.AddComponent<Image>();
            scrollBg.color = UIStyleTokens.PickerScrollBackground;
            mi_scrollRect = scrollGO.AddComponent<ScrollRect>();
            mi_scrollRect.horizontal = false;
            mi_scrollRect.vertical = true;
            mi_scrollRect.movementType = ScrollRect.MovementType.Clamped;
            SetRectStretched(scrollGO, 4f, -4f, -60f, 30f);

            // Viewport (RectMask2D, NOT Mask — Mask uses alpha and breaks with clear images)
            GameObject viewportGO = new GameObject("AC_Viewport");
            viewportGO.transform.SetParent(scrollGO.transform, false);
            viewportGO.AddComponent<RectMask2D>();
            SetRectStretched(viewportGO, 0f, 0f, 0f, 0f);
            mi_scrollRect.viewport = viewportGO.GetComponent<RectTransform>();

            // Content container
            GameObject contentGO = new GameObject("AC_Content");
            contentGO.transform.SetParent(viewportGO.transform, false);
            RectTransform contentRT = contentGO.AddComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0f, 1f);
            contentRT.anchorMax = new Vector2(1f, 1f);
            contentRT.pivot     = new Vector2(0f, 1f);
            contentRT.sizeDelta = new Vector2(0f, 0f);
            contentRT.anchoredPosition = Vector2.zero;
            mi_listContent = contentGO.transform;
            mi_scrollRect.content = contentRT;

            // Clear recipe button
            CreateButton(mi_root.transform, "AC_ClearBtn",
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                Vector2.zero, Vector2.zero,
                "Clear Recipe", () => OnClearClicked(), out _);
            SetRectAnchored(mi_root.transform.Find("AC_ClearBtn").gameObject,
                0f, 1f, 0f, 0f, 8f, -8f, 4f, 28f);

            // Status label
            mi_statusLabel = CreateLabel(mi_root.transform, "AC_StatusLbl",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -76f), new Vector2(POPUP_WIDTH - 20f, 16f),
                "", 10, TextAnchor.UpperCenter);
            mi_statusLabel.color = CRaftStyleHelper.ColSubtext;
        }

        public void Show(CrafterBehaviour behaviour, int slotIndex)
        {
            mi_contextBehaviour = behaviour;
            mi_contextSlotIndex = slotIndex;

            AudioFeedbackAdapter.PlayOpen();

            mi_root.SetActive(true);
            // Always start with a clean search when reopening the picker.
            mi_searchField.text = "";
            CollectRecipes();
            BuildRecipeViewModels();
            PopulateList("");
            // Auto-focus the search field so the user can start typing immediately.
            if (UnityEngine.EventSystems.EventSystem.current != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(mi_searchField.gameObject);
        }

        public void Show()
        {
            Show(null, -1);
        }

        public void Hide()
        {
            if (mi_root == null || !mi_root.activeSelf)
                return;

            if (mi_populateRoutine != null)
            {
                StopCoroutine(mi_populateRoutine);
                mi_populateRoutine = null;
            }

            mi_root.SetActive(false);
            mi_onHidden?.Invoke();

            mi_contextBehaviour = null;
            mi_contextSlotIndex = -1;
        }

        // --- Recipe collection & listing ---

        private void CollectRecipes()
        {
            mi_allRecipes.Clear();

            foreach (Item_Base item in ItemManager.GetAllItems())
            {
                if (item == null) continue;
                if (item.settings_Inventory == null) continue;
                string displayName = item.settings_Inventory.DisplayName;
                if (string.IsNullOrEmpty(displayName) || displayName == "An item") continue;
                if (item.settings_recipe == null) continue;
                if (item.settings_recipe.CraftingCategory == CraftingCategory.Nothing) continue;
                if (item.settings_recipe.CraftingCategory == CraftingCategory.Hidden) continue;
                if (item.settings_recipe.NewCost == null || item.settings_recipe.NewCost.Length == 0) continue;
                // Explicit policy: recipe visibility does not apply a learned-state filter.
                mi_allRecipes.Add(item);
            }

            mi_allRecipes = mi_allRecipes
                .OrderBy(i => i.settings_Inventory.DisplayName)
                .ToList();
        }

        private void BuildRecipeViewModels()
        {
            mi_allRecipeViews.Clear();
            Inventory sourceInventory = mi_contextBehaviour?.GetSlotInputInventoryForPreview(mi_contextSlotIndex);

            for (int i = 0; i < mi_allRecipes.Count; i++)
            {
                Item_Base item = mi_allRecipes[i];
                ItemInstance_Recipe recipe = item?.settings_recipe;
                if (item == null || recipe == null)
                    continue;

                RecipeListItemViewModel viewModel = new RecipeListItemViewModel
                {
                    RecipeItem = item,
                    RecipeName = item.settings_Inventory?.DisplayName ?? "Unknown",
                    OutputSprite = item.settings_Inventory?.Sprite,
                    OutputAmount = Mathf.Max(1, recipe.AmountToCraft)
                };

                List<string> ingredientSearchNames = new List<string>();
                CostMultiple[] costs = recipe.NewCost;
                if (costs != null)
                {
                    for (int costIndex = 0; costIndex < costs.Length; costIndex++)
                    {
                        CostMultiple cost = costs[costIndex];
                        if (cost == null)
                            continue;

                        string displayName = BuildIngredientDisplayName(cost);
                        int available = GetAvailableIngredientAmount(sourceInventory, cost);
                        viewModel.Ingredients.Add(new RecipeIngredientViewModel
                        {
                            DisplayName = displayName,
                            RequiredAmount = Mathf.Max(0, cost.amount),
                            AvailableAmount = Mathf.Max(0, available)
                        });
                        ingredientSearchNames.Add(displayName);
                    }
                }

                string ingredientBlob = string.Join(" ", ingredientSearchNames.ToArray());
                viewModel.SearchBlob = (viewModel.RecipeName + " " + ingredientBlob).ToLowerInvariant();
                mi_allRecipeViews.Add(viewModel);
            }
        }

        private void PopulateList(string filter)
        {
            mi_populationGeneration++;
            if (mi_populateRoutine != null)
            {
                StopCoroutine(mi_populateRoutine);
                mi_populateRoutine = null;
            }

            ClearRows();
            mi_populateRoutine = StartCoroutine(PopulateListBatched(filter ?? string.Empty, mi_populationGeneration));
        }

        private IEnumerator PopulateListBatched(string filter, int generation)
        {
            string lower = filter.ToLowerInvariant();
            int batchSize = Mathf.Max(1, CModConfig.RecipePickerBatchSize);
            int maxResults = Mathf.Max(1, CModConfig.MaxRecipePickerResults);

            int matchingCount = 0;
            int rowIndex = 0;

            for (int i = 0; i < mi_allRecipeViews.Count; i++)
            {
                if (generation != mi_populationGeneration)
                    yield break;

                RecipeListItemViewModel viewModel = mi_allRecipeViews[i];
                if (viewModel == null)
                    continue;

                if (!string.IsNullOrEmpty(lower) && (viewModel.SearchBlob == null || !viewModel.SearchBlob.Contains(lower)))
                    continue;

                matchingCount++;
                if (rowIndex >= maxResults)
                    continue;

                GameObject row = CreateRow(mi_listContent, viewModel, rowIndex);
                mi_rows.Add(row);
                rowIndex++;

                if (rowIndex % batchSize == 0)
                {
                    UpdateContentSize();
                    UpdateStatusLabel(filter, matchingCount, rowIndex, maxResults);
                    yield return null;
                }
            }

            UpdateContentSize();
            UpdateStatusLabel(filter, matchingCount, rowIndex, maxResults);
            if (mi_scrollRect != null)
                mi_scrollRect.normalizedPosition = new Vector2(0f, 1f);

            mi_populateRoutine = null;
        }

        private void ClearRows()
        {
            for (int i = 0; i < mi_rows.Count; i++)
            {
                GameObject row = mi_rows[i];
                if (row != null)
                    Destroy(row);
            }

            mi_rows.Clear();
            UpdateContentSize();
        }

        private void UpdateContentSize()
        {
            RectTransform contentRT = mi_listContent?.GetComponent<RectTransform>();
            if (contentRT == null)
                return;

            contentRT.sizeDelta = new Vector2(0f, mi_rows.Count * (ITEM_HEIGHT + 2f));
            contentRT.anchoredPosition = Vector2.zero;
        }

        private void UpdateStatusLabel(string filter, int matchingCount, int shownCount, int maxResults)
        {
            if (mi_statusLabel == null)
                return;

            if (mi_allRecipeViews.Count == 0)
            {
                mi_statusLabel.text = "No craftable recipes found.";
                return;
            }

            if (matchingCount == 0)
            {
                mi_statusLabel.text = "No results for \"" + filter + "\"";
                return;
            }

            bool truncated = matchingCount > shownCount || shownCount >= maxResults;
            if (truncated)
                mi_statusLabel.text = shownCount + " of " + matchingCount + " shown (cap " + maxResults + ")";
            else
                mi_statusLabel.text = shownCount + " of " + matchingCount + " recipes";
        }

        private GameObject CreateRow(Transform parent, RecipeListItemViewModel viewModel, int rowIndex)
        {
            GameObject go = new GameObject("AC_Row_" + rowIndex);
            go.transform.SetParent(parent, false);

            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0f, 1f);
            rt.anchorMax        = new Vector2(1f, 1f);
            rt.pivot            = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -(rowIndex * (ITEM_HEIGHT + 2f)));
            rt.sizeDelta        = new Vector2(0f, ITEM_HEIGHT);

            // Background
            Image bg = go.AddComponent<Image>();
            bg.color = UIStyleTokens.PickerRow;

            // Hover
            Color normalColor = bg.color;
            Color hoverColor  = UIStyleTokens.PickerRowHover;
            EventTrigger evtTrigger = go.AddComponent<EventTrigger>();
            AddHoverEvents(evtTrigger, bg, normalColor, hoverColor);

            // Click
            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.transition    = Selectable.Transition.None;
            btn.onClick.AddListener(() => OnItemClicked(viewModel.RecipeItem));

            // --- Top: Icon + Name ---
            Sprite sprite = viewModel.OutputSprite;
            float iconW = ICON_SIZE;
            if (sprite != null)
            {
                CreateImage(go.transform, "Icon", sprite,
                    new Vector2(0f, 1f), new Vector2(4f, -4f),
                    new Vector2(iconW, iconW));
            }

            // Item name
            Text nameLbl = CreateLabel(go.transform, "Name",
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                Vector2.zero, Vector2.zero,
                viewModel.RecipeName, 13, TextAnchor.MiddleLeft);
            nameLbl.fontStyle = FontStyle.Bold;
            nameLbl.raycastTarget = false;
            SetRectAnchored(nameLbl.gameObject, 0f, 1f, 1f, 1f,
                iconW + 8f, -4f, -2f, -(ICON_SIZE + 2f));

            // Output amount and compact production hint
            int craftAmount = viewModel.OutputAmount;
            Text amtLbl = CreateLabel(go.transform, "Amt",
                new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-4f, -4f), new Vector2(56f, 20f),
                "x" + craftAmount, 11, TextAnchor.MiddleRight);
            amtLbl.color = CRaftStyleHelper.ColGood;
            amtLbl.raycastTarget = false;

            Text outputLbl = CreateLabel(go.transform, "Output",
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                Vector2.zero, Vector2.zero,
                "Output per craft: " + craftAmount, 10, TextAnchor.MiddleLeft);
            outputLbl.raycastTarget = false;
            outputLbl.color = UIStyleTokens.AccentSoft;
            SetRectAnchored(outputLbl.gameObject, 0f, 1f, 1f, 1f,
                iconW + 8f, -6f, -(ICON_SIZE + 8f), -50f);

            // --- Bottom: Ingredient requirement/availability ---
            string ingredientsText = BuildIngredientSummary(viewModel);
            Text ingLbl = CreateLabel(go.transform, "IngLbl",
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                Vector2.zero, Vector2.zero,
                ingredientsText, 10, TextAnchor.UpperLeft);
            ingLbl.color = CRaftStyleHelper.ColSubtext;
            ingLbl.raycastTarget = false;
            SetRectAnchored(ingLbl.gameObject, 0f, 1f, 1f, 1f,
                6f, -6f, -62f, -84f);

            return go;
        }

        private static string BuildIngredientSummary(RecipeListItemViewModel viewModel)
        {
            if (viewModel == null || viewModel.Ingredients == null || viewModel.Ingredients.Count == 0)
                return "Ingredients: none";

            int totalRequired = 0;
            int totalAvailable = 0;
            List<string> parts = new List<string>();
            for (int i = 0; i < viewModel.Ingredients.Count; i++)
            {
                RecipeIngredientViewModel ingredient = viewModel.Ingredients[i];
                if (ingredient == null)
                    continue;

                totalRequired += ingredient.RequiredAmount;
                totalAvailable += ingredient.AvailableAmount;
                parts.Add(ingredient.DisplayName + " " + ingredient.AvailableAmount + "/" + ingredient.RequiredAmount);
            }

            return "Ingredients " + totalAvailable + "/" + totalRequired + "  |  " + string.Join("  |  ", parts.ToArray());
        }

        private static int GetAvailableIngredientAmount(Inventory inventory, CostMultiple cost)
        {
            if (inventory == null || cost == null || cost.items == null)
                return 0;

            int available = 0;
            for (int i = 0; i < cost.items.Length; i++)
            {
                Item_Base option = cost.items[i];
                if (option == null)
                    continue;

                available += inventory.GetItemCount(option.UniqueName);
            }

            return available;
        }

        private static string BuildIngredientDisplayName(CostMultiple cost)
        {
            if (cost == null || cost.items == null || cost.items.Length == 0)
                return "Unknown";

            List<string> names = new List<string>();
            for (int i = 0; i < cost.items.Length; i++)
            {
                Item_Base option = cost.items[i];
                if (option == null)
                    continue;

                string displayName = option.settings_Inventory?.DisplayName;
                if (string.IsNullOrEmpty(displayName))
                    displayName = option.UniqueName;

                if (!names.Contains(displayName))
                    names.Add(displayName);
            }

            if (names.Count == 0)
                return "Unknown";

            return string.Join(" / ", names.ToArray());
        }

        // --- Callbacks ---

        private void OnSearchChanged(string value) => PopulateList(value);
        private void OnItemClicked(Item_Base item)
        {
            AudioFeedbackAdapter.PlayClick();
            mi_onSelected?.Invoke(item);
            Hide();
        }

        private void OnClearClicked()
        {
            AudioFeedbackAdapter.PlayClick();
            mi_onSelected?.Invoke(null);
            Hide();
        }

        // --- Shared UGUI helpers ---

        private static Text CreateLabel(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size,
            string text, int fontSize, TextAnchor alignment)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            Text lbl = go.AddComponent<Text>();
            CRaftStyleHelper.Apply(lbl, fontSize, alignment);
            lbl.text = text;
            return lbl;
        }

        private static void CreateImage(Transform parent, string name, Sprite sprite,
            Vector2 anchor, Vector2 pos, Vector2 size)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchor; rt.anchorMax = anchor;
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            Image img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
        }

        private static InputField CreateInputField(Transform parent, string name, string placeholder)
        {
            return UIFactory.CreateInputField(parent, name, placeholder);
        }

        private static void CreateButton(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size,
            string label, Action callback, out Text labelOut)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            Image img = go.AddComponent<Image>();
            CRaftStyleHelper.ApplyButton(img, CRaftStyleHelper.ColBtn);
            Button btn = go.AddComponent<Button>();
            btn.image = img;
            if (callback != null)
            {
                btn.onClick.AddListener(() =>
                {
                    AudioFeedbackAdapter.PlayClick();
                    callback();
                });
            }

            GameObject lblGO = new GameObject("Label");
            lblGO.transform.SetParent(go.transform, false);
            RectTransform lr = lblGO.AddComponent<RectTransform>();
            lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
            lr.offsetMin = Vector2.zero; lr.offsetMax = Vector2.zero;
            Text txt = lblGO.AddComponent<Text>();
            CRaftStyleHelper.Apply(txt, 12, TextAnchor.MiddleCenter);
            txt.text = label;
            labelOut = txt;
        }

        private static void AddHoverEvents(EventTrigger trigger, Image bg,
            Color normalColor, Color hoverColor)
        {
            EventTrigger.Entry enter = new EventTrigger.Entry();
            enter.eventID = EventTriggerType.PointerEnter;
            enter.callback.AddListener(_ => bg.color = hoverColor);
            trigger.triggers.Add(enter);

            EventTrigger.Entry exit = new EventTrigger.Entry();
            exit.eventID = EventTriggerType.PointerExit;
            exit.callback.AddListener(_ => bg.color = normalColor);
            trigger.triggers.Add(exit);
        }

        /// <summary>Sets anchored rect using anchor min/max and offset min/max.</summary>
        private static void SetRectAnchored(GameObject go,
            float aMinX, float aMaxX, float aMinY, float aMaxY,
            float oMinX, float oMaxX, float oMinY, float oMaxY)
        {
            RectTransform rt = go.GetComponent<RectTransform>();
            if (rt == null) return;
            rt.anchorMin = new Vector2(aMinX, aMinY);
            rt.anchorMax = new Vector2(aMaxX, aMaxY);
            rt.offsetMin = new Vector2(oMinX, oMinY);
            rt.offsetMax = new Vector2(oMaxX, oMaxY);
        }

        /// <summary>Sets a stretch rect with left/right/top/bottom offsets from parent edges.</summary>
        private static void SetRectStretched(GameObject go, float left, float right, float top, float bottom)
        {
            RectTransform rt = go.GetComponent<RectTransform>();
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(right, top);
        }
    }
}
