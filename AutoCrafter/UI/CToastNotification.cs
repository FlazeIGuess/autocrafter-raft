using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace pp.RaftMods.AutoCrafter
{
    /// <summary>
    /// Lightweight screen-space toast notification.
    /// Attach once to the AutoCrafter UI host via CModUI.
    /// Call Show() to display a short message at the top-center of the screen.
    /// </summary>
    public class CToastNotification : MonoBehaviour
    {
        private const float TOAST_WIDTH   = 500f;
        private const float TOAST_HEIGHT  = 52f;
        private const float FADE_IN_TIME  = 0.18f;
        private const float FADE_OUT_TIME = 0.45f;

        private GameObject  mi_canvasGO;
        private CanvasGroup mi_canvasGroup;
        private Text        mi_messageText;
        private Image       mi_background;
        private Coroutine   mi_fadeRoutine;

        private void Awake()
        {
            BuildToastCanvas();
            mi_canvasGO.SetActive(false);
        }

        // ------------------------------------------------------------------

        /// <summary>Shows a toast for <paramref name="duration"/> seconds then fades out.</summary>
        public void Show(string message, Color bgColor, float duration = 3f)
        {
            if (mi_fadeRoutine != null)
                StopCoroutine(mi_fadeRoutine);
            mi_messageText.text   = message;
            mi_background.color   = bgColor;
            mi_canvasGO.SetActive(true);
            mi_fadeRoutine = StartCoroutine(FadeSequence(duration));
        }

        // ------------------------------------------------------------------

        private IEnumerator FadeSequence(float duration)
        {
            // Fade in
            yield return StartCoroutine(Fade(0f, 1f, FADE_IN_TIME));

            // Hold
            yield return new WaitForSeconds(duration);

            // Fade out
            yield return StartCoroutine(Fade(1f, 0f, FADE_OUT_TIME));

            mi_canvasGO.SetActive(false);
        }

        private IEnumerator Fade(float from, float to, float time)
        {
            float elapsed = 0f;
            mi_canvasGroup.alpha = from;
            while (elapsed < time)
            {
                elapsed += Time.unscaledDeltaTime;
                mi_canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / time);
                yield return null;
            }
            mi_canvasGroup.alpha = to;
        }

        // ------------------------------------------------------------------

        private void BuildToastCanvas()
        {
            // Dedicated overlay canvas - always on top
            mi_canvasGO = new GameObject("AC_ToastCanvas");
            DontDestroyOnLoad(mi_canvasGO);

            Canvas canvas = mi_canvasGO.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;

            CanvasScaler scaler = mi_canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight  = 0.5f;

            mi_canvasGO.AddComponent<GraphicRaycaster>();
            mi_canvasGroup = mi_canvasGO.AddComponent<CanvasGroup>();
            mi_canvasGroup.interactable   = false;
            mi_canvasGroup.blocksRaycasts = false;

            // Background panel
            GameObject panelGO = new GameObject("AC_ToastPanel");
            panelGO.transform.SetParent(mi_canvasGO.transform, false);
            RectTransform panelRT = panelGO.AddComponent<RectTransform>();
            // Anchor to top-center
            panelRT.anchorMin        = new Vector2(0.5f, 1f);
            panelRT.anchorMax        = new Vector2(0.5f, 1f);
            panelRT.pivot            = new Vector2(0.5f, 1f);
            panelRT.anchoredPosition = new Vector2(0f, -28f);
            panelRT.sizeDelta        = new Vector2(TOAST_WIDTH, TOAST_HEIGHT);

            mi_background       = panelGO.AddComponent<Image>();
            mi_background.color = CRaftStyleHelper.ColBtnRed;
            CRaftStyleHelper.ApplyPanel(mi_background, CRaftStyleHelper.ColBtnRed);

            // Message text
            GameObject textGO = new GameObject("AC_ToastText");
            textGO.transform.SetParent(panelGO.transform, false);
            RectTransform textRT = textGO.AddComponent<RectTransform>();
            textRT.anchorMin  = Vector2.zero;
            textRT.anchorMax  = Vector2.one;
            textRT.offsetMin  = new Vector2(12f, 4f);
            textRT.offsetMax  = new Vector2(-12f, -4f);

            mi_messageText = textGO.AddComponent<Text>();
            CRaftStyleHelper.Apply(mi_messageText, 14, TextAnchor.MiddleCenter);
            mi_messageText.color      = Color.white;
            mi_messageText.fontStyle  = FontStyle.Bold;
            mi_messageText.resizeTextForBestFit = false;
        }
    }
}
