using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace pp.RaftMods.AutoCrafter
{
    /// <summary>
    /// Handles slot panel lifecycle and refresh orchestration for the crafter dialog.
    /// </summary>
    public sealed class SlotListController
    {
        private readonly List<CUICrafterSlotPanel> mi_slotPanels = new List<CUICrafterSlotPanel>();
        private GameObject mi_upgradeHint;

        public int Count => mi_slotPanels.Count;

        public void RefreshStatus()
        {
            for (int i = 0; i < mi_slotPanels.Count; i++)
                mi_slotPanels[i].RefreshStatus();
        }

        public void RefreshInputLabel(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= mi_slotPanels.Count)
                return;

            mi_slotPanels[slotIndex].RefreshInputLabel();
        }

        public void RefreshOutputLabel(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= mi_slotPanels.Count)
                return;

            mi_slotPanels[slotIndex].RefreshOutputLabel();
        }

        public bool Sync(
            CCrafterData data,
            Transform slotsContent,
            CrafterBehaviour behaviour,
            float hintWidth,
            Action<int> onRecipePicker,
            Action<int> onOutputPicker,
            Action<int> onInputPicker)
        {
            if (!data.IsUpgraded)
            {
                ClearPanels();
                EnsureUpgradeHint(slotsContent, hintWidth);
                return false;
            }

            if (mi_upgradeHint != null)
            {
                UnityEngine.Object.Destroy(mi_upgradeHint);
                mi_upgradeHint = null;
            }

            if (!CModConfig.UseIncrementalSlotRefresh)
            {
                FullRebuild(data, slotsContent, behaviour, onRecipePicker, onOutputPicker, onInputPicker);
                return true;
            }

            if (mi_slotPanels.Count != data.Slots.Count)
            {
                FullRebuild(data, slotsContent, behaviour, onRecipePicker, onOutputPicker, onInputPicker);
                return true;
            }

            for (int i = 0; i < mi_slotPanels.Count; i++)
            {
                CUICrafterSlotPanel panel = mi_slotPanels[i];
                CCrafterSlot slot = data.Slots[i];
                if (panel == null || panel.gameObject == null || slot == null || !panel.SyncFromSlot(slot, behaviour))
                {
                    FullRebuild(data, slotsContent, behaviour, onRecipePicker, onOutputPicker, onInputPicker);
                    return true;
                }
            }

            return false;
        }

        private void FullRebuild(
            CCrafterData data,
            Transform slotsContent,
            CrafterBehaviour behaviour,
            Action<int> onRecipePicker,
            Action<int> onOutputPicker,
            Action<int> onInputPicker)
        {
            ClearPanels();

            for (int i = 0; i < data.Slots.Count; i++)
            {
                int slotIndex = i;
                GameObject go = new GameObject("AC_SlotPanel_" + i);
                go.transform.SetParent(slotsContent, false);
                go.AddComponent<RectTransform>().sizeDelta = new Vector2(0f, 156f);

                CUICrafterSlotPanel panel = go.AddComponent<CUICrafterSlotPanel>();
                panel.Build(
                    go.transform,
                    data.Slots[i],
                    slotIndex,
                    () => onRecipePicker(slotIndex),
                    () => onOutputPicker(slotIndex),
                    () => onInputPicker(slotIndex),
                    behaviour);
                mi_slotPanels.Add(panel);
            }
        }

        private void EnsureUpgradeHint(Transform slotsContent, float hintWidth)
        {
            if (mi_upgradeHint != null)
                return;

            Text hint = UIFactory.CreateLabel(slotsContent, "AC_UpgradeHint",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(hintWidth, 60f),
                "Upgrade this chest to unlock\nautomatic crafting slots.", 11, TextAnchor.MiddleCenter);
            CRaftStyleHelper.Apply(hint, 11, TextAnchor.MiddleCenter);
            hint.color = CRaftStyleHelper.ColSubtext;
            mi_upgradeHint = hint.gameObject;
        }

        private void ClearPanels()
        {
            for (int i = 0; i < mi_slotPanels.Count; i++)
            {
                CUICrafterSlotPanel panel = mi_slotPanels[i];
                if (panel != null && panel.gameObject != null)
                    UnityEngine.Object.Destroy(panel.gameObject);
            }

            mi_slotPanels.Clear();
        }
    }
}
