using UnityEngine;
using UnityEngine.UI;

namespace pp.RaftMods.AutoCrafter
{
    /// <summary>
    /// Shared UGUI factory helpers used by all AutoCrafter UI classes.
    /// Centralises widget creation so every UI element shares the same
    /// layout and styling code path.
    /// </summary>
    internal static class UIFactory
    {
        // ------------------------------------------------------------------
        //  Panel
        // ------------------------------------------------------------------

        public static GameObject CreatePanel(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin        = anchorMin;
            rt.anchorMax        = anchorMax;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta        = size;
            go.AddComponent<Image>().color = CRaftStyleHelper.ColPanel;
            return go;
        }

        // ------------------------------------------------------------------
        //  Label
        // ------------------------------------------------------------------

        public static Text CreateLabel(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size,
            string text, int fontSize, TextAnchor alignment)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin        = anchorMin;
            rt.anchorMax        = anchorMax;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta        = size;
            Text lbl = go.AddComponent<Text>();
            CRaftStyleHelper.Apply(lbl, fontSize, alignment);
            lbl.text = text;
            return lbl;
        }

        // ------------------------------------------------------------------
        //  Button (returns the parent GameObject; Button component is on it)
        // ------------------------------------------------------------------

        public static GameObject CreateButtonGO(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size,
            string label, UnityEngine.Events.UnityAction callback)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin        = anchorMin;
            rt.anchorMax        = anchorMax;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta        = size;
            Image img  = go.AddComponent<Image>();
            CRaftStyleHelper.ApplyButton(img);
            Button btn = go.AddComponent<Button>();
            btn.image  = img;
            if (callback != null) btn.onClick.AddListener(callback);

            GameObject lblGO = new GameObject("Label");
            lblGO.transform.SetParent(go.transform, false);
            RectTransform lr = lblGO.AddComponent<RectTransform>();
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = Vector2.one;
            lr.offsetMin = new Vector2(4f, 0f);
            lr.offsetMax = new Vector2(-4f, 0f);
            Text txt = lblGO.AddComponent<Text>();
            CRaftStyleHelper.Apply(txt, 12, TextAnchor.MiddleCenter);
            txt.text = label;

            return go;
        }

        // ------------------------------------------------------------------
        //  Toggle
        // ------------------------------------------------------------------

        public static Toggle CreateToggle(Transform parent, string name,
            Vector2 anchoredPos, Vector2 size, bool initialValue,
            UnityEngine.Events.UnityAction<bool> callback)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0f, 1f);
            rt.anchorMax        = new Vector2(0f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta        = size;
            Image bg = go.AddComponent<Image>();
            CRaftStyleHelper.ApplyButton(bg, new Color(0.14f, 0.18f, 0.24f));
            Toggle toggle = go.AddComponent<Toggle>();
            toggle.targetGraphic = bg;
            toggle.isOn          = initialValue;

            // Checkmark
            GameObject ckGO = new GameObject("Checkmark");
            ckGO.transform.SetParent(go.transform, false);
            RectTransform ckRT = ckGO.AddComponent<RectTransform>();
            ckRT.anchorMin = new Vector2(0.12f, 0.12f);
            ckRT.anchorMax = new Vector2(0.88f, 0.88f);
            ckRT.offsetMin = Vector2.zero;
            ckRT.offsetMax = Vector2.zero;
            Image ckImg = ckGO.AddComponent<Image>();
            ckImg.color  = CRaftStyleHelper.ColGood;
            toggle.graphic = ckImg;

            if (callback != null) toggle.onValueChanged.AddListener(callback);
            return toggle;
        }

        // ------------------------------------------------------------------
        //  Input field
        // ------------------------------------------------------------------

        public static InputField CreateInputField(Transform parent, string name, string placeholder)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            Image bg = go.AddComponent<Image>();
            CRaftStyleHelper.ApplyInput(bg);
            InputField field = go.AddComponent<InputField>();
            field.image = bg;

            // Value text
            GameObject textGO = new GameObject("Text");
            textGO.transform.SetParent(go.transform, false);
            RectTransform tr = textGO.AddComponent<RectTransform>();
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(6f, 2f); tr.offsetMax = new Vector2(-6f, -2f);
            Text txt = textGO.AddComponent<Text>();
            CRaftStyleHelper.Apply(txt, 12, TextAnchor.MiddleLeft);
            field.textComponent = txt;

            // Placeholder
            GameObject phGO = new GameObject("Placeholder");
            phGO.transform.SetParent(go.transform, false);
            RectTransform pr = phGO.AddComponent<RectTransform>();
            pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one;
            pr.offsetMin = new Vector2(6f, 2f); pr.offsetMax = new Vector2(-6f, -2f);
            Text ph = phGO.AddComponent<Text>();
            CRaftStyleHelper.Apply(ph, 12, TextAnchor.MiddleLeft);
            ph.color     = CRaftStyleHelper.ColSubtext;
            ph.fontStyle = FontStyle.Italic;
            ph.text      = placeholder;
            field.placeholder = ph;

            return field;
        }

        // ------------------------------------------------------------------
        //  Layout helpers
        // ------------------------------------------------------------------

        /// <summary>Sets a stretch rect (anchor 0,0 to 1,1) with offsets.</summary>
        public static void SetStretchRect(GameObject go,
            float left, float right, float top, float bottom)
        {
            RectTransform rt = go.GetComponent<RectTransform>();
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left,   bottom);
            rt.offsetMax = new Vector2(right,  top);
        }

        // ------------------------------------------------------------------
        //  Input focus guard (prevents Raft key bindings while typing)
        // ------------------------------------------------------------------

        public static void AddInputFocusGuard(InputField field)
        {
            InputMenuLockService.Shared.Bind(field);
        }
    }
}
