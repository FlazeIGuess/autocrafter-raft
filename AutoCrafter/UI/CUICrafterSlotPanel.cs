using System;
using UnityEngine;
using UnityEngine.UI;

namespace pp.RaftMods.AutoCrafter
{
    /// <summary>
    /// UI panel for one crafting slot (compact alpha layout).
    ///
    /// - Recipe selector button
    /// - Active toggle
    /// - Infinite / count switcher (count input validates: must be > 0, turns red on bad input)
    /// - Output container picker button
    /// - Input container picker button (NEW)
    /// - Status indicator label
    /// </summary>
    public class CUICrafterSlotPanel : MonoBehaviour
    {
        private readonly AutoCrafterUIBridge mi_bridge = new AutoCrafterUIBridge();

        private CCrafterSlot    mi_slot;
        private int             mi_slotIndex;
        private CrafterBehaviour mi_behaviour;
        private Action          mi_openPickerCallback;
        private Action          mi_openOutputPickerCallback;
        private Action          mi_openInputPickerCallback;

        private Text       mi_recipeLabel;
        private Text       mi_recipeMetaLabel;
        private Text       mi_statusLabel;
        private Text       mi_outputLabel;
        private Text       mi_inputLabel;
        private Toggle     mi_activeToggle;
        private Toggle     mi_infiniteToggle;
        private InputField mi_countInput;
        private Image      mi_countInputBg;
        private Image      mi_statusBadgeBg;
        private GameObject mi_countRow;

        public void Build(Transform parent, CCrafterSlot slot, int slotIndex,
            Action openPickerCallback,
            Action openOutputPickerCallback,
            Action openInputPickerCallback,
            CrafterBehaviour behaviour)
        {
            mi_slot                    = slot;
            mi_slotIndex               = slotIndex;
            mi_behaviour               = behaviour;
            mi_openPickerCallback      = openPickerCallback;
            mi_openOutputPickerCallback = openOutputPickerCallback;
            mi_openInputPickerCallback  = openInputPickerCallback;

            // Slot background
            Image bg = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            CRaftStyleHelper.ApplyPanel(bg, UIStyleTokens.SlotPanel);
            Outline edge = GetComponent<Outline>() ?? gameObject.AddComponent<Outline>();
            edge.effectColor = UIStyleTokens.SlotPanelEdge;
            edge.effectDistance = new Vector2(1f, -1f);

            float y = -4f;

            // "Slot N" label
            Text slotNumLbl = UIFactory.CreateLabel(parent, "AC_SlotNum_" + slotIndex,
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(6f, y), new Vector2(60f, 18f),
                "Slot " + (slotIndex + 1), 11, TextAnchor.MiddleLeft);
            slotNumLbl.color     = UIStyleTokens.AccentSoft;
            slotNumLbl.fontStyle = FontStyle.Bold;

            // Recipe button (stretches from after label to right edge)
            GameObject recipeGO = UIFactory.CreateButtonGO(parent, "AC_RecipeBtn_" + slotIndex,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                Vector2.zero, Vector2.zero,
                BuildRecipeTitle(slot), () => { PlayClick(); mi_openPickerCallback?.Invoke(); });
            SetRectAnchored(recipeGO, 0f, 1f, 1f, 1f, 68f, -6f, y - 18f, y);
            mi_recipeLabel = recipeGO.GetComponentInChildren<Text>();
            CRaftStyleHelper.Apply(mi_recipeLabel, 11, TextAnchor.MiddleLeft);

            mi_recipeMetaLabel = UIFactory.CreateLabel(parent, "AC_RecipeMeta_" + slotIndex,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                Vector2.zero, Vector2.zero,
                BuildRecipeMeta(slot), 9, TextAnchor.MiddleLeft);
            SetRectAnchored(mi_recipeMetaLabel.gameObject, 0f, 1f, 1f, 1f, 68f, -6f, y - 34f, y - 18f);
            mi_recipeMetaLabel.color = CRaftStyleHelper.ColSubtext;

            // Active toggle row
            y -= 38f;
            Text activeLbl = UIFactory.CreateLabel(parent, "AC_ActiveLbl_" + slotIndex,
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(6f, y), new Vector2(78f, 18f),
                "Enabled:", 11, TextAnchor.MiddleLeft);
            CRaftStyleHelper.Apply(activeLbl, 11, TextAnchor.MiddleLeft);
            mi_activeToggle = UIFactory.CreateToggle(parent, "AC_ActiveToggle_" + slotIndex,
                new Vector2(88f, y + 1f), new Vector2(18f, 18f),
                slot.IsActive, OnActiveChanged);

            // Infinite toggle + count row
            y -= 22f;
            Text infLbl = UIFactory.CreateLabel(parent, "AC_InfLbl_" + slotIndex,
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(6f, y), new Vector2(78f, 18f),
                "Loop:", 11, TextAnchor.MiddleLeft);
            CRaftStyleHelper.Apply(infLbl, 11, TextAnchor.MiddleLeft);
            mi_infiniteToggle = UIFactory.CreateToggle(parent, "AC_InfToggle_" + slotIndex,
                new Vector2(88f, y + 1f), new Vector2(18f, 18f),
                slot.IsInfinite, OnInfiniteChanged);

            // Count row (shown when NOT infinite)
            mi_countRow = new GameObject("AC_CountRow_" + slotIndex);
            mi_countRow.transform.SetParent(parent, false);
            RectTransform cr = mi_countRow.AddComponent<RectTransform>();
            cr.anchorMin        = new Vector2(0f, 1f);
            cr.anchorMax        = new Vector2(0f, 1f);
            cr.anchoredPosition = new Vector2(116f, y + 1f);
            cr.sizeDelta        = new Vector2(140f, 18f);

            Text cntLbl = UIFactory.CreateLabel(mi_countRow.transform, "AC_CntLbl_" + slotIndex,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(0f, 0f), new Vector2(46f, 18f),
                "Count:", 11, TextAnchor.MiddleLeft);
            CRaftStyleHelper.Apply(cntLbl, 11, TextAnchor.MiddleLeft);

            mi_countInput = UIFactory.CreateInputField(mi_countRow.transform,
                "AC_CntInput_" + slotIndex, "1");
            RectTransform ciRT = mi_countInput.gameObject.GetComponent<RectTransform>();
            ciRT.anchorMin        = new Vector2(0f, 0.5f);
            ciRT.anchorMax        = new Vector2(0f, 0.5f);
            ciRT.anchoredPosition = new Vector2(74f, 0f);
            ciRT.sizeDelta        = new Vector2(62f, 18f);
            mi_countInput.contentType = InputField.ContentType.IntegerNumber;
            mi_countInput.text        = slot.RemainingCount > 0 ? slot.RemainingCount.ToString() : "1";
            mi_countInput.onEndEdit.AddListener(OnCountChanged);
            UIFactory.AddInputFocusGuard(mi_countInput);
            mi_countInputBg = mi_countInput.gameObject.GetComponent<Image>();
            mi_countRow.SetActive(!slot.IsInfinite);

            // Output container button
            y -= 24f;
            string outputText = GetContainerDisplayText("Output", slot.HasOutputContainer,
                slot.OutputContainerIndex, mi_behaviour?.GetResolvedOutputStorageForSlot(slotIndex));
            GameObject outputGO = UIFactory.CreateButtonGO(parent, "AC_OutputBtn_" + slotIndex,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                Vector2.zero, Vector2.zero,
                outputText, () => { PlayClick(); mi_openOutputPickerCallback?.Invoke(); });
            SetRectAnchored(outputGO, 0f, 1f, 1f, 1f, 6f, -6f, y - 18f, y);
            mi_outputLabel = outputGO.GetComponentInChildren<Text>();
            CRaftStyleHelper.Apply(mi_outputLabel, 10, TextAnchor.MiddleLeft);
            mi_outputLabel.color = GetContainerLabelColor(slot.HasOutputContainer,
                slot.OutputContainerIndex,
                mi_behaviour?.GetResolvedOutputStorageForSlot(slotIndex),
                false);

            // Input container button
            y -= 22f;
            string inputText = GetContainerDisplayText("Input", slot.HasInputContainer,
                slot.InputContainerIndex, mi_behaviour?.GetResolvedInputStorageForSlot(slotIndex));
            GameObject inputGO = UIFactory.CreateButtonGO(parent, "AC_InputBtn_" + slotIndex,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                Vector2.zero, Vector2.zero,
                inputText, () => { PlayClick(); mi_openInputPickerCallback?.Invoke(); });
            SetRectAnchored(inputGO, 0f, 1f, 1f, 1f, 6f, -6f, y - 18f, y);
            mi_inputLabel = inputGO.GetComponentInChildren<Text>();
            CRaftStyleHelper.Apply(mi_inputLabel, 10, TextAnchor.MiddleLeft);
            mi_inputLabel.color = GetContainerLabelColor(slot.HasInputContainer,
                slot.InputContainerIndex,
                mi_behaviour?.GetResolvedInputStorageForSlot(slotIndex),
                true);

            // Status label
            y -= 24f;
            GameObject statusBadge = new GameObject("AC_StatusBadge_" + slotIndex);
            statusBadge.transform.SetParent(parent, false);
            statusBadge.AddComponent<RectTransform>();
            SetRectAnchored(statusBadge, 0f, 1f, 1f, 1f, 6f, -6f, y - 18f, y);
            mi_statusBadgeBg = statusBadge.AddComponent<Image>();
            CRaftStyleHelper.ApplyButton(mi_statusBadgeBg, UIStyleTokens.BadgeNeutral);

            mi_statusLabel = UIFactory.CreateLabel(statusBadge.transform, "AC_StatusLbl_" + slotIndex,
                Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero,
                "", 10, TextAnchor.MiddleLeft);
            SetRectAnchored(mi_statusLabel.gameObject, 0f, 1f, 0f, 1f, 8f, -4f, 0f, 0f);

            RefreshStatus();
        }

        // ------------------------------------------------------------------
        //  Public refresh methods
        // ------------------------------------------------------------------

        public void RefreshStatus()
        {
            if (mi_slot == null || mi_statusLabel == null) return;

            SlotStatusViewModel status = mi_behaviour?.GetSlotStatusViewModel(mi_slotIndex);
            if (status == null)
                status = SlotStatusViewModel.Create(SlotCraftabilityState.Inactive, "Slot unavailable");

            mi_statusLabel.text = status.DisplayText;
            switch (status.State)
            {
                case SlotCraftabilityState.Ready:
                    mi_statusLabel.color = CRaftStyleHelper.ColGood;
                    if (mi_statusBadgeBg != null)
                        mi_statusBadgeBg.color = UIStyleTokens.BadgeReady;
                    break;
                case SlotCraftabilityState.Inactive:
                    mi_statusLabel.color = CRaftStyleHelper.ColSubtext;
                    if (mi_statusBadgeBg != null)
                        mi_statusBadgeBg.color = UIStyleTokens.BadgeNeutral;
                    break;
                case SlotCraftabilityState.OutputFull:
                    mi_statusLabel.color = CRaftStyleHelper.ColWarn;
                    if (mi_statusBadgeBg != null)
                        mi_statusBadgeBg.color = UIStyleTokens.BadgeWarn;
                    break;
                case SlotCraftabilityState.NoOutputContainer:
                case SlotCraftabilityState.MissingIngredients:
                default:
                    mi_statusLabel.color = CRaftStyleHelper.ColBad;
                    if (mi_statusBadgeBg != null)
                        mi_statusBadgeBg.color = UIStyleTokens.BadgeBad;
                    break;
            }
        }

        public void RefreshOutputLabel()
        {
            if (mi_outputLabel == null || mi_slot == null) return;
            mi_outputLabel.text = GetContainerDisplayText("Output",
                mi_slot.HasOutputContainer,
                mi_slot.OutputContainerIndex,
                mi_behaviour?.GetResolvedOutputStorageForSlot(mi_slotIndex));
            mi_outputLabel.color = GetContainerLabelColor(
                mi_slot.HasOutputContainer,
                mi_slot.OutputContainerIndex,
                mi_behaviour?.GetResolvedOutputStorageForSlot(mi_slotIndex),
                false);
        }

        public void RefreshInputLabel()
        {
            if (mi_inputLabel == null || mi_slot == null) return;
            mi_inputLabel.text = GetContainerDisplayText("Input",
                mi_slot.HasInputContainer,
                mi_slot.InputContainerIndex,
                mi_behaviour?.GetResolvedInputStorageForSlot(mi_slotIndex));
            mi_inputLabel.color = GetContainerLabelColor(
                mi_slot.HasInputContainer,
                mi_slot.InputContainerIndex,
                mi_behaviour?.GetResolvedInputStorageForSlot(mi_slotIndex),
                true);
        }

        public bool SyncFromSlot(CCrafterSlot slot, CrafterBehaviour behaviour)
        {
            if (slot == null)
                return false;

            mi_slot = slot;
            mi_behaviour = behaviour;

            if (mi_recipeLabel != null)
                mi_recipeLabel.text = BuildRecipeTitle(slot);

            if (mi_recipeMetaLabel != null)
                mi_recipeMetaLabel.text = BuildRecipeMeta(slot);

            if (mi_activeToggle != null)
                mi_activeToggle.SetIsOnWithoutNotify(slot.IsActive);

            if (mi_infiniteToggle != null)
                mi_infiniteToggle.SetIsOnWithoutNotify(slot.IsInfinite);

            if (mi_countRow != null)
                mi_countRow.SetActive(!slot.IsInfinite);

            if (mi_countInput != null)
                mi_countInput.text = slot.RemainingCount > 0 ? slot.RemainingCount.ToString() : "1";

            if (mi_countInputBg != null)
                CRaftStyleHelper.ApplyInput(mi_countInputBg);

            RefreshInputLabel();
            RefreshOutputLabel();
            RefreshStatus();
            return true;
        }

        // ------------------------------------------------------------------
        //  Callbacks
        // ------------------------------------------------------------------

        private void OnActiveChanged(bool value)
        {
            mi_behaviour.SetSlotActive(mi_slotIndex, value);
            RefreshStatus();
        }

        private void OnInfiniteChanged(bool value)
        {
            mi_behaviour.SetSlotInfinite(mi_slotIndex, value);
            mi_countRow.SetActive(!value);
        }

        private void OnCountChanged(string value)
        {
            if (int.TryParse(value, out int count) && count > 0)
            {
                // Valid input - restore normal colour
                if (mi_countInputBg != null)
                    CRaftStyleHelper.ApplyInput(mi_countInputBg);
                mi_behaviour.SetSlotCount(mi_slotIndex, count);
            }
            else
            {
                // Invalid input - tint red and clamp to 1
                if (mi_countInputBg != null)
                    mi_countInputBg.color = CRaftStyleHelper.ColInputErr;
                if (mi_countInput != null)
                    mi_countInput.text = "1";
                mi_behaviour.SetSlotCount(mi_slotIndex, 1);
            }
        }

        // ------------------------------------------------------------------
        //  Private helpers
        // ------------------------------------------------------------------

        private string GetContainerDisplayText(string prefix, bool hasContainer,
            int containerIndex, Storage_Small cachedStorage)
        {
            if (!hasContainer)
                return prefix + ": Own Chest";

            string ident = mi_bridge.BuildContainerIdentityLabel(containerIndex);
            AutoCrafterUIBridge.ContainerReferenceState state = mi_bridge.GetContainerReferenceState(containerIndex, cachedStorage);
            if (state == AutoCrafterUIBridge.ContainerReferenceState.Missing)
                return prefix + ": " + ident + " [missing]";

            return prefix + ": " + ident;
        }

        private Color GetContainerLabelColor(bool hasContainer, int containerIndex, Storage_Small cachedStorage, bool isInput)
        {
            if (!hasContainer)
                return CRaftStyleHelper.ColSubtext;

            AutoCrafterUIBridge.ContainerReferenceState state = mi_bridge.GetContainerReferenceState(containerIndex, cachedStorage);
            if (state == AutoCrafterUIBridge.ContainerReferenceState.Missing)
                return CRaftStyleHelper.ColBad;

            return isInput ? CRaftStyleHelper.ColGood : UIStyleTokens.AccentSoft;
        }

        private static void PlayClick()
        {
            AudioFeedbackAdapter.PlayClick();
        }

        private static string BuildRecipeTitle(CCrafterSlot slot)
        {
            if (slot == null || !slot.HasRecipe || slot.CachedItem == null)
                return "Select Recipe";

            string recipeName = slot.CachedItem.settings_Inventory != null
                ? slot.CachedItem.settings_Inventory.DisplayName
                : slot.CachedItem.UniqueName;
            int outputAmount = Mathf.Max(1, slot.CachedItem.settings_recipe != null
                ? slot.CachedItem.settings_recipe.AmountToCraft
                : 1);

            return outputAmount > 1 ? recipeName + "  x" + outputAmount : recipeName;
        }

        private static string BuildRecipeMeta(CCrafterSlot slot)
        {
            if (slot == null || !slot.HasRecipe)
                return "Pick recipe to start slot";

            if (slot.IsInfinite)
                return "Looping";

            return "Remaining: " + Mathf.Max(0, slot.RemainingCount);
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
    }
}
