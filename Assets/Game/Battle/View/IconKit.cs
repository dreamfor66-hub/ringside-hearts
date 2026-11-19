using System;
using System.Collections.Generic;
using RingsideHearts.Battle.Core;
using UnityEngine;
using UnityEngine.UI;

namespace RingsideHearts.Battle.View
{
    /// <summary>
    /// Procedural icons so information reads at a glance without art: a shape per fighter type,
    /// pips per chain role, and small glyph icons (special, second skill, assist).
    /// Striker = star (burst), Showman = diamond (sparkle), Grappler = claw, Stampeder = arrowhead,
    /// Highflier = wing (chevron up).
    /// </summary>
    public static class IconKit
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static readonly string[] TypeShort = { "스트", "쇼", "그래플", "스탬피", "하이플" };

        public static Sprite TypeIcon(FighterType t)
        {
            switch (t)
            {
                case FighterType.Striker: return Shape("star", (x, y) => Star(x, y, 5, 0.95f, 0.42f));
                case FighterType.Showman: return Shape("diamond", (x, y) => Math.Abs(x) + Math.Abs(y) <= 0.92f);
                case FighterType.Grappler: return Shape("claw", Claw);
                case FighterType.Stampeder: return Shape("arrow", Arrow);
                default: return Shape("wing", Wing);
            }
        }

        public static Sprite Bolt => Shape("bolt", (x, y) =>
            (y > -0.05f && y < 0.95f && x > -0.55f + (0.95f - y) * 0.2f - 0.15f && x < 0.15f + (0.95f - y) * 0.2f) ||
            (y < 0.15f && y > -0.95f && x > -0.15f - (y + 0.95f) * 0.2f && x < 0.55f - (y + 0.95f) * 0.2f + 0.15f));
        public static Sprite Heart => Shape("heart", (x, y) =>
        {
            float a = x * x + (y - 0.25f) * (y - 0.25f) * 1.1f;
            float yy = -y + 0.25f;
            return Math.Pow(x * x + yy * yy - 0.5, 3) - x * x * yy * yy * yy < 0;
        });
        public static Sprite Ring => Shape("ring", (x, y) => { float r = (float)Math.Sqrt(x * x + y * y); return r < 0.95f && r > 0.7f; });
        public static Sprite Card => Shape("card", (x, y) => Math.Abs(x) < 0.6f && Math.Abs(y) < 0.9f);
        public static Sprite Hand => Shape("hand", (x, y) =>
            (Math.Abs(x) < 0.55f && y < 0.2f && y > -0.85f) || (y >= 0.2f && y < 0.9f && Math.Abs(x) < 0.55f && ((int)((x + 0.55f) / 0.275f) % 2 == 0)));

        static bool Star(float x, float y, int points, float outer, float inner)
        {
            double ang = Math.Atan2(y, x) + Math.PI / 2;
            double r = Math.Sqrt(x * x + y * y);
            double seg = Math.PI * 2 / points;
            double a = ((ang % seg) + seg) % seg;
            double t = Math.Abs(a - seg / 2) / (seg / 2);
            double edge = inner + (outer - inner) * t;
            return r <= edge;
        }

        static bool Claw(float x, float y)
        {
            // three curved talons over a palm
            if (y < -0.1f && y > -0.9f && Math.Abs(x) < 0.6f) return true;
            for (int i = -1; i <= 1; i++)
            {
                float cx = i * 0.38f;
                float dx = x - cx - (y - 0.1f) * 0.25f * i;
                if (y >= -0.15f && y < 0.9f - Math.Abs(i) * 0.15f && Math.Abs(dx) < 0.13f) return true;
            }
            return false;
        }

        static bool Arrow(float x, float y)
        {
            // arrowhead pointing right with a short shaft
            if (x > -0.1f && Math.Abs(y) < 0.9f - (x + 0.1f) * 0.9f) return true;
            return x <= -0.1f && x > -0.9f && Math.Abs(y) < 0.22f;
        }

        static bool Wing(float x, float y)
        {
            // chevron up (rising)
            float d = Math.Abs(x) * 0.9f;
            return y > -0.7f + d && y < -0.2f + d && Math.Abs(x) < 0.95f;
        }

        static Sprite Shape(string key, Func<float, float, bool> inside)
        {
            if (Cache.TryGetValue(key, out Sprite s) && s != null) return s;
            const int n = 96;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[n * n];
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    // 2×2 supersampling for soft edges
                    int hits = 0;
                    for (int sy = 0; sy < 2; sy++)
                        for (int sx = 0; sx < 2; sx++)
                        {
                            float x = ((i + 0.25f + sx * 0.5f) / n) * 2f - 1f;
                            float y = ((j + 0.25f + sy * 0.5f) / n) * 2f - 1f;
                            if (inside(x, y)) hits++;
                        }
                    px[j * n + i] = new Color32(255, 255, 255, (byte)(hits * 255 / 4));
                }
            tex.SetPixels32(px);
            tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            Cache[key] = s;
            return s;
        }

        /// <summary>A type badge: dark disc with the white type shape.</summary>
        public static RectTransform TypeBadge(Transform parent, FighterType t, float size)
        {
            Image disc = UiKit.MakeImage(parent, "TypeBadge", UiKit.Circle, new Color(0.08f, 0.08f, 0.1f, 0.9f));
            disc.rectTransform.sizeDelta = new Vector2(size, size);
            Image shape = UiKit.MakeImage(disc.rectTransform, "Shape", TypeIcon(t), Color.white);
            UiKit.Center(shape.rectTransform, new Vector2(size * 0.66f, size * 0.66f), Vector2.zero);
            return disc.rectTransform;
        }

        /// <summary>Role pips: 선봉 ●○○, 연결 ●●○, 마무리 ●●●.</summary>
        public static RectTransform RolePips(Transform parent, ChainRole role, float size)
        {
            RectTransform root = UiKit.MakeRect(parent, "RolePips");
            root.sizeDelta = new Vector2(size * 3.4f, size);
            for (int i = 0; i < 3; i++)
            {
                bool on = i <= (int)role;
                Image pip = UiKit.MakeImage(root, "Pip", UiKit.Circle, on ? new Color32(0xEF, 0x9F, 0x27, 0xFF) : new Color(1f, 1f, 1f, 0.2f));
                pip.rectTransform.anchorMin = pip.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                pip.rectTransform.pivot = new Vector2(0f, 0.5f);
                pip.rectTransform.sizeDelta = new Vector2(size * 0.9f, size * 0.9f);
                pip.rectTransform.anchoredPosition = new Vector2(i * size * 1.15f, 0f);
            }
            return root;
        }
    }

    /// <summary>Character art (Resources/Art/Awakened|Standing/{id}.png) with a readable placeholder when missing.</summary>
    public static class CharacterArt
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite Awakened(string id) => Load("Art/Awakened/" + id);
        public static Sprite Standing(string id) => Load("Art/Standing/" + id) ?? Awakened(id);

        static Sprite Load(string path)
        {
            if (Cache.TryGetValue(path, out Sprite s)) return s;
            var tex = Resources.Load<Texture2D>(path);
            s = tex != null ? Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f) : null;
            Cache[path] = s;
            return s;
        }

        /// <summary>
        /// Fills <paramref name="area"/> with the art (aspect kept, bottom-anchored) or a placeholder:
        /// attribute-colored panel with the short name and type shape.
        /// </summary>
        public static Image Place(RectTransform area, CharacterData c, bool standing, float alpha = 1f)
        {
            Sprite art = standing ? Standing(c.Id) : Awakened(c.Id);
            if (art != null)
            {
                Image img = UiKit.MakeImage(area, "Art", art, new Color(1f, 1f, 1f, alpha));
                img.preserveAspect = true;
                UiKit.Stretch(img.rectTransform);
                return img;
            }
            Color col = UiKit.AttributeColor(c.Attribute);
            Image ph = UiKit.MakeImage(area, "Placeholder", UiKit.Square, new Color(col.r * 0.55f, col.g * 0.55f, col.b * 0.55f, 0.9f * alpha));
            UiKit.Stretch(ph.rectTransform, 12f, 12f, 12f, 12f);
            Image shape = UiKit.MakeImage(ph.rectTransform, "Shape", IconKit.TypeIcon(c.Type), new Color(1f, 1f, 1f, 0.18f));
            shape.preserveAspect = true;
            UiKit.Stretch(shape.rectTransform, 20f, 20f, 20f, 20f);
            Text t = UiKit.MakeText(ph.rectTransform, "Name", 64, TextAnchor.MiddleCenter, Color.white);
            UiKit.Stretch(t.rectTransform);
            t.text = c.Short + "\n<size=26>(그림 없음)</size>";
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            return ph;
        }
    }
}
