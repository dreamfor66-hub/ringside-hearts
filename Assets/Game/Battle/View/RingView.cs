using System.Collections.Generic;
using RingsideHearts.Battle.Core;
using UnityEngine;
using UnityEngine.UI;

namespace RingsideHearts.Battle.View
{
    /// <summary>
    /// Draws the apron band, ring floor, ropes and the near-rope zone inside the Ring zone, and maps
    /// ring coordinates (0..ring.size, y up) to local canvas positions.
    /// </summary>
    public sealed class RingView : MonoBehaviour
    {
        const float RopeThickness = 8f;
        const float PostSize = 26f;

        float _ringSize;
        float _half;
        RectTransform _ropeLayer;
        readonly RectTransform[] _segments = new RectTransform[8];

        public float UnitsPerRing { get; private set; }
        /// <summary>Multiplies how far ropes bend toward a body (special moves pull them harder).</summary>
        public float RopeExaggerate = 1f;
        /// <summary>Same rect as the floor; units and apron units are placed here.</summary>
        public RectTransform UnitLayer { get; private set; }
        /// <summary>Above units: aim guide and floating text.</summary>
        public RectTransform FxLayer { get; private set; }

        public void Build(RectTransform ringZone, Tuning t)
        {
            _ringSize = t.Get("ring.size");
            float zone = ringZone.rect.width > 0f ? ringZone.rect.width : ScreenLayout.RingSize;
            float apron = zone * t.Get("view.apronRatio");
            float floorSize = zone - apron * 2f;
            UnitsPerRing = floorSize / _ringSize;

            transform.SetParent(ringZone, false);
            var root = gameObject.GetComponent<RectTransform>();
            if (root == null) root = gameObject.AddComponent<RectTransform>();
            UiKit.Stretch(root);

            Image apronImage = UiKit.MakeImage(root, "Apron", UiKit.Square, UiKit.Apron);
            UiKit.Stretch(apronImage.rectTransform);

            Image floor = UiKit.MakeImage(root, "Floor", UiKit.Square, UiKit.Floor);
            UiKit.Center(floor.rectTransform, new Vector2(floorSize, floorSize), Vector2.zero);

            float near = t.Get("ring.nearRopeDiameters") * 2f * t.Get("unit.radius") * UnitsPerRing;
            BuildNearRopeBand(floor.rectTransform, floorSize, near);

            float half = floorSize * 0.5f;
            _half = half;
            _ropeLayer = UiKit.MakeRect(root, "Ropes");
            UiKit.Center(_ropeLayer, new Vector2(floorSize, floorSize), Vector2.zero);
            for (int i = 0; i < 8; i++)
            {
                Image seg = UiKit.MakeImage(_ropeLayer, "RopeSegment", UiKit.Square, UiKit.Rope);
                seg.rectTransform.anchorMin = seg.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                seg.rectTransform.pivot = new Vector2(0f, 0.5f);
                _segments[i] = seg.rectTransform;
            }
            UpdateRopes(null, 0f);
            foreach (var corner in new[] { new Vector2(-1, -1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(1, 1) })
            {
                Image post = UiKit.MakeImage(root, "Post", UiKit.Square, UiKit.Rope);
                UiKit.Center(post.rectTransform, new Vector2(PostSize, PostSize), corner * half);
            }

            UnitLayer = UiKit.MakeRect(root, "Units");
            UiKit.Center(UnitLayer, new Vector2(floorSize, floorSize), Vector2.zero);
            FxLayer = UiKit.MakeRect(root, "Fx");
            UiKit.Center(FxLayer, new Vector2(floorSize, floorSize), Vector2.zero);
        }

        /// <summary>Ring coordinates to a local position in <see cref="UnitLayer"/> / <see cref="FxLayer"/>.</summary>
        public Vector2 RingToLocal(Vec2 p)
        {
            float half = _ringSize * 0.5f;
            return new Vector2((p.X - half) * UnitsPerRing, (p.Y - half) * UnitsPerRing);
        }

        /// <summary>A canvas-space drag delta converted to ring units (y up).</summary>
        public Vec2 CanvasDeltaToRing(Vector2 delta) => new Vec2(delta.x / UnitsPerRing, delta.y / UnitsPerRing);

        /// <summary>
        /// Bends each rope toward the body pressing into it the most (bodies in ring coordinates).
        /// Pass null to draw straight ropes.
        /// </summary>
        public void UpdateRopes(IReadOnlyList<Vec2> bodies, float radius)
        {
            float h = _half;
            float r = radius * UnitsPerRing;
            // side order: right, left, top, bottom; each rope is drawn corner A -> bend -> corner B
            for (int side = 0; side < 4; side++)
            {
                bool vertical = side < 2;
                float sign = side % 2 == 0 ? 1f : -1f;
                Vector2 a = vertical ? new Vector2(sign * h, -h) : new Vector2(-h, sign * h);
                Vector2 b = vertical ? new Vector2(sign * h, h) : new Vector2(h, sign * h);
                Vector2 bend = (a + b) * 0.5f;
                float best = 0f;
                if (bodies != null)
                {
                    foreach (Vec2 p in bodies)
                    {
                        Vector2 l = RingToLocal(p);
                        float edge = (vertical ? l.x : l.y) * sign + r;
                        float push = (edge - h) * (edge > h ? RopeExaggerate : 1f);
                        if (push > best)
                        {
                            best = push;
                            float along = Mathf.Clamp(vertical ? l.y : l.x, -h + 1f, h - 1f);
                            bend = vertical ? new Vector2(sign * (h + push), along) : new Vector2(along, sign * (h + push));
                        }
                    }
                }
                Segment(_segments[side * 2], a, bend);
                Segment(_segments[side * 2 + 1], bend, b);
            }
        }

        static void Segment(RectTransform seg, Vector2 from, Vector2 to)
        {
            Vector2 d = to - from;
            seg.anchoredPosition = from;
            seg.sizeDelta = new Vector2(d.magnitude, RopeThickness);
            seg.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        }

        static void BuildNearRopeBand(RectTransform floor, float floorSize, float width)
        {
            float half = floorSize * 0.5f;
            float inner = floorSize - width * 2f;
            MakeTint(floor, new Vector2(floorSize, width), new Vector2(0f, half - width * 0.5f));
            MakeTint(floor, new Vector2(floorSize, width), new Vector2(0f, -half + width * 0.5f));
            MakeTint(floor, new Vector2(width, inner), new Vector2(-half + width * 0.5f, 0f));
            MakeTint(floor, new Vector2(width, inner), new Vector2(half - width * 0.5f, 0f));
        }

        static void MakeTint(RectTransform parent, Vector2 size, Vector2 position)
        {
            Image tint = UiKit.MakeImage(parent, "NearRope", UiKit.Square, UiKit.NearRope);
            UiKit.Center(tint.rectTransform, size, position);
        }
    }
}
