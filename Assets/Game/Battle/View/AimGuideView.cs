using System.Collections.Generic;
using RingsideHearts.Battle.Core;
using UnityEngine;
using UnityEngine.UI;

namespace RingsideHearts.Battle.View
{
    /// <summary>
    /// Dotted simulated path from the shooter into the first rope and back off it, a short dim segment
    /// along the way it leaves, a contact marker, and the shot power in percent.
    /// </summary>
    public sealed class AimGuideView : MonoBehaviour
    {
        const float DotSpacing = 34f;
        const float DotSize = 12f;
        const int MaxDots = 96;
        const float ReflectedLength = 1.0f;

        RingView _ring;
        readonly List<Image> _dots = new List<Image>();
        Image _marker;
        Text _power;

        public static AimGuideView Create(RingView ring)
        {
            RectTransform rt = UiKit.MakeRect(ring.FxLayer, "AimGuide");
            UiKit.Stretch(rt);
            var view = rt.gameObject.AddComponent<AimGuideView>();
            view._ring = ring;
            for (int i = 0; i < MaxDots; i++)
            {
                Image dot = UiKit.MakeImage(rt, "Dot", UiKit.Circle, Color.white);
                UiKit.Center(dot.rectTransform, new Vector2(DotSize, DotSize), Vector2.zero);
                dot.enabled = false;
                view._dots.Add(dot);
            }
            view._marker = UiKit.MakeImage(rt, "Contact", UiKit.Circle, new Color(1f, 1f, 1f, 0.35f));
            UiKit.Center(view._marker.rectTransform, new Vector2(34f, 34f), Vector2.zero);
            view._power = UiKit.MakeText(rt, "Power", 34, TextAnchor.MiddleCenter, UiKit.TextMain);
            UiKit.Center(view._power.rectTransform, new Vector2(160f, 50f), Vector2.zero);
            view.Hide();
            return view;
        }

        public void Show(Vec2 from, AimGuide guide, float power01)
        {
            Vector2 a = _ring.RingToLocal(from);
            int used = PlacePath(guide.Path, new Color(1f, 1f, 1f, 0.9f));
            if (guide.ReachesRope)
            {
                Vector2 exit = _ring.RingToLocal(guide.Exit);
                Vector2 tip = _ring.RingToLocal(guide.Exit + guide.Reflected * ReflectedLength);
                used = PlaceDots(exit, tip, used, new Color(1f, 1f, 1f, 0.35f));
            }
            for (int i = used; i < _dots.Count; i++) _dots[i].enabled = false;

            _marker.enabled = true;
            _marker.rectTransform.anchoredPosition = _ring.RingToLocal(guide.ReachesRope ? guide.Contact : guide.Stop);
            _power.enabled = true;
            _power.text = Mathf.RoundToInt(power01 * 100f) + "%";
            _power.rectTransform.anchoredPosition = a + new Vector2(0f, -_ring.UnitsPerRing * 1.15f);
        }

        /// <summary>Dots at even spacing along the simulated path (it bends into the rope and back).</summary>
        int PlacePath(List<Vec2> path, Color color)
        {
            int index = 0;
            float carried = 0f;
            for (int i = 1; i < path.Count && index < _dots.Count; i++)
            {
                Vector2 p0 = _ring.RingToLocal(path[i - 1]);
                Vector2 p1 = _ring.RingToLocal(path[i]);
                float length = Vector2.Distance(p0, p1);
                float at = DotSpacing - carried;
                while (at <= length && index < _dots.Count)
                {
                    Image dot = _dots[index++];
                    dot.enabled = true;
                    dot.color = color;
                    dot.rectTransform.anchoredPosition = Vector2.Lerp(p0, p1, at / length);
                    at += DotSpacing;
                }
                carried = length - (at - DotSpacing);
            }
            return index;
        }

        public void Hide()
        {
            foreach (Image dot in _dots) dot.enabled = false;
            if (_marker != null) _marker.enabled = false;
            if (_power != null) _power.enabled = false;
        }

        int PlaceDots(Vector2 a, Vector2 b, int start, Color color)
        {
            float length = Vector2.Distance(a, b);
            int count = Mathf.Max(1, Mathf.FloorToInt(length / DotSpacing));
            int index = start;
            for (int k = 1; k <= count && index < _dots.Count; k++, index++)
            {
                Image dot = _dots[index];
                dot.enabled = true;
                dot.color = color;
                dot.rectTransform.anchoredPosition = Vector2.Lerp(a, b, k / (float)count);
            }
            return index;
        }
    }
}
