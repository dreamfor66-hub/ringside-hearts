using System;
using System.Collections;
using RingsideHearts.Battle.Core;
using UnityEngine;
using UnityEngine.UI;

namespace RingsideHearts.Battle.View
{
    /// <summary>
    /// Presentation without art: screen shake, flashes, shockwaves, punch text, blackouts,
    /// banners, the special-move time-stop cut-in and the referee's yellow card.
    /// </summary>
    public sealed class BattleFx : MonoBehaviour
    {
        RectTransform _shakeTarget;
        Vector2 _shakeOrigin;
        float _shakeLeft;
        float _shakeAmp;
        RectTransform _overlayRoot;
        RingView _ring;
        Image _blackout;
        Image _flash;
        float _flashLeft;
        float _flashTotal;

        public bool Busy { get; private set; }

        public static BattleFx Create(RectTransform safe, RectTransform shakeTarget, RingView ring)
        {
            RectTransform root = UiKit.MakeRect(safe, "Fx");
            UiKit.Stretch(root);
            var fx = root.gameObject.AddComponent<BattleFx>();
            fx._overlayRoot = root;
            fx._shakeTarget = shakeTarget;
            fx._shakeOrigin = shakeTarget.anchoredPosition;
            fx._ring = ring;
            fx._blackout = UiKit.MakeImage(root, "Blackout", UiKit.Square, new Color(0f, 0f, 0f, 0f));
            UiKit.Stretch(fx._blackout.rectTransform);
            fx._flash = UiKit.MakeImage(ring.FxLayer, "Flash", UiKit.Square, new Color(1f, 1f, 1f, 0f));
            UiKit.Stretch(fx._flash.rectTransform, -200f, -200f, -200f, -200f);
            return fx;
        }

        public void Shake(float amplitude, float seconds)
        {
            if (amplitude < _shakeAmp && _shakeLeft > 0f) return;
            _shakeAmp = amplitude;
            _shakeLeft = seconds;
        }

        public void Flash(float alpha, float seconds)
        {
            _flashTotal = _flashLeft = seconds;
            _flash.color = new Color(1f, 1f, 1f, alpha);
            _flash.transform.SetAsLastSibling();
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (_shakeLeft > 0f)
            {
                _shakeLeft -= dt;
                float a = _shakeAmp * Mathf.Clamp01(_shakeLeft * 4f);
                _shakeTarget.anchoredPosition = _shakeOrigin + new Vector2(UnityEngine.Random.Range(-a, a), UnityEngine.Random.Range(-a, a));
                if (_shakeLeft <= 0f)
                {
                    _shakeTarget.anchoredPosition = _shakeOrigin;
                    _shakeAmp = 0f;
                }
            }
            if (_flashLeft > 0f)
            {
                _flashLeft -= dt;
                Color c = _flash.color;
                _flash.color = new Color(c.r, c.g, c.b, Mathf.Max(0f, c.a * (_flashLeft / Mathf.Max(0.001f, _flashTotal))));
            }
        }

        /// <summary>An expanding ring at a ring-layer position (radius in canvas units).</summary>
        public void Shockwave(Vector2 at, float radius, Color color, float seconds = 0.45f)
        {
            Image img = UiKit.MakeImage(_ring.FxLayer, "Shockwave", IconKit.Ring, color);
            UiKit.Center(img.rectTransform, new Vector2(20f, 20f), at);
            StartCoroutine(Grow(img, radius * 2f, seconds));
        }

        IEnumerator Grow(Image img, float size, float seconds)
        {
            float t = 0f;
            Color c = img.color;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                float k = t / seconds;
                float e = 1f - (1f - k) * (1f - k);
                img.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(20f, size, e);
                img.color = new Color(c.r, c.g, c.b, c.a * (1f - k));
                yield return null;
            }
            Destroy(img.gameObject);
        }

        /// <summary>Text that slams in big and settles (in the ring layer).</summary>
        public void Punch(Vector2 at, string text, Color color, int size = 64, float seconds = 0.9f)
        {
            Text t = UiKit.MakeText(_ring.FxLayer, "Punch", size, TextAnchor.MiddleCenter, color);
            UiKit.Center(t.rectTransform, new Vector2(900f, size * 1.6f), at);
            t.text = text;
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.95f);
            o.effectDistance = new Vector2(3f, -3f);
            StartCoroutine(PunchRoutine(t, seconds));
        }

        IEnumerator PunchRoutine(Text t, float seconds)
        {
            float time = 0f;
            Color c = t.color;
            Vector2 start = t.rectTransform.anchoredPosition;
            while (time < seconds)
            {
                time += Time.unscaledDeltaTime;
                float k = time / seconds;
                float scale = k < 0.12f ? Mathf.Lerp(2.2f, 1f, k / 0.12f) : 1f;
                t.rectTransform.localScale = Vector3.one * scale;
                t.rectTransform.anchoredPosition = start + new Vector2(0f, 30f * Mathf.Max(0f, k - 0.6f) / 0.4f);
                t.color = new Color(c.r, c.g, c.b, k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f);
                yield return null;
            }
            Destroy(t.gameObject);
        }

        /// <summary>A straight flash line across the ring (assist lariat).</summary>
        public void Beam(Vector2 from, Vector2 to, float width, Color color, float seconds = 0.5f)
        {
            Image img = UiKit.MakeImage(_ring.FxLayer, "Beam", UiKit.Square, color);
            RectTransform rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            Vector2 d = to - from;
            rt.anchoredPosition = from;
            rt.sizeDelta = new Vector2(d.magnitude, width);
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            StartCoroutine(Fade(img, seconds));
        }

        IEnumerator Fade(Image img, float seconds)
        {
            float t = 0f;
            Color c = img.color;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                img.color = new Color(c.r, c.g, c.b, c.a * (1f - t / seconds));
                yield return null;
            }
            Destroy(img.gameObject);
        }

        /// <summary>A faded copy of a unit left behind (stampeder trail).</summary>
        public void Afterimage(Vector2 at, float diameter, Color color)
        {
            Image img = UiKit.MakeImage(_ring.UnitLayer, "Afterimage", UiKit.Circle, new Color(color.r, color.g, color.b, 0.35f));
            UiKit.Center(img.rectTransform, new Vector2(diameter, diameter), at);
            img.transform.SetAsFirstSibling();
            StartCoroutine(Fade(img, 0.3f));
        }

        /// <summary>A banner sliding across the middle of the screen (turn changes, warnings).</summary>
        public void Banner(string text, Color color, float seconds = 1.0f)
        {
            Image band = UiKit.MakeImage(_overlayRoot, "Banner", UiKit.Square, new Color(0f, 0f, 0f, 0.7f));
            band.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            band.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            band.rectTransform.sizeDelta = new Vector2(0f, 120f);
            band.rectTransform.anchoredPosition = new Vector2(0f, 180f);
            Text t = UiKit.MakeText(band.rectTransform, "Text", 52, TextAnchor.MiddleCenter, color);
            UiKit.Stretch(t.rectTransform);
            t.text = text;
            t.gameObject.AddComponent<Outline>().effectColor = Color.black;
            StartCoroutine(Slide(band, t, seconds));
        }

        IEnumerator Slide(Image band, Text t, float seconds)
        {
            float time = 0f;
            while (time < seconds)
            {
                time += Time.unscaledDeltaTime;
                float k = time / seconds;
                float x = k < 0.15f ? Mathf.Lerp(1200f, 0f, k / 0.15f) : k > 0.85f ? Mathf.Lerp(0f, -1200f, (k - 0.85f) / 0.15f) : 0f;
                t.rectTransform.anchoredPosition = new Vector2(x, 0f);
                band.color = new Color(0f, 0f, 0f, 0.7f * (k < 0.1f ? k / 0.1f : k > 0.9f ? (1f - k) / 0.1f : 1f));
                yield return null;
            }
            Destroy(band.gameObject);
        }

        /// <summary>
        /// Time stops: the screen goes dark, the fighter's awakened art slams in with the move's name,
        /// then the action resumes. Joint specials show all three fighters.
        /// </summary>
        public IEnumerator SpecialCutIn(Fighter[] fighters, string moveName, float seconds)
        {
            Busy = true;
            RectTransform root = UiKit.MakeRect(_overlayRoot, "CutIn");
            UiKit.Stretch(root);
            Image dark = UiKit.MakeImage(root, "Dark", UiKit.Square, new Color(0f, 0f, 0f, 0f));
            UiKit.Stretch(dark.rectTransform);
            Color col = UiKit.AttributeColor(fighters[0].Data.Attribute);
            Image band = UiKit.MakeImage(root, "Band", UiKit.Square, new Color(col.r, col.g, col.b, 0.85f));
            band.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            band.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            band.rectTransform.sizeDelta = new Vector2(0f, 560f);
            band.rectTransform.anchoredPosition = new Vector2(0f, 120f);
            band.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -6f);
            // speed streaks
            for (int i = 0; i < 9; i++)
            {
                Image s = UiKit.MakeImage(band.rectTransform, "Streak", UiKit.Square, new Color(1f, 1f, 1f, 0.18f));
                s.rectTransform.anchorMin = s.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                s.rectTransform.sizeDelta = new Vector2(UnityEngine.Random.Range(300f, 900f), UnityEngine.Random.Range(4f, 12f));
                s.rectTransform.anchoredPosition = new Vector2(UnityEngine.Random.Range(-500f, 500f), UnityEngine.Random.Range(-260f, 260f));
            }
            var arts = new RectTransform[fighters.Length];
            for (int i = 0; i < fighters.Length; i++)
            {
                RectTransform area = UiKit.MakeRect(band.rectTransform, "ArtArea");
                float w = fighters.Length == 1 ? 980f : 460f;
                UiKit.Center(area, new Vector2(w, 820f), new Vector2(fighters.Length == 1 ? 140f : (i - 1) * 330f, 40f));
                area.localRotation = Quaternion.Euler(0f, 0f, 6f);
                CharacterArt.Place(area, fighters[i].Data, false);
                arts[i] = area;
            }
            Text name = UiKit.MakeText(root, "Move", 80, TextAnchor.MiddleCenter, Color.white);
            UiKit.Center(name.rectTransform, new Vector2(1060f, 120f), new Vector2(0f, -230f));
            name.text = moveName;
            var o = name.gameObject.AddComponent<Outline>();
            o.effectColor = Color.black;
            o.effectDistance = new Vector2(4f, -4f);
            Text who = UiKit.MakeText(root, "Who", 40, TextAnchor.MiddleCenter, UiKit.Yellow);
            UiKit.Center(who.rectTransform, new Vector2(1060f, 60f), new Vector2(0f, -320f));
            who.text = string.Join(" · ", Array.ConvertAll(fighters, f => f.Data.Name));
            who.gameObject.AddComponent<Outline>().effectColor = Color.black;

            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                float k = t / seconds;
                dark.color = new Color(0f, 0f, 0f, 0.8f * Mathf.Clamp01(k * 6f) * (k > 0.85f ? (1f - k) / 0.15f : 1f));
                float slide = k < 0.18f ? Mathf.Lerp(-1400f, 0f, 1f - (1f - k / 0.18f) * (1f - k / 0.18f)) : k > 0.85f ? Mathf.Lerp(0f, 1400f, (k - 0.85f) / 0.15f) : -20f * (k - 0.18f);
                band.rectTransform.anchoredPosition = new Vector2(slide, 120f);
                name.rectTransform.localScale = Vector3.one * (k < 0.25f ? Mathf.Lerp(2f, 1f, Mathf.Clamp01((k - 0.12f) / 0.13f)) : 1f);
                name.color = new Color(1f, 1f, 1f, k < 0.12f ? 0f : k > 0.85f ? (1f - k) / 0.15f : 1f);
                who.color = new Color(UiKit.Yellow.r, UiKit.Yellow.g, UiKit.Yellow.b, name.color.a);
                yield return null;
            }
            Destroy(root.gameObject);
            Shake(16f, 0.25f);
            Busy = false;
        }

        /// <summary>The referee shows a yellow card over his head.</summary>
        public void YellowCard(Vector2 refLocal, int warnings, int penaltyAt)
        {
            RectTransform root = UiKit.MakeRect(_ring.FxLayer, "YellowCard");
            UiKit.Center(root, new Vector2(300f, 160f), refLocal + new Vector2(0f, 110f));
            Image card = UiKit.MakeImage(root, "Card", UiKit.Square, new Color32(0xF5, 0xD0, 0x2A, 0xFF));
            UiKit.Center(card.rectTransform, new Vector2(70f, 96f), new Vector2(-90f, 0f));
            card.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 12f);
            Text t = UiKit.MakeText(root, "Text", 34, TextAnchor.MiddleLeft, UiKit.Yellow);
            UiKit.Center(t.rectTransform, new Vector2(220f, 100f), new Vector2(60f, 0f));
            t.text = "경고 " + warnings + "/" + penaltyAt + (warnings >= penaltyAt ? "\n힘겨루기 불리!" : "");
            t.gameObject.AddComponent<Outline>().effectColor = Color.black;
            StartCoroutine(CardRoutine(root));
        }

        IEnumerator CardRoutine(RectTransform root)
        {
            float t = 0f;
            Vector2 start = root.anchoredPosition;
            while (t < 1.6f)
            {
                t += Time.unscaledDeltaTime;
                root.localScale = Vector3.one * (t < 0.15f ? Mathf.Lerp(0.2f, 1.1f, t / 0.15f) : t < 0.25f ? Mathf.Lerp(1.1f, 1f, (t - 0.15f) / 0.1f) : 1f);
                root.anchoredPosition = start + new Vector2(0f, t * 10f);
                yield return null;
            }
            Destroy(root.gameObject);
        }
    }
}
