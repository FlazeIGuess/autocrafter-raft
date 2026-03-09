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
    /// Popup scroll list for assigning input or output containers.
    /// Shows all Storage_Small chests on the raft, sorted by distance.
    /// Stale references remain visible but are non-selectable.
    /// Used for both input and output container selection.
    /// </summary>
    public class CUIContainerPicker : MonoBehaviour
    {
        private const float POPUP_WIDTH = 280f;
        private const float ITEM_HEIGHT = 42f;

        private Action<Storage_Small> mi_onSelected;
        private Action mi_onHidden;
        private GameObject mi_root;
        private Transform mi_listContent;
        private ScrollRect mi_scrollRect;
        private Text mi_statusLabel;
        private Text mi_headerLabel;

        private uint mi_excludeObjectIndex;
        private CrafterBehaviour mi_contextBehaviour;
        private int mi_contextSlotIndex = -1;
        private bool mi_contextInputMode;
        private int mi_recipeRequiredUnits;
        private int mi_recipeAggregateUnits;
        private int mi_recipeContributorCount;

        private readonly AutoCrafterUIBridge mi_bridge = new AutoCrafterUIBridge();
        private List<GameObject> mi_rows = new List<GameObject>();
        private Coroutine mi_populateRoutine;
        private int mi_populationGeneration;

        public void Build(Transform parent, Action<Storage_Small> onSelected, Action onHidden)
        {
            mi_onSelected = onSelected;
            mi_onHidden = onHidden;

            mi_root = new GameObject("AC_ContainerPickerPanel");
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
            mi_headerLabel = CreateLabel(mi_root.transform, "AC_ContPickerHeader",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -8f), new Vector2(POPUP_WIDTH - 20f, 22f),
                "Select Container", 13, TextAnchor.UpperCenter);
            mi_headerLabel.color = CRaftStyleHelper.ColAccent;

            // Close button
            CreateButton(mi_root.transform, "AC_ContPickerClose",
                new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-4f, -4f), new Vector2(20f, 20f),
                "X", () => Hide());

            // Scroll view
            GameObject scrollGO = new GameObject("AC_ContScroll");
            scrollGO.transform.SetParent(mi_root.transform, false);
            Image scrollBg = scrollGO.AddComponent<Image>();
            scrollBg.color = UIStyleTokens.PickerScrollBackground;
            mi_scrollRect = scrollGO.AddComponent<ScrollRect>();
            mi_scrollRect.horizontal = false;
            mi_scrollRect.vertical = true;
            mi_scrollRect.movementType = ScrollRect.MovementType.Clamped;
            SetRectStretched(scrollGO, 4f, -4f, -36f, 30f);

            // Viewport with RectMask2D (NOT Mask)
            GameObject viewportGO = new GameObject("AC_ContViewport");
            viewportGO.transform.SetParent(scrollGO.transform, false);
            viewportGO.AddComponent<RectMask2D>();
            SetRectStretched(viewportGO, 0f, 0f, 0f, 0f);
            mi_scrollRect.viewport = viewportGO.GetComponent<RectTransform>();

            // Content
            GameObject contentGO = new GameObject("AC_ContContent");
            contentGO.transform.SetParent(viewportGO.transform, false);
            RectTransform contentRT = contentGO.AddComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0f, 1f);
            contentRT.anchorMax = new Vector2(1f, 1f);
            contentRT.pivot     = new Vector2(0f, 1f);
            contentRT.sizeDelta = Vector2.zero;
            contentRT.anchoredPosition = Vector2.zero;
            mi_listContent = contentGO.transform;
            mi_scrollRect.content = contentRT;

            // Clear button
            CreateButton(mi_root.transform, "AC_ContClearBtn",
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                Vector2.zero, Vector2.zero,
                "Use Own Chest (Clear)", () => OnClearClicked());
            SetRectAnchored(mi_root.transform.Find("AC_ContClearBtn").gameObject,
                0f, 1f, 0f, 0f, 8f, -8f, 4f, 28f);

            // Status label
            mi_statusLabel = CreateLabel(mi_root.transform, "AC_ContStatusLbl",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -36f), new Vector2(POPUP_WIDTH - 16f, 14f),
                "", 10, TextAnchor.UpperCenter);
            mi_statusLabel.color = CRaftStyleHelper.ColSubtext;
        }

        /// <summary>Shows the container picker. Title changes based on mode.</summary>
        public void Show(CrafterBehaviour behaviour, int slotIndex, bool inputMode,
            uint excludeObjectIndex, string title = "Select Container")
        {
            mi_contextBehaviour = behaviour;
            mi_contextSlotIndex = slotIndex;
            mi_contextInputMode = inputMode;
            mi_excludeObjectIndex = excludeObjectIndex;
            mi_headerLabel.text = title;
            AudioFeedbackAdapter.PlayOpen();
            mi_root.SetActive(true);
            PopulateList();
        }

        public void Show(uint excludeObjectIndex, string title = "Select Container")
        {
            Show(null, -1, false, excludeObjectIndex, title);
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
            mi_contextInputMode = false;
            mi_recipeRequiredUnits = 0;
            mi_recipeAggregateUnits = 0;
            mi_recipeContributorCount = 0;
        }

        private void PopulateList()
        {
            mi_populationGeneration++;
            if (mi_populateRoutine != null)
            {
                StopCoroutine(mi_populateRoutine);
                mi_populateRoutine = null;
            }

            ClearRows();

            Transform crafterTransform = null;
            List<ContainerListItemViewModel> items = new List<ContainerListItemViewModel>();
            Dictionary<int, Storage_Small> liveByIndex = new Dictionary<int, Storage_Small>();

            if (mi_bridge.TryGetStorage(mi_excludeObjectIndex, out Storage_Small crafterStorage))
                crafterTransform = crafterStorage.transform;

            foreach (Storage_Small storage in StorageManager.allStorages)
            {
                if (storage == null) continue;
                int index = (int)storage.ObjectIndex;
                if (index <= 0 || storage.ObjectIndex == mi_excludeObjectIndex)
                    continue;

                if (!liveByIndex.ContainsKey(index))
                    liveByIndex.Add(index, storage);
            }

            ItemInstance_Recipe contextRecipe = mi_contextInputMode
                ? mi_contextBehaviour?.GetSlotRecipeForPreview(mi_contextSlotIndex)
                : null;
            mi_recipeRequiredUnits = mi_contextInputMode
                ? mi_contextBehaviour?.GetIngredientRequirementForRecipe(contextRecipe) ?? 0
                : 0;

            mi_recipeAggregateUnits = 0;
            mi_recipeContributorCount = 0;
            if (mi_contextInputMode && contextRecipe != null && mi_contextBehaviour != null)
            {
                foreach (Storage_Small storage in liveByIndex.Values)
                {
                    Inventory inv = storage.GetInventoryReference();
                    int available = mi_contextBehaviour.GetIngredientAvailabilityForRecipe(inv, contextRecipe);
                    mi_recipeAggregateUnits += available;
                    if (available > 0)
                        mi_recipeContributorCount++;
                }
            }

            List<Storage_Small> sortedLive = liveByIndex.Values.ToList();
            if (crafterTransform != null)
            {
                Vector3 crafterPos = crafterTransform.position;
                sortedLive = sortedLive
                    .OrderBy(s => Vector3.Distance(s.transform.position, crafterPos))
                    .ToList();
            }

            foreach (Storage_Small storage in sortedLive)
            {
                float dist = 0f;
                if (crafterTransform != null)
                    dist = Vector3.Distance(storage.transform.position, crafterTransform.position);

                items.Add(BuildLiveContainerItem(storage, dist, contextRecipe));
            }

            List<int> referenced = mi_bridge.GetReferencedContainerIndices(mi_contextBehaviour, mi_contextInputMode);
            referenced.Sort();
            for (int i = 0; i < referenced.Count; i++)
            {
                int staleIndex = referenced[i];
                if (staleIndex <= 0 || staleIndex == (int)mi_excludeObjectIndex)
                    continue;

                if (liveByIndex.ContainsKey(staleIndex))
                    continue;

                items.Add(BuildMissingContainerItem(staleIndex));
            }

            if (items.Count == 0)
            {
                if (mi_statusLabel != null)
                    mi_statusLabel.text = "No other chests found.";
                UpdateContentSize();
                return;
            }

            mi_populateRoutine = StartCoroutine(PopulateRowsBatched(items, mi_populationGeneration));
        }

        private IEnumerator PopulateRowsBatched(List<ContainerListItemViewModel> items, int generation)
        {
            int batchSize = Mathf.Max(1, CModConfig.ContainerPickerBatchSize);
            for (int i = 0; i < items.Count; i++)
            {
                if (generation != mi_populationGeneration)
                    yield break;

                GameObject row = CreateRow(mi_listContent, items[i], i);
                mi_rows.Add(row);

                if ((i + 1) % batchSize == 0)
                {
                    UpdateContentSize();
                    if (mi_statusLabel != null)
                        mi_statusLabel.text = BuildStatusText(items);
                    yield return null;
                }
            }

            UpdateContentSize();
            if (mi_statusLabel != null)
                mi_statusLabel.text = BuildStatusText(items);

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
        }

        private void UpdateContentSize()
        {
            RectTransform contentRT = mi_listContent?.GetComponent<RectTransform>();
            if (contentRT == null)
                return;

            contentRT.sizeDelta = new Vector2(0f, mi_rows.Count * (ITEM_HEIGHT + 2f));
            contentRT.anchoredPosition = Vector2.zero;
        }

        private ContainerListItemViewModel BuildLiveContainerItem(Storage_Small storage, float distance, ItemInstance_Recipe contextRecipe)
        {
            CrafterBehaviour beh = storage.GetComponent<CrafterBehaviour>();
            bool isAC = beh != null && beh.Data != null && beh.Data.IsUpgraded;
            int objectIndex = (int)storage.ObjectIndex;

            ContainerListItemViewModel item = new ContainerListItemViewModel();
            item.ObjectIndex = objectIndex;
            item.DisplayName = mi_bridge.BuildContainerIdentityLabel(objectIndex);
            item.IdentityMarker = "#" + objectIndex;
            item.DistanceMeters = distance;
            item.IsAutoCrafterChest = isAC;
            item.IsStaleReference = false;
            item.IsSelectable = true;
            item.Storage = storage;
            item.Subtitle = BuildRowSubtitle(storage, distance, contextRecipe);
            return item;
        }

        private ContainerListItemViewModel BuildMissingContainerItem(int objectIndex)
        {
            ContainerListItemViewModel item = new ContainerListItemViewModel();
            item.ObjectIndex = objectIndex;
            item.DisplayName = mi_bridge.BuildContainerIdentityLabel(objectIndex);
            item.IdentityMarker = "#" + objectIndex;
            item.DistanceMeters = -1f;
            item.IsAutoCrafterChest = false;
            item.IsStaleReference = true;
            item.IsSelectable = false;
            item.Storage = null;
            item.Subtitle = "Missing reference. Chest is not currently available.";
            return item;
        }

        private string BuildRowSubtitle(Storage_Small storage, float distance, ItemInstance_Recipe contextRecipe)
        {
            List<string> parts = new List<string>();
            if (distance > 0f)
                parts.Add("Distance " + distance.ToString("F1") + "m");

            if (mi_contextInputMode && contextRecipe != null && mi_contextBehaviour != null)
            {
                Inventory inv = storage.GetInventoryReference();
                int sourceUnits = mi_contextBehaviour.GetIngredientAvailabilityForRecipe(inv, contextRecipe);
                string sourcePart = "source " + sourceUnits + "/" + mi_recipeRequiredUnits;
                if (mi_recipeContributorCount > 1)
                {
                    sourcePart += "  |  aggregate " + mi_recipeAggregateUnits + "/" + mi_recipeRequiredUnits
                        + " from " + mi_recipeContributorCount + " chest(s)";
                }

                parts.Add(sourcePart);
            }

            return parts.Count > 0 ? string.Join("  |  ", parts.ToArray()) : string.Empty;
        }

        private string BuildStatusText(List<ContainerListItemViewModel> items)
        {
            int selectable = 0;
            int stale = 0;
            for (int i = 0; i < items.Count; i++)
            {
                ContainerListItemViewModel item = items[i];
                if (item == null)
                    continue;

                if (item.IsSelectable)
                    selectable++;
                else if (item.IsStaleReference)
                    stale++;
            }

            string text = selectable + " chest(s) available";
            if (stale > 0)
                text += " | " + stale + " missing reference(s)";

            return text;
        }

        private GameObject CreateRow(Transform parent, ContainerListItemViewModel item, int rowIndex)
        {
            GameObject go = new GameObject("AC_ContRow_" + rowIndex);
            go.transform.SetParent(parent, false);

            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0f, 1f);
            rt.anchorMax        = new Vector2(1f, 1f);
            rt.pivot            = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -(rowIndex * (ITEM_HEIGHT + 2f)));
            rt.sizeDelta        = new Vector2(0f, ITEM_HEIGHT);

            Image bg = go.AddComponent<Image>();
            Color normalColor = UIStyleTokens.PickerRow;
            Color hoverColor  = UIStyleTokens.PickerRowHover;
            if (item.IsStaleReference)
            {
                normalColor = UIStyleTokens.PickerRowMissing;
                hoverColor = normalColor;
            }
            bg.color = normalColor;

            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.transition = Selectable.Transition.None;
            btn.interactable = item.IsSelectable;
            if (item.IsSelectable)
            {
                EventTrigger evtTrigger = go.AddComponent<EventTrigger>();
                AddHoverEvents(evtTrigger, bg, normalColor, hoverColor);
                btn.onClick.AddListener(() => OnContainerClicked(item.Storage));
            }

            string rowLabel = item.DisplayName;
            if (!string.IsNullOrEmpty(item.IdentityMarker) && !rowLabel.Contains(item.IdentityMarker))
                rowLabel += " (" + item.IdentityMarker + ")";

            if (item.IsStaleReference)
                rowLabel = "[MISSING] " + rowLabel;
            else if (item.IsAutoCrafterChest)
                rowLabel = "[AUTOCRAFTER] " + rowLabel;
            else
                rowLabel = "[LIVE] " + rowLabel;

            Text lbl = CreateLabel(go.transform, "LblMain",
                new Vector2(0f, 0.5f), new Vector2(1f, 1f),
                Vector2.zero, Vector2.zero,
                rowLabel, 11, TextAnchor.MiddleLeft);
            lbl.raycastTarget = false;
            SetRectAnchored(lbl.gameObject, 0f, 1f, 0f, 1f, 8f, -4f, 18f, -1f);
            lbl.color = item.IsStaleReference
                ? CRaftStyleHelper.ColBad
                : (item.IsAutoCrafterChest ? UIStyleTokens.AccentSoft : CRaftStyleHelper.ColText);

            Text subLbl = CreateLabel(go.transform, "LblSub",
                new Vector2(0f, 0f), new Vector2(1f, 0.5f),
                Vector2.zero, Vector2.zero,
                item.Subtitle ?? string.Empty, 9, TextAnchor.MiddleLeft);
            subLbl.raycastTarget = false;
            SetRectAnchored(subLbl.gameObject, 0f, 1f, 0f, 1f, 8f, -4f, 2f, -20f);
            subLbl.color = item.IsStaleReference ? new Color(1f, 0.7f, 0.7f) : CRaftStyleHelper.ColSubtext;

            return go;
        }

        private void OnContainerClicked(Storage_Small storage)
        {
            AudioFeedbackAdapter.PlayClick();
            mi_onSelected?.Invoke(storage);
            Hide();
        }

        private void OnClearClicked()
        {
            AudioFeedbackAdapter.PlayClick();
            mi_onSelected?.Invoke(null);
            Hide();
        }

        // --- UGUI helpers ---

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

        private static void CreateButton(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size,
            string label, Action callback)
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
            CRaftStyleHelper.Apply(txt, 11, TextAnchor.MiddleCenter);
            txt.text = label;
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
