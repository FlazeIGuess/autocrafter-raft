using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace pp.RaftMods.AutoCrafter
{
    /// <summary>
    /// Centralized guard for text input focus so Raft gameplay input is blocked while typing
    /// and restored safely when focus is released or UI is forcibly closed.
    /// </summary>
    public sealed class InputMenuLockService
    {
        private MenuType mi_previousMenu = MenuType.None;
        private int mi_lockCount;

        public static InputMenuLockService Shared { get; } = new InputMenuLockService();

        private InputMenuLockService()
        {
        }

        public void Bind(InputField field)
        {
            if (field == null)
                return;

            InputMenuFocusGuard guard = field.GetComponent<InputMenuFocusGuard>();
            if (guard == null)
                guard = field.gameObject.AddComponent<InputMenuFocusGuard>();
            guard.Initialize(this);
        }

        public void Lock()
        {
            if (mi_lockCount == 0)
                mi_previousMenu = CanvasHelper.ActiveMenu;

            mi_lockCount++;
            CanvasHelper.ActiveMenu = MenuType.PauseMenu;
        }

        public void Unlock()
        {
            if (mi_lockCount <= 0)
                return;

            mi_lockCount--;
            if (mi_lockCount == 0)
                CanvasHelper.ActiveMenu = mi_previousMenu;
        }

        public void RestoreAll()
        {
            if (mi_lockCount <= 0)
                return;

            mi_lockCount = 0;
            CanvasHelper.ActiveMenu = mi_previousMenu;
        }

        private sealed class InputMenuFocusGuard : MonoBehaviour, ISelectHandler, IDeselectHandler
        {
            private InputMenuLockService mi_service;
            private bool mi_isLocked;

            public void Initialize(InputMenuLockService service)
            {
                mi_service = service;
            }

            public void OnSelect(BaseEventData eventData)
            {
                if (mi_service == null || mi_isLocked)
                    return;

                mi_service.Lock();
                mi_isLocked = true;
            }

            public void OnDeselect(BaseEventData eventData)
            {
                ReleaseLock();
            }

            private void OnDisable()
            {
                ReleaseLock();
            }

            private void OnDestroy()
            {
                ReleaseLock();
            }

            private void ReleaseLock()
            {
                if (!mi_isLocked || mi_service == null)
                    return;

                mi_service.Unlock();
                mi_isLocked = false;
            }
        }
    }
}
