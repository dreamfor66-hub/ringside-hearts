using System.Collections.Generic;
using RingsideHearts.Battle.Core;
using UnityEngine;
using UnityEngine.UI;

namespace RingsideHearts.Battle.View
{
    /// <summary>Referee, match objects, corner marks and the cage look on top of the ring floor.</summary>
    public sealed class FieldView : MonoBehaviour
    {
        RingView _ring;
        RectTransform _ref;
        Image _refBody;
        Text _refLabel;
        readonly Dictionary<FieldObject, RectTransform> _objects = new Dictionary<FieldObject, RectTransform>();

        public static FieldView Create(RingView ring, Match m)
        {
            RectTransform root = UiKit.MakeRect(ring.UnitLayer, "Field");
            UiKit.Stretch(root);
            root.SetAsFirstSibling();
            root.gameObject.AddComponent<RectMask2D>(); // corner marks stay inside the ropes
            var v = root.gameObject.AddComponent<FieldView>();
            v._ring = ring;
            Tuning t = m.Tuning;
            float upr = ring.UnitsPerRing;

            float size = t.Get("ring.size");
            float cornerR = t.Get("corner.radius") * upr;
            foreach (Vec2 c in new[] { new Vec2(0, 0), new Vec2(size, 0), new Vec2(0, size), new Vec2(size, size) })
            {
                Image glow = UiKit.MakeImage(root, "Corner", UiKit.Circle, new Color(1f, 0.85f, 0.3f, 0.10f));
                UiKit.Center(glow.rectTransform, new Vector2(cornerR * 2f, cornerR * 2f), ring.RingToLocal(c));
            }

            foreach (FieldObject o in m.Field.Objects)
            {
                float d = o.Radius * 2f * upr;
                Image img = UiKit.MakeImage(root, o.Name, o.Kind == ObjectKind.Table ? UiKit.Square : UiKit.Circle, new Color32(0x8B, 0x6B, 0x45, 0xFF));
                UiKit.Center(img.rectTransform, new Vector2(d, d), ring.RingToLocal(o.Position));
                Text label = UiKit.MakeText(img.rectTransform, "Label", 22, TextAnchor.MiddleCenter, UiKit.TextMain);
                UiKit.Stretch(label.rectTransform);
                label.text = o.Name;
                v._objects[o] = img.rectTransform;
            }

            float rd = t.Get("ref.radius") * 2f * upr;
            v._ref = UiKit.MakeRect(root, "Referee");
            UiKit.Center(v._ref, new Vector2(rd, rd), Vector2.zero);
            Image rim = UiKit.MakeImage(v._ref, "Rim", UiKit.Circle, new Color(0f, 0f, 0f, 0.8f));
            UiKit.Center(rim.rectTransform, new Vector2(rd + 6f, rd + 6f), Vector2.zero);
            v._refBody = UiKit.MakeImage(v._ref, "Body", UiKit.Circle, new Color32(0xF2, 0xF2, 0xF2, 0xFF));
            UiKit.Center(v._refBody.rectTransform, new Vector2(rd, rd), Vector2.zero);
            Text letter = UiKit.MakeText(v._ref, "Letter", 22, TextAnchor.MiddleCenter, new Color32(0x1A, 0x1A, 0x1A, 0xFF));
            UiKit.Stretch(letter.rectTransform);
            letter.text = "심";
            v._refLabel = UiKit.MakeText(v._ref, "State", 22, TextAnchor.MiddleCenter, UiKit.Red);
            UiKit.Center(v._refLabel.rectTransform, new Vector2(200f, 30f), new Vector2(0f, -rd * 0.5f - 18f));
            v._refLabel.gameObject.AddComponent<Outline>().effectColor = Color.black;
            return v;
        }

        public void SetReferee(Vec2 p) => _ref.anchoredPosition = _ring.RingToLocal(p);

        public void Refresh(Match m)
        {
            SetReferee(m.Field.Referee);
            bool down = m.Field.RefereeDown > 0;
            _refBody.color = down ? UiKit.Gray : new Color32(0xF2, 0xF2, 0xF2, 0xFF);
            _refLabel.text = down ? "심판 다운 " + m.Field.RefereeDown : "";
            foreach (var kv in _objects) kv.Value.gameObject.SetActive(!kv.Key.Broken);
        }

        public void Break(FieldObject o)
        {
            if (_objects.TryGetValue(o, out RectTransform rt)) rt.gameObject.SetActive(false);
        }
    }
}
