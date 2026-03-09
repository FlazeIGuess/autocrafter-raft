using UnityEngine;
using UnityEngine.EventSystems;

namespace pp.RaftMods.AutoCrafter
{
    /// <summary>
    /// Attach to any UI element (e.g. a header bar) to make a target panel draggable.
    /// Moves the target RectTransform's anchoredPosition by the drag delta each frame.
    /// </summary>
    public class CDraggablePanel : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        /// <summary>The RectTransform that will be moved when this element is dragged.</summary>
        public RectTransform Target;

        /// <summary>Padding to keep between panel and canvas edge.</summary>
        public float SafeMargin = 8f;

        /// <summary>Minimum visible header height when the panel is partially off-screen.</summary>
        public float MinVisibleHeader = 24f;

        /// <summary>Current header height used for visibility clamp.</summary>
        public float HeaderHeight = 44f;

        /// <summary>Allows temporary drag lock while modal overlays are active.</summary>
        public bool IsDragEnabled = true;

        private Canvas mi_canvas;
        private RectTransform mi_canvasRect;
        private Vector2 mi_dragStartPanelPos;
        private Vector2 mi_dragStartPointerPos;

        private void Awake()
        {
            // Walk up the hierarchy to find the parent Canvas for scale correction.
            mi_canvas = GetComponentInParent<Canvas>();
            while (mi_canvas != null && !mi_canvas.isRootCanvas)
                mi_canvas = mi_canvas.transform.parent?.GetComponentInParent<Canvas>();

            if (mi_canvas != null)
                mi_canvasRect = mi_canvas.GetComponent<RectTransform>();
        }

        public void OnBeginDrag(PointerEventData data)
        {
            if (!IsDragEnabled) return;
            if (Target == null || mi_canvasRect == null) return;
            mi_dragStartPanelPos = Target.anchoredPosition;
            // Store the pointer start in canvas-local space
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                mi_canvasRect, data.position,
                data.pressEventCamera, out mi_dragStartPointerPos);
        }

        public void OnDrag(PointerEventData data)
        {
            if (!IsDragEnabled) return;
            if (Target == null || mi_canvasRect == null) return;

            Vector2 currentPointerPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                mi_canvasRect, data.position,
                data.pressEventCamera, out currentPointerPos);

            Target.anchoredPosition = mi_dragStartPanelPos + (currentPointerPos - mi_dragStartPointerPos);
            ClampToCanvasBounds();
        }

        /// <summary>
        /// Clamps the target panel to the root canvas.
        /// Keeps the full width visible and always keeps at least part of the header reachable.
        /// </summary>
        public void ClampToCanvasBounds()
        {
            if (Target == null || mi_canvasRect == null)
                return;

            Vector2 canvasHalf = mi_canvasRect.rect.size * 0.5f;
            Vector2 size = Target.rect.size;

            float minX = -canvasHalf.x + (size.x * Target.pivot.x) + SafeMargin;
            float maxX = canvasHalf.x - (size.x * (1f - Target.pivot.x)) - SafeMargin;

            // Keep at least header part visible at top/bottom so it is always draggable back.
            float visibleHeader = Mathf.Clamp(MinVisibleHeader, 8f, Mathf.Max(8f, HeaderHeight));
            float minVisible = Mathf.Min(visibleHeader, size.y);

            float minY = -canvasHalf.y + minVisible + SafeMargin;
            float maxY = canvasHalf.y - minVisible - SafeMargin;

            if (minX > maxX)
            {
                float centerX = (minX + maxX) * 0.5f;
                minX = centerX;
                maxX = centerX;
            }

            if (minY > maxY)
            {
                float centerY = (minY + maxY) * 0.5f;
                minY = centerY;
                maxY = centerY;
            }

            Vector2 anchored = Target.anchoredPosition;
            anchored.x = Mathf.Clamp(anchored.x, minX, maxX);
            anchored.y = Mathf.Clamp(anchored.y, minY, maxY);
            Target.anchoredPosition = anchored;
        }

        private void OnRectTransformDimensionsChange()
        {
            // Re-clamp when resolution or canvas dimensions change.
            ClampToCanvasBounds();
        }
    }
}
