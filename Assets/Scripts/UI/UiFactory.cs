using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SSRP.UI
{
    /// <summary>Colour palette shared by every screen.</summary>
    public static class UiTheme
    {
        public static readonly Color Backdrop = new Color(0.03f, 0.04f, 0.09f, 0.85f);
        public static readonly Color Panel = new Color(0.08f, 0.10f, 0.18f, 0.96f);
        public static readonly Color PanelLight = new Color(0.13f, 0.16f, 0.27f, 1f);
        public static readonly Color Button = new Color(0.16f, 0.20f, 0.34f, 1f);
        public static readonly Color ButtonHighlight = new Color(0.25f, 0.55f, 0.95f, 1f);
        public static readonly Color ButtonPressed = new Color(0.18f, 0.40f, 0.75f, 1f);
        public static readonly Color Text = new Color(0.93f, 0.95f, 1f, 1f);
        public static readonly Color TextDim = new Color(0.62f, 0.67f, 0.80f, 1f);
        public static readonly Color Accent = new Color(0.35f, 0.85f, 1f, 1f);
        public static readonly Color Good = new Color(0.30f, 0.90f, 0.45f, 1f);
        public static readonly Color Warn = new Color(1f, 0.75f, 0.25f, 1f);
        public static readonly Color Bad = new Color(1f, 0.32f, 0.32f, 1f);
        public static readonly Color Off = new Color(0.35f, 0.38f, 0.45f, 1f);
    }

    /// <summary>
    /// Builds uGUI elements from code. Every screen in the game builds its layout with these
    /// helpers at Awake, so layouts live next to the logic that drives them and prefabs never
    /// lose references. Text uses the built-in legacy font (no TextMeshPro essential-resources
    /// import is needed, which keeps the project working straight after cloning).
    /// </summary>
    public static class UiFactory
    {
        public const float ReferenceWidth = 1920f;
        public const float ReferenceHeight = 1080f;

        private static Font font;

        public static Font Font
        {
            get
            {
                if (font == null)
                    font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

                return font;
            }
        }

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI") >= 0 ? LayerMask.NameToLayer("UI") : 0;
            var rt = (RectTransform)go.transform;

            if (parent != null)
                rt.SetParent(parent, false);

            return rt;
        }

        /// <summary>Screen-space overlay canvas scaled for 1920x1080 (works at 16:9 and 16:10).</summary>
        public static Canvas CreateCanvas(string name, int sortingOrder, Transform parent = null)
        {
            RectTransform rt = CreateRect(name, parent);
            return SetupCanvas(rt.gameObject, sortingOrder);
        }

        public static Canvas SetupCanvas(GameObject go, int sortingOrder)
        {
            if (!(go.transform is RectTransform))
                go.AddComponent<RectTransform>();

            Canvas canvas = go.GetComponent<Canvas>();
            if (canvas == null)
                canvas = go.AddComponent<Canvas>();

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            if (scaler == null)
                scaler = go.AddComponent<CanvasScaler>();

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (go.GetComponent<GraphicRaycaster>() == null)
                go.AddComponent<GraphicRaycaster>();

            return canvas;
        }

        public static void Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        public static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        public static Image CreateImage(Transform parent, string name, Color colour)
        {
            RectTransform rt = CreateRect(name, parent);
            Image img = rt.gameObject.AddComponent<Image>();
            img.color = colour;
            return img;
        }

        public static Image CreatePanel(Transform parent, string name, Color colour)
        {
            Image img = CreateImage(parent, name, colour);
            Stretch(img.rectTransform);
            return img;
        }

        public static Text CreateText(Transform parent, string name, string content, int size,
            TextAnchor alignment = TextAnchor.MiddleCenter, Color? colour = null, FontStyle style = FontStyle.Normal)
        {
            RectTransform rt = CreateRect(name, parent);
            Text text = rt.gameObject.AddComponent<Text>();
            text.font = Font;
            text.text = content;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = colour ?? UiTheme.Text;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        public static LayoutElement SetSize(Component c, float preferredWidth, float preferredHeight,
            float flexibleWidth = -1f)
        {
            LayoutElement le = c.GetComponent<LayoutElement>();
            if (le == null)
                le = c.gameObject.AddComponent<LayoutElement>();

            if (preferredWidth >= 0f) le.preferredWidth = preferredWidth;
            if (preferredHeight >= 0f)
            {
                le.preferredHeight = preferredHeight;
                le.minHeight = preferredHeight;
            }

            le.flexibleWidth = flexibleWidth;
            return le;
        }

        public static VerticalLayoutGroup AddVertical(Component c, float spacing, int padding,
            TextAnchor alignment = TextAnchor.UpperCenter, bool expandWidth = true)
        {
            VerticalLayoutGroup v = c.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = new RectOffset(padding, padding, padding, padding);
            v.childAlignment = alignment;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = expandWidth;
            v.childForceExpandHeight = false;
            return v;
        }

        public static HorizontalLayoutGroup AddHorizontal(Component c, float spacing, int padding,
            TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            HorizontalLayoutGroup h = c.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.padding = new RectOffset(padding, padding, padding, padding);
            h.childAlignment = alignment;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            return h;
        }

        /// <summary>A horizontal row container (for use inside a vertical layout).</summary>
        public static RectTransform CreateRow(Transform parent, string name, float height, float spacing = 16f)
        {
            RectTransform row = CreateRect(name, parent);
            AddHorizontal(row, spacing, 0, TextAnchor.MiddleLeft);
            SetSize(row, -1f, height, 1f);
            return row;
        }

        public static Button CreateButton(Transform parent, string name, string label, UnityAction onClick,
            float width = 420f, float height = 76f, int fontSize = 32)
        {
            Image bg = CreateImage(parent, name, Color.white);
            Place(bg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(width, height));

            Button button = bg.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            ColorBlock colours = button.colors;
            colours.normalColor = UiTheme.Button;
            colours.highlightedColor = UiTheme.ButtonHighlight;
            colours.selectedColor = UiTheme.ButtonHighlight;
            colours.pressedColor = UiTheme.ButtonPressed;
            colours.disabledColor = new Color(0.12f, 0.13f, 0.18f, 0.6f);
            colours.colorMultiplier = 1f;
            colours.fadeDuration = 0.08f;
            button.colors = colours;

            Text text = CreateText(bg.transform, "Label", label, fontSize);
            Stretch(text.rectTransform, 6f);

            if (onClick != null)
                button.onClick.AddListener(onClick);

            SetSize(button, width, height, 0f);
            return button;
        }

        public static void SetButtonLabel(Button button, string label)
        {
            Text text = button != null ? button.GetComponentInChildren<Text>() : null;
            if (text != null)
                text.text = label;
        }

        public static Slider CreateSlider(Transform parent, string name, float min, float max, float value,
            UnityAction<float> onChanged, float width = 420f)
        {
            GameObject go = DefaultControls.CreateSlider(new DefaultControls.Resources());
            go.name = name;
            go.transform.SetParent(parent, false);

            foreach (Image img in go.GetComponentsInChildren<Image>(true))
            {
                if (img.name == "Background") img.color = UiTheme.PanelLight;
                else if (img.name == "Fill") img.color = UiTheme.Accent;
                else if (img.name == "Handle") img.color = UiTheme.Text;
            }

            Slider slider = go.GetComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;

            if (onChanged != null)
                slider.onValueChanged.AddListener(onChanged);

            SetSize(slider, width, 36f, 0f);
            return slider;
        }

        public static Dropdown CreateDropdown(Transform parent, string name, float width = 420f)
        {
            GameObject go = DefaultControls.CreateDropdown(new DefaultControls.Resources());
            go.name = name;
            go.transform.SetParent(parent, false);

            foreach (Text t in go.GetComponentsInChildren<Text>(true))
            {
                t.font = Font;
                t.fontSize = 26;
                t.color = UiTheme.Text;
            }

            foreach (Image img in go.GetComponentsInChildren<Image>(true))
                img.color = img.name == "Item Background" ? UiTheme.PanelLight : UiTheme.Button;

            Dropdown dropdown = go.GetComponent<Dropdown>();
            SetSize(dropdown, width, 60f, 1f);
            return dropdown;
        }

        /// <summary>A bar whose fill is driven by <see cref="SetBar"/> (no sprite needed).</summary>
        public static RectTransform CreateBar(Transform parent, string name, Color fillColour, bool vertical,
            float width, float height)
        {
            Image back = CreateImage(parent, name, UiTheme.PanelLight);
            SetSize(back, width, height, 0f);
            Place(back.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(width, height));

            Image fill = CreateImage(back.transform, "Fill", fillColour);
            RectTransform rt = fill.rectTransform;
            rt.pivot = vertical ? new Vector2(0.5f, 0f) : new Vector2(0f, 0.5f);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = vertical ? new Vector2(1f, 0f) : new Vector2(0f, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static void SetBar(RectTransform fill, float value01, bool vertical)
        {
            if (fill == null)
                return;

            value01 = Mathf.Clamp01(value01);
            fill.anchorMax = vertical ? new Vector2(1f, value01) : new Vector2(value01, 1f);
            fill.offsetMax = Vector2.zero;
        }
    }
}
