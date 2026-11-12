using UnityEngine;
using UnityEngine.UI;

namespace CyberDefender.UI
{
    /// <summary>Общие хелперы для UI, который строится из кода (HUD, меню).</summary>
    public static class UiFactory
    {
        public static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.6f);
        public static readonly Color Cyan = new Color(0.2f, 0.9f, 1f);
        public static readonly Color Danger = new Color(1f, 0.25f, 0.3f);
        public static readonly Color EnergyColor = new Color(0.3f, 1f, 0.6f);
        public static readonly Color Muted = new Color(0.7f, 0.75f, 0.8f);

        private static Font font;
        public static Font DefaultFont => font != null ? font : (font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        public static RectTransform CreateCanvas(Transform parent, string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            return (RectTransform)go.transform;
        }

        public static RectTransform CreateRect(string name, RectTransform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        public static RectTransform CreateStretched(string name, RectTransform parent, float inset = 0f)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            return rect;
        }

        public static Image AddImage(RectTransform rect, Color color, bool raycastTarget = false)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            return image;
        }

        public static Text CreateText(RectTransform parent, string content, int fontSize, TextAnchor alignment,
            Vector2 anchor, Vector2 position, Vector2 size, Color color)
        {
            RectTransform rect = CreateRect("Text", parent, anchor, position, size);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = DefaultFont;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.text = content;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            var outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(2f, -2f);
            return text;
        }

        public static RectTransform CreateBar(RectTransform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color, out Image fillImage)
        {
            RectTransform background = CreateRect("Bar", parent, anchor, position, size);
            AddImage(background, PanelColor);

            RectTransform fill = CreateStretched("Fill", background, 3f);
            fillImage = AddImage(fill, color);
            return fill;
        }

        public static void SetFill(RectTransform fill, float value, bool fromRight)
        {
            value = Mathf.Clamp01(value);
            if (fromRight)
            {
                fill.anchorMin = new Vector2(1f - value, 0f);
                fill.anchorMax = Vector2.one;
            }
            else
            {
                fill.anchorMin = Vector2.zero;
                fill.anchorMax = new Vector2(value, 1f);
            }
        }
    }
}
