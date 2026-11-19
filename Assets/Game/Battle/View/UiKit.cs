using System;
using UnityEngine;
using UnityEngine.UI;

namespace RingsideHearts.Battle.View
{
    /// <summary>Runtime-generated graybox sprites, the Korean OS font, and small uGUI builders.</summary>
    public static class UiKit
    {
        static readonly string[] KoreanFontCandidates = { "Malgun Gothic", "Noto Sans CJK KR", "Noto Sans KR", "NanumGothic" };

        public static readonly Color Background = new Color32(0x1E, 0x1F, 0x24, 0xFF);
        public static readonly Color Zone = new Color32(0x2A, 0x2C, 0x33, 0xFF);
        public static readonly Color Apron = new Color32(0x3A, 0x3D, 0x46, 0xFF);
        public static readonly Color Floor = new Color32(0x50, 0x54, 0x60, 0xFF);
        public static readonly Color Rope = new Color32(0xE6, 0xE6, 0xE6, 0xFF);
        public static readonly Color NearRope = new Color(0.94f, 0.62f, 0.15f, 0.10f);
        public static readonly Color TextMain = new Color32(0xED, 0xED, 0xED, 0xFF);
        public static readonly Color TextDim = new Color32(0xA8, 0xAA, 0xB2, 0xFF);

        // Attribute colors (battle doc 3.3): 열혈 red, 냉정 blue, 개방 green, 성실 yellow, 음침 purple.
        public static readonly Color Red = new Color32(0xE2, 0x4B, 0x4A, 0xFF);
        public static readonly Color Blue = new Color32(0x37, 0x8A, 0xDD, 0xFF);
        public static readonly Color Green = new Color32(0x63, 0x99, 0x22, 0xFF);
        public static readonly Color Yellow = new Color32(0xEF, 0x9F, 0x27, 0xFF);
        public static readonly Color Purple = new Color32(0x7F, 0x77, 0xDD, 0xFF);
        public static readonly Color Gray = new Color32(0x6E, 0x71, 0x7A, 0xFF);

        /// <summary>The five attribute colors (design 10.1): 열혈 red, 냉정 blue, 개방 green, 성실 yellow, 음침 purple.</summary>
        public static Color AttributeColor(RingsideHearts.Battle.Core.FighterAttribute a)
        {
            switch (a)
            {
                case RingsideHearts.Battle.Core.FighterAttribute.Hot: return Red;
                case RingsideHearts.Battle.Core.FighterAttribute.Cool: return Blue;
                case RingsideHearts.Battle.Core.FighterAttribute.Open: return Green;
                case RingsideHearts.Battle.Core.FighterAttribute.Steady: return Yellow;
                default: return Purple;
            }
        }

        /// <summary>Top-anchored, horizontally stretched rect: y is the distance down from the parent's top.</summary>
        public static void TopRow(RectTransform rt, float y, float height, float inset = 40f)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(-inset * 2f, height);
            rt.anchoredPosition = new Vector2(0f, -y);
        }

        static Sprite _circle;
        static Sprite _square;
        static Font _font;

        public static Sprite Circle => _circle != null ? _circle : (_circle = MakeCircle(256));
        public static Sprite Square => _square != null ? _square : (_square = MakeSquare());

        public static Font KoreanFont
        {
            get
            {
                if (_font != null) return _font;
                string[] installed = Font.GetOSInstalledFontNames();
                foreach (string candidate in KoreanFontCandidates)
                {
                    if (Array.IndexOf(installed, candidate) >= 0)
                    {
                        _font = Font.CreateDynamicFontFromOSFont(candidate, 32);
                        return _font;
                    }
                }
                // Fall back to letting the OS pick from the whole list, then the built-in font.
                _font = Font.CreateDynamicFontFromOSFont(KoreanFontCandidates, 32);
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        public static RectTransform MakeRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static Image MakeImage(Transform parent, string name, Sprite sprite, Color color)
        {
            RectTransform rt = MakeRect(parent, name);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static Text MakeText(Transform parent, string name, int size, TextAnchor anchor, Color color)
        {
            RectTransform rt = MakeRect(parent, name);
            var text = rt.gameObject.AddComponent<Text>();
            text.font = KoreanFont;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        public static Button MakeButton(Transform parent, string name, string label, Vector2 size)
        {
            Image bg = MakeImage(parent, name, Square, new Color32(0x3E, 0x41, 0x4C, 0xFF));
            bg.raycastTarget = true;
            bg.rectTransform.sizeDelta = size;
            var button = bg.gameObject.AddComponent<Button>();
            Text text = MakeText(bg.transform, "Label", 34, TextAnchor.MiddleCenter, TextMain);
            Stretch(text.rectTransform);
            text.text = label;
            return button;
        }

        /// <summary>Fills the parent, with optional insets (left, bottom, right, top).</summary>
        public static void Stretch(RectTransform rt, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>Fixed-size rect anchored at the parent's center.</summary>
        public static void Center(RectTransform rt, Vector2 size, Vector2 position)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
        }

        static Sprite MakeCircle(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - r;
                    float dy = y + 0.5f - r;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    byte a = (byte)(Mathf.Clamp01(r - d) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        static Sprite MakeSquare()
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var pixels = new Color32[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
        }
    }
}
