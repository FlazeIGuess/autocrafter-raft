using UnityEngine;
using UnityEngine.UI;

namespace pp.RaftMods.AutoCrafter
{
    /// <summary>
    /// Grabs Raft's own runtime UI assets (font, sprites) and exposes them for
    /// the AutoCrafter UI so every element blends with the native game aesthetic.
    /// Call Initialize() once after the game scene is fully loaded.
    /// </summary>
    internal static class CRaftStyleHelper
    {
        // Raft colour palette (warm beige body text, cyan accent, dark panel)
        public static readonly Color ColText       = UIStyleTokens.Text;
        public static readonly Color ColAccent     = UIStyleTokens.Accent;
        public static readonly Color ColHeader     = UIStyleTokens.Header;
        public static readonly Color ColSubtext    = UIStyleTokens.Subtext;
        public static readonly Color ColPanel      = UIStyleTokens.Panel;
        public static readonly Color ColPanelDark  = UIStyleTokens.PanelDark;
        public static readonly Color ColBtn        = UIStyleTokens.Button;
        public static readonly Color ColBtnHover   = UIStyleTokens.ButtonHover;
        public static readonly Color ColBtnGreen   = UIStyleTokens.ButtonGreen;
        public static readonly Color ColBtnRed     = UIStyleTokens.ButtonRed;
        public static readonly Color ColInput      = UIStyleTokens.Input;
        public static readonly Color ColInputErr   = UIStyleTokens.InputError;
        public static readonly Color ColGood       = UIStyleTokens.Good;
        public static readonly Color ColWarn       = UIStyleTokens.Warn;
        public static readonly Color ColBad        = UIStyleTokens.Bad;

        public static Font   GameFont     { get; private set; }
        public static Sprite PanelSprite  { get; private set; } // 9-sliced background
        public static Sprite ButtonSprite { get; private set; } // 9-sliced button
        public static Sprite InputSprite  { get; private set; } // 9-sliced input field
        public static bool   IsReady      { get; private set; }

        /// <summary>
        /// Call once when the game scene is ready.  Safe to call multiple times.
        /// </summary>
        public static void Initialize()
        {
            if (IsReady) return;

            // --- Font ---
            GameFont = TryGrabFont();

            // --- Sprites: search live UI images for known Raft sprite names ---
            PanelSprite  = TryGrabSprite("raft_bg",   "background", "bg", "panel", "frame");
            ButtonSprite = TryGrabSprite("btn",       "button", "btn_normal", "btnNormal");
            InputSprite  = TryGrabSprite("input",     "inputfield", "inputNormal", "field");

            // Fallbacks: plain white pixel so 9-slice is skipped gracefully
            if (PanelSprite  == null) PanelSprite  = CreatePixelSprite();
            if (ButtonSprite == null) ButtonSprite = CreatePixelSprite();
            if (InputSprite  == null) InputSprite  = CreatePixelSprite();

            IsReady = true;
            Debug.Log("[AutoCrafter] CRaftStyleHelper initialized. Font=" + (GameFont != null ? GameFont.name : "Arial fallback"));
        }

        // ------------------------------------------------------------------
        //  Apply helpers
        // ------------------------------------------------------------------

        /// <summary>Applies game font + standard body text colour.</summary>
        public static void Apply(Text t, int fontSize = 12, TextAnchor align = TextAnchor.MiddleLeft)
        {
            if (t == null) return;
            t.font      = GameFont ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.color     = ColText;
            t.fontSize  = fontSize;
            t.alignment = align;
        }

        /// <summary>Applies a Raft-style panel appearance (9-sliced, dark).</summary>
        public static void ApplyPanel(Image img, Color? overrideColor = null)
        {
            if (img == null) return;
            img.sprite = PanelSprite;
            img.type   = PanelSprite != null ? Image.Type.Sliced : Image.Type.Simple;
            img.color  = overrideColor ?? ColPanel;
        }

        /// <summary>Applies a Raft-style button appearance (9-sliced).</summary>
        public static void ApplyButton(Image img, Color? overrideColor = null)
        {
            if (img == null) return;
            img.sprite = ButtonSprite;
            img.type   = ButtonSprite != null ? Image.Type.Sliced : Image.Type.Simple;
            img.color  = overrideColor ?? ColBtn;
        }

        /// <summary>Applies a Raft-style input field appearance (9-sliced).</summary>
        public static void ApplyInput(Image img)
        {
            if (img == null) return;
            img.sprite = InputSprite;
            img.type   = InputSprite != null ? Image.Type.Sliced : Image.Type.Simple;
            img.color  = ColInput;
        }

        // ------------------------------------------------------------------
        //  Private helpers
        // ------------------------------------------------------------------

        private static Font TryGrabFont()
        {
            // First pass: prefer any live Text that is NOT Arial
            Text[] allTexts = Resources.FindObjectsOfTypeAll<Text>();
            foreach (Text t in allTexts)
            {
                if (t == null || t.font == null) continue;
                string n = t.font.name.ToLower();
                if (!n.Contains("arial") && !n.Contains("liberation"))
                    return t.font;
            }

            // Second pass: find by known Raft font name hints
            Font[] allFonts = Resources.FindObjectsOfTypeAll<Font>();
            foreach (Font f in allFonts)
            {
                if (f == null) continue;
                string n = f.name.ToLower();
                if (n.Contains("rock") || n.Contains("raft") || n.Contains("cabin")
                    || n.Contains("chinese") || n.Contains("noto"))
                    return f;
            }

            return Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private static Sprite TryGrabSprite(params string[] nameHints)
        {
            Sprite[] all = Resources.FindObjectsOfTypeAll<Sprite>();
            foreach (Sprite s in all)
            {
                if (s == null) continue;
                string n = s.name.ToLower();
                foreach (string hint in nameHints)
                {
                    if (n.Contains(hint.ToLower()))
                    {
                        // Only accept sprites that have a border set (9-slice) or a reasonable size
                        if (s.border != Vector4.zero || (s.rect.width >= 8 && s.rect.height >= 8))
                            return s;
                    }
                }
            }
            return null;
        }

        private static Sprite CreatePixelSprite()
        {
            Texture2D tex = new Texture2D(4, 4, TextureFormat.ARGB32, false);
            Color[] cols = new Color[16];
            for (int i = 0; i < 16; i++) cols[i] = Color.white;
            tex.SetPixels(cols);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
        }
    }
}
