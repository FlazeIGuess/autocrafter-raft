using UnityEngine;

namespace pp.RaftMods.AutoCrafter
{
    /// <summary>
    /// Root UI manager. Owns the canvas overlay and the single CUICrafterDialog.
    /// Mirrors the Auto Sorter CModUI pattern.
    /// </summary>
    public class CModUI : MonoBehaviour
    {
        private UIBootstrap mi_bootstrap;
        private AutoCrafterUIBridge mi_bridge;

        /// <summary>Screen-space toast notification. Accessible to the dialog for upgrade feedback.</summary>
        public CToastNotification Toast { get; private set; }

        internal void SetToast(CToastNotification toast)
        {
            Toast = toast;
        }

        private void Awake()
        {
            mi_bootstrap = new UIBootstrap();
            mi_bridge = new AutoCrafterUIBridge();
        }

        // --- Construction ---

        /// <summary>Builds the full UGUI hierarchy on the game canvas.</summary>
        public void BuildUI()
        {
            if (mi_bootstrap == null)
                mi_bootstrap = new UIBootstrap();
            if (mi_bridge == null)
                mi_bridge = new AutoCrafterUIBridge();

            if (!mi_bootstrap.EnsureBuilt(this))
            {
                Debug.LogWarning("[AutoCrafter] UI build deferred - game canvas not ready yet.");
                return;
            }

            Debug.Log("[AutoCrafter] UI built.");
        }

        /// <summary>Shows the crafter dialog for the given chest behaviour.</summary>
        public void ShowDialog(CrafterBehaviour behaviour)
        {
            if (behaviour == null) return;
            ShowDialog(behaviour.ObjectIndex);
        }

        public void ShowDialog(uint objectIndex)
        {
            if (mi_bootstrap == null)
                mi_bootstrap = new UIBootstrap();
            if (mi_bridge == null)
                mi_bridge = new AutoCrafterUIBridge();

            mi_bootstrap.ShowOrDefer(this, mi_bridge, objectIndex);
        }

        public void OnStorageOpened(uint objectIndex)
        {
            ShowDialog(objectIndex);
        }

        public void OnStorageClosed(uint objectIndex)
        {
            mi_bootstrap?.HideAndCancelDeferred(objectIndex);
        }

        /// <summary>Hides the crafter dialog without closing the chest inventory.</summary>
        public void HideDialog()
        {
            mi_bootstrap?.Dialog?.Hide();
        }

        /// <summary>
        /// Refreshes the slot status labels for the chest with the given ObjectIndex.
        /// Called by the craft loop after each craft attempt.
        /// </summary>
        public void RefreshSlotStatus(uint objectIndex)
        {
            mi_bootstrap?.Dialog?.RefreshStatus(objectIndex);
        }

        /// <summary>
        /// Called when a Storage_Small is destroyed.
        /// Hides the dialog if it is currently showing that chest.
        /// </summary>
        public void OnStorageDestroyed(uint objectIndex)
        {
            CUICrafterDialog dialog = mi_bootstrap?.Dialog;
            if (dialog != null && dialog.CurrentObjectIndex == objectIndex)
                dialog.Hide();
            mi_bootstrap?.ClearDeferred(objectIndex);
        }

        /// <summary>Destroys the entire UI hierarchy.</summary>
        public void DestroyUI()
        {
            mi_bootstrap?.DestroyUI();
        }

        private void Update()
        {
            mi_bootstrap?.FlushDeferred(this, mi_bridge);
        }

        private void OnDestroy()
        {
            DestroyUI();
        }
    }
}
