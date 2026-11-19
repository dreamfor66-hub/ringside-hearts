using System;
using RingsideHearts.Battle.Core;
using UnityEngine;
using UnityEngine.UI;

namespace RingsideHearts.Battle.View
{
    /// <summary>
    /// Fighter card for either team (design 4.1), built to read at a glance: portrait in the
    /// attribute color, type shape and role pips, a big stamina bar with numbers, a body bar, the
    /// special countdown as a round badge, and a TAG highlight when tapping it would tag in.
    /// </summary>
    public sealed class FighterCard : MonoBehaviour
    {
        Fighter _fighter;
        float _wakeRatio = 0.3f;
        RectTransform _rt;
        Image _back;
        Image _frame;
        RectTransform _portrait;
        Text _name;
        RectTransform _badges;
        RectTransform _stamina;
        Image _staminaImage;
        Text _staminaText;
        RectTransform _body;
        Text _bodyText;
        Image _specialBack;
        Text _special;
        Text _state;
        Outline _outline;
        Button _button;
        float _pulse;

        public event Action<Fighter> Clicked;
        public Fighter Fighter => _fighter;

        public static FighterCard Create(RectTransform parent, string objectName, bool clickable)
        {
            Image bg = UiKit.MakeImage(parent, objectName, UiKit.Square, new Color32(0x2C, 0x2E, 0x36, 0xFF));
            var card = bg.gameObject.AddComponent<FighterCard>();
            card._back = bg;
            card._rt = bg.rectTransform;
            if (clickable)
            {
                bg.raycastTarget = true;
                card._button = bg.gameObject.AddComponent<Button>();
                card._button.transition = Selectable.Transition.None;
                card._button.onClick.AddListener(() => card.Clicked?.Invoke(card._fighter));
            }
            RectTransform rt = card._rt;
            card._outline = bg.gameObject.AddComponent<Outline>();
            card._outline.effectDistance = new Vector2(4f, -4f);
            card._outline.effectColor = new Color(1f, 0.85f, 0.3f, 0f);

            card._frame = UiKit.MakeImage(rt, "Frame", UiKit.Square, Color.white);
            Anchor(card._frame.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(92f, 92f), new Vector2(6f, 0f));
            card._frame.gameObject.AddComponent<RectMask2D>();
            card._portrait = UiKit.MakeRect(card._frame.rectTransform, "Portrait");

            card._name = UiKit.MakeText(rt, "Name", 26, TextAnchor.UpperLeft, UiKit.TextMain);
            Anchor(card._name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(330f, 34f), new Vector2(108f, -4f));
            card._name.supportRichText = true;
            card._badges = UiKit.MakeRect(rt, "Badges");
            Anchor(card._badges, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(330f, 34f), new Vector2(108f, -40f));

            Image sBack = UiKit.MakeImage(rt, "StaminaBack", UiKit.Square, new Color(0f, 0f, 0f, 0.55f));
            sBack.rectTransform.anchorMin = new Vector2(0f, 1f);
            sBack.rectTransform.anchorMax = new Vector2(1f, 1f);
            sBack.rectTransform.pivot = new Vector2(0f, 1f);
            sBack.rectTransform.offsetMin = new Vector2(450f, -46f);
            sBack.rectTransform.offsetMax = new Vector2(-96f, -10f);
            card._staminaImage = UiKit.MakeImage(sBack.rectTransform, "Fill", UiKit.Square, Color.white);
            card._stamina = Fill(card._staminaImage.rectTransform);
            card._staminaText = UiKit.MakeText(sBack.rectTransform, "Value", 24, TextAnchor.MiddleLeft, Color.white);
            UiKit.Stretch(card._staminaText.rectTransform, 10f, 0f, 10f, 0f);
            card._staminaText.gameObject.AddComponent<Outline>().effectColor = Color.black;

            Image bBack = UiKit.MakeImage(rt, "BodyBack", UiKit.Square, new Color(0f, 0f, 0f, 0.55f));
            bBack.rectTransform.anchorMin = new Vector2(0f, 1f);
            bBack.rectTransform.anchorMax = new Vector2(1f, 1f);
            bBack.rectTransform.pivot = new Vector2(0f, 1f);
            bBack.rectTransform.offsetMin = new Vector2(450f, -70f);
            bBack.rectTransform.offsetMax = new Vector2(-96f, -54f);
            card._body = Fill(UiKit.MakeImage(bBack.rectTransform, "Fill", UiKit.Square, new Color(0.92f, 0.92f, 0.95f, 0.9f)).rectTransform);
            card._bodyText = UiKit.MakeText(bBack.rectTransform, "Value", 16, TextAnchor.MiddleLeft, Color.black);
            UiKit.Stretch(card._bodyText.rectTransform, 8f, 0f, 8f, 0f);

            card._state = UiKit.MakeText(rt, "State", 22, TextAnchor.LowerLeft, UiKit.Red);
            card._state.rectTransform.anchorMin = new Vector2(0f, 0f);
            card._state.rectTransform.anchorMax = new Vector2(1f, 0f);
            card._state.rectTransform.pivot = new Vector2(0f, 0f);
            card._state.rectTransform.offsetMin = new Vector2(450f, 4f);
            card._state.rectTransform.offsetMax = new Vector2(-96f, 30f);

            card._specialBack = UiKit.MakeImage(rt, "Special", UiKit.Circle, new Color(0f, 0f, 0f, 0.6f));
            Anchor(card._specialBack.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(76f, 76f), new Vector2(-10f, 0f));
            card._special = UiKit.MakeText(card._specialBack.rectTransform, "Count", 30, TextAnchor.MiddleCenter, Color.white);
            UiKit.Stretch(card._special.rectTransform);
            return card;
        }

        static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 size, Vector2 pos)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
        }

        static RectTransform Fill(RectTransform f)
        {
            f.anchorMin = Vector2.zero;
            f.anchorMax = Vector2.one;
            f.pivot = new Vector2(0f, 0.5f);
            f.offsetMin = f.offsetMax = Vector2.zero;
            return f;
        }

        public void Bind(Fighter fighter, float wakeRatio)
        {
            _fighter = fighter;
            _wakeRatio = wakeRatio;
            gameObject.SetActive(fighter != null);
            if (fighter == null) return;
            foreach (Transform c in _portrait) Destroy(c.gameObject);
            foreach (Transform c in _badges) Destroy(c.gameObject);
            Color col = UiKit.AttributeColor(fighter.Data.Attribute);
            _frame.color = col;
            // show the upper body: the art area is taller than the frame and anchored at its top
            _portrait.anchorMin = _portrait.anchorMax = new Vector2(0.5f, 1f);
            _portrait.pivot = new Vector2(0.5f, 1f);
            _portrait.sizeDelta = new Vector2(150f, 220f);
            _portrait.anchoredPosition = new Vector2(0f, 6f);
            CharacterArt.Place(_portrait, fighter.Data, false);
            RectTransform type = IconKit.TypeBadge(_badges, fighter.Data.Type, 32f);
            Anchor(type, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(32f, 32f), Vector2.zero);
            Text typeName = UiKit.MakeText(_badges, "Type", 20, TextAnchor.MiddleLeft, UiKit.TextDim);
            Anchor(typeName.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(110f, 30f), new Vector2(38f, 0f));
            typeName.text = CharacterData.TypeNames[(int)fighter.Data.Type];
            RectTransform pips = IconKit.RolePips(_badges, fighter.Data.Role, 16f);
            Anchor(pips, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), pips.sizeDelta, new Vector2(156f, 0f));
            Image assist = UiKit.MakeImage(_badges, "Assist", IconKit.Hand, new Color(0.55f, 0.9f, 0.55f, fighter.Data.Assist == AssistKind.None ? 0.2f : 1f));
            Anchor(assist.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(26f, 26f), new Vector2(222f, 0f));
            Text assistName = UiKit.MakeText(_badges, "AssistName", 18, TextAnchor.MiddleLeft, UiKit.TextDim);
            Anchor(assistName.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(100f, 30f), new Vector2(252f, 0f));
            assistName.text = CharacterData.AssistNames[(int)fighter.Data.Assist];
            Refresh(false, false);
        }

        /// <param name="acting">This fighter acts now.</param>
        /// <param name="taggable">Tapping this card would tag it in now.</param>
        public void Refresh(bool acting, bool taggable)
        {
            Fighter f = _fighter;
            if (f == null) return;
            Color c = UiKit.AttributeColor(f.Data.Attribute);
            _name.text = f.Data.Name + "  <size=18><color=#A8AAB2>" + (f.InRing ? "링 안" : "대기") + "</color></size>";
            _stamina.anchorMax = new Vector2(Mathf.Clamp01(f.StaminaRatio), 1f);
            _staminaImage.color = f.IsDown ? UiKit.Gray : c;
            _staminaText.text = "스태미나 " + Mathf.CeilToInt(Mathf.Max(0f, f.Stamina)) + " / " + Mathf.RoundToInt(f.MaxStamina);
            _body.anchorMax = new Vector2(Mathf.Clamp01(f.BodyRatio), 1f);
            _bodyText.text = "바디 " + Mathf.RoundToInt(f.BodyRatio * 100f) + "%";
            _special.text = f.SpecialReady ? "필살!" : f.SpecialCount.ToString();
            _special.fontSize = f.SpecialReady ? 22 : 34;
            _specialBack.color = f.SpecialReady ? new Color32(0xD8, 0x8A, 0x10, 0xFF) : new Color(0f, 0f, 0f, 0.6f);
            string state = "";
            if (f.IsDown) state = "DOWN · 기상까지 " + Mathf.RoundToInt(Mathf.Clamp01(f.StaminaRatio / _wakeRatio) * 100f) + "%";
            else if (f.Fallen > 0) state = "링 밖으로 떨어짐 · " + f.Fallen + "턴";
            else if (f.TagLock > 0 && !f.InRing) state = "태그 잠금 · " + f.TagLock + "턴";
            else if (taggable) state = "▶ 눌러서 태그";
            _state.text = state;
            _state.color = f.IsDown ? UiKit.Red : taggable ? UiKit.Yellow : UiKit.TextDim;
            _back.color = acting ? new Color32(0x4A, 0x44, 0x2C, 0xFF) : f.InRing ? new Color32(0x34, 0x37, 0x42, 0xFF) : new Color32(0x28, 0x2A, 0x31, 0xFF);
            _outline.effectColor = new Color(1f, 0.85f, 0.3f, taggable ? 0.9f : 0f);
            if (_button != null) _button.interactable = taggable;
        }

        /// <summary>Mid-shot bar update (a hit landed).</summary>
        public void ShowStamina(float stamina, float body)
        {
            if (_fighter == null) return;
            _stamina.anchorMax = new Vector2(Mathf.Clamp01(stamina / _fighter.MaxStamina), 1f);
            _body.anchorMax = new Vector2(Mathf.Clamp01(body / _fighter.MaxBody), 1f);
            _staminaText.text = "스태미나 " + Mathf.CeilToInt(Mathf.Max(0f, stamina)) + " / " + Mathf.RoundToInt(_fighter.MaxStamina);
            _bodyText.text = "바디 " + Mathf.RoundToInt(body / _fighter.MaxBody * 100f) + "%";
        }

        void Update()
        {
            if (_fighter == null || !_fighter.SpecialReady) return;
            _pulse += Time.unscaledDeltaTime * 5f;
            _specialBack.rectTransform.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(_pulse));
        }
    }
}
