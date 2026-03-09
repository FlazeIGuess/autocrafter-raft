using UnityEngine;

namespace pp.RaftMods.AutoCrafter
{
    /// <summary>
    /// Owns UI root/dialog creation and deferred show requests while scenes are loading.
    /// </summary>
    public sealed class UIBootstrap
    {
        private GameObject mi_root;
        private CUICrafterDialog mi_dialog;
        private uint? mi_deferredObjectIndex;

        public CUICrafterDialog Dialog => mi_dialog;

        public bool EnsureBuilt(CModUI owner)
        {
            if (mi_root != null && mi_dialog != null)
                return true;

            if (owner.Toast == null)
                owner.SetToast(owner.gameObject.AddComponent<CToastNotification>());

            GameObject canvasGO = GameObject.Find("Canvases/_CanvasGame_New");
            if (canvasGO == null)
                return false;

            CRaftStyleHelper.Initialize();

            if (mi_root == null)
            {
                Transform existing = canvasGO.transform.Find("AutoCrafter_UI");
                if (existing != null)
                {
                    mi_root = existing.gameObject;
                }
                else
                {
                    mi_root = new GameObject("AutoCrafter_UI");
                    mi_root.transform.SetParent(canvasGO.transform, false);

                    RectTransform rootRect = mi_root.AddComponent<RectTransform>();
                    rootRect.anchorMin = Vector2.zero;
                    rootRect.anchorMax = Vector2.one;
                    rootRect.offsetMin = Vector2.zero;
                    rootRect.offsetMax = Vector2.zero;
                }
            }

            if (mi_dialog == null)
            {
                mi_dialog = mi_root.GetComponent<CUICrafterDialog>();
                if (mi_dialog == null)
                {
                    mi_dialog = mi_root.AddComponent<CUICrafterDialog>();
                    mi_dialog.Build(mi_root.transform);
                }
                mi_dialog.Hide();
            }

            return true;
        }

        public bool ShowOrDefer(CModUI owner, AutoCrafterUIBridge bridge, uint objectIndex)
        {
            if (!EnsureBuilt(owner))
            {
                mi_deferredObjectIndex = objectIndex;
                return false;
            }

            if (!bridge.TryGetBehaviour(objectIndex, out CrafterBehaviour behaviour))
            {
                mi_deferredObjectIndex = objectIndex;
                return false;
            }

            mi_dialog.Show(behaviour);
            mi_deferredObjectIndex = null;
            return true;
        }

        public void FlushDeferred(CModUI owner, AutoCrafterUIBridge bridge)
        {
            if (!mi_deferredObjectIndex.HasValue)
                return;

            ShowOrDefer(owner, bridge, mi_deferredObjectIndex.Value);
        }

        public void ClearDeferred(uint objectIndex)
        {
            if (mi_deferredObjectIndex.HasValue && mi_deferredObjectIndex.Value == objectIndex)
                mi_deferredObjectIndex = null;
        }

        public void HideAndCancelDeferred(uint objectIndex)
        {
            ClearDeferred(objectIndex);
            mi_dialog?.Hide();
        }

        public void DestroyUI()
        {
            if (mi_root != null)
                Object.Destroy(mi_root);
            mi_root = null;
            mi_dialog = null;
            mi_deferredObjectIndex = null;
        }
    }
}
