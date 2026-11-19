using RingsideHearts.Battle.Core;
using UnityEngine;
using UnityEngine.UI;

namespace RingsideHearts.Battle.View
{
    /// <summary>
    /// Graybox fighter: attribute-colored circle with short name and type shape, an outer stamina
    /// ring, an inner body ring, and the special countdown above. Can lift off the floor (jumps,
    /// throws) leaving a shadow, and glow with an aura (showman bounces).
    /// </summary>
    public sealed class UnitView : MonoBehaviour
    {
        const float FlashSeconds = 0.18f;
        const float RingWidth = 12f;

        RingView _ring;
        RectTransform _visual;
        Image _shadow;
        Image _body;
        Image _flash;
        Image _staminaRing;
        Image _bodyRing;
        Image _highlight;
        Image _aura;
        Text _tag;
        Image _specialBack;
        Text _special;
        float _flashLeft;
        bool _down;
        float _diameter;
        float _lift;
        float _auraLevel;
        float _pulse;
        bool _enemyThreat;

        public RectTransform Rect { get; private set; }
        public Color BaseColor { get; private set; }
        public Fighter Fighter { get; private set; }
        public Vec2 RingPosition { get; private set; }
        public float Diameter => _diameter;

        public static UnitView Create(RingView ring, Fighter f, float radius)
        {
            float diameter = radius * 2f * ring.UnitsPerRing;
            RectTransform rt = UiKit.MakeRect(ring.UnitLayer, "Unit_" + f.Data.Id);
            UiKit.Center(rt, new Vector2(diameter, diameter), Vector2.zero);
            var view = rt.gameObject.AddComponent<UnitView>();
            view._ring = ring;
            view.Rect = rt;
            view.Fighter = f;
            view._diameter = diameter;
            view.BaseColor = UiKit.AttributeColor(f.Data.Attribute);

            view._shadow = UiKit.MakeImage(rt, "Shadow", UiKit.Circle, new Color(0f, 0f, 0f, 0f));
            UiKit.Center(view._shadow.rectTransform, new Vector2(diameter, diameter * 0.5f), new Vector2(0f, -diameter * 0.15f));
            view._visual = UiKit.MakeRect(rt, "Visual");
            UiKit.Stretch(view._visual);
            RectTransform v = view._visual;

            float outer = diameter + RingWidth * 2f;
            view._aura = UiKit.MakeImage(v, "Aura", UiKit.Circle, new Color(1f, 0.8f, 0.2f, 0f));
            UiKit.Center(view._aura.rectTransform, new Vector2(outer, outer), Vector2.zero);
            view._highlight = UiKit.MakeImage(v, "Highlight", UiKit.Circle, new Color(1f, 0.85f, 0.3f, 0f));
            UiKit.Center(view._highlight.rectTransform, new Vector2(outer + 22f, outer + 22f), Vector2.zero);
            Image back = UiKit.MakeImage(v, "StaminaBack", UiKit.Circle, new Color(0f, 0f, 0f, 0.55f));
            UiKit.Center(back.rectTransform, new Vector2(outer, outer), Vector2.zero);
            view._staminaRing = Ring(v, outer, view.BaseColor);
            Image gap = UiKit.MakeImage(v, "Gap", UiKit.Circle, UiKit.Floor);
            UiKit.Center(gap.rectTransform, new Vector2(diameter + 4f, diameter + 4f), Vector2.zero);
            view._bodyRing = Ring(v, diameter + 2f, new Color(1f, 1f, 1f, 0.9f));
            view._body = UiKit.MakeImage(v, "Body", UiKit.Circle, view.BaseColor);
            UiKit.Center(view._body.rectTransform, new Vector2(diameter - 10f, diameter - 10f), Vector2.zero);
            view._flash = UiKit.MakeImage(v, "Flash", UiKit.Circle, new Color(1f, 1f, 1f, 0f));
            UiKit.Center(view._flash.rectTransform, new Vector2(diameter, diameter), Vector2.zero);

            Text name = UiKit.MakeText(v, "Name", Mathf.RoundToInt(diameter * 0.26f), TextAnchor.MiddleCenter, new Color32(0x1A, 0x1A, 0x1A, 0xFF));
            UiKit.Center(name.rectTransform, new Vector2(diameter * 1.4f, diameter * 0.4f), new Vector2(0f, diameter * 0.12f));
            name.text = f.Data.Short;
            Image type = UiKit.MakeImage(v, "Type", IconKit.TypeIcon(f.Data.Type), new Color32(0x1A, 0x1A, 0x1A, 0xC8));
            UiKit.Center(type.rectTransform, new Vector2(diameter * 0.3f, diameter * 0.3f), new Vector2(0f, -diameter * 0.2f));

            view._specialBack = UiKit.MakeImage(v, "SpecialBack", UiKit.Circle, new Color(0f, 0f, 0f, 0.75f));
            UiKit.Center(view._specialBack.rectTransform, new Vector2(42f, 42f), new Vector2(outer * 0.42f, outer * 0.42f));
            view._special = UiKit.MakeText(view._specialBack.rectTransform, "Special", 26, TextAnchor.MiddleCenter, UiKit.TextDim);
            UiKit.Stretch(view._special.rectTransform);
            view._tag = Label(v, 24, new Vector2(0f, -outer * 0.5f - 16f), UiKit.TextMain);
            return view;
        }

        static Image Ring(RectTransform parent, float size, Color color)
        {
            Image img = UiKit.MakeImage(parent, "Ring", UiKit.Circle, color);
            UiKit.Center(img.rectTransform, new Vector2(size, size), Vector2.zero);
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Radial360;
            img.fillOrigin = (int)Image.Origin360.Top;
            img.fillClockwise = false;
            img.fillAmount = 1f;
            return img;
        }

        static Text Label(RectTransform parent, int size, Vector2 at, Color color)
        {
            Text t = UiKit.MakeText(parent, "Label", size, TextAnchor.MiddleCenter, color);
            UiKit.Center(t.rectTransform, new Vector2(260f, 40f), at);
            var outline = t.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            return t;
        }

        public void SetRingPosition(Vec2 p)
        {
            RingPosition = p;
            Rect.anchoredPosition = _ring.RingToLocal(p);
        }

        /// <summary>0 = on the floor, 1 = top of a jump: the body rises and grows, its shadow stays.</summary>
        public void SetLift(float h)
        {
            _lift = Mathf.Clamp01(h);
            _visual.anchoredPosition = new Vector2(0f, _lift * _diameter * 0.9f);
            _visual.localScale = Vector3.one * (1f + 0.55f * _lift);
            _shadow.color = new Color(0f, 0f, 0f, _lift > 0.01f ? 0.45f - 0.2f * _lift : 0f);
            _shadow.rectTransform.localScale = Vector3.one * (1f - 0.3f * _lift);
            if (_lift > 0.01f) Rect.SetAsLastSibling();
        }

        /// <summary>Showman build-up: 0 = none, each bounce one level brighter and bigger.</summary>
        public void SetAura(int level)
        {
            _auraLevel = level;
            if (level <= 0)
            {
                _aura.color = new Color(1f, 0.8f, 0.2f, 0f);
                return;
            }
            Color[] steps = { new Color(1f, 1f, 1f), new Color(1f, 0.9f, 0.3f), new Color(1f, 0.6f, 0.15f), new Color(1f, 0.3f, 0.15f), new Color(1f, 0.2f, 0.6f) };
            Color c = steps[Mathf.Min(level, steps.Length) - 1];
            _aura.color = new Color(c.r, c.g, c.b, 0.55f);
            _aura.rectTransform.sizeDelta = Vector2.one * (_diameter + 24f + level * 22f);
        }

        /// <summary>Redraws everything from the fighter's current state.</summary>
        public void Refresh(float wakeRatio, bool acting, bool enemyThreat = false)
        {
            Fighter f = Fighter;
            SetRingPosition(f.Position);
            bool apron = !f.InRing;
            Rect.localScale = Vector3.one * (apron ? 0.8f : 1f);
            _down = f.IsDown;
            _body.color = f.IsDown ? UiKit.Gray : f.Fallen > 0 ? BaseColor * 0.45f : BaseColor;
            if (f.IsDown)
            {
                _staminaRing.color = Color.white;
                _staminaRing.fillAmount = Mathf.Clamp01(f.Stamina / (f.MaxStamina * wakeRatio));
            }
            else
            {
                _staminaRing.color = BaseColor;
                _staminaRing.fillAmount = f.StaminaRatio;
            }
            _bodyRing.fillAmount = f.BodyRatio;
            _special.text = f.SpecialReady ? "!" : f.SpecialCount.ToString();
            _special.color = f.SpecialReady ? Color.white : UiKit.TextDim;
            _enemyThreat = enemyThreat && f.SpecialReady && f.InRing;
            _specialBack.color = f.SpecialReady ? (_enemyThreat ? UiKit.Red : new Color32(0xD8, 0x8A, 0x10, 0xFF)) : new Color(0f, 0f, 0f, 0.75f);
            string tag = "";
            if (f.IsDown) tag = "DOWN";
            if (apron && f.Fallen > 0) tag = "낙하 " + f.Fallen;
            else if (apron && f.TagLock > 0) tag = "잠금 " + f.TagLock;
            else if (!apron && f.OnTurnbuckle) tag = "코너 위!";
            _tag.text = tag;
            _tag.color = f.IsDown ? UiKit.Red : UiKit.TextMain;
            _highlight.color = new Color(1f, 0.85f, 0.3f, acting ? 0.55f : 0f);
            SetLift(0f);
        }

        /// <summary>Shows a stamina value mid-shot (hits land before the state is redrawn).</summary>
        public void ShowStamina(float stamina, float max, float body, float maxBody)
        {
            if (_down) return;
            _staminaRing.fillAmount = Mathf.Clamp01(stamina / max);
            _bodyRing.fillAmount = Mathf.Clamp01(body / maxBody);
        }

        public void Flash() => _flashLeft = FlashSeconds;

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (_flashLeft > 0f)
            {
                _flashLeft -= dt;
                _flash.color = new Color(1f, 1f, 1f, Mathf.Clamp01(_flashLeft / FlashSeconds) * 0.85f);
            }
            if (Fighter != null && Fighter.SpecialReady)
            {
                _pulse += dt * (_enemyThreat ? 8f : 5f);
                _specialBack.rectTransform.localScale = Vector3.one * (1f + 0.15f * Mathf.Sin(_pulse));
            }
            if (_auraLevel > 0f)
                _aura.rectTransform.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(Time.unscaledTime * 12f));
        }
    }
}
