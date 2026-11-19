using UnityEngine;
using UnityEngine.UI;

namespace RingsideHearts.Battle.View
{
    /// <summary>A modal card: icon, title, body. Tap anywhere to close.</summary>
    public static class InfoPopup
    {
        public static void Show(RectTransform root, Sprite icon, Color iconColor, string title, string body)
        {
            Image shade = UiKit.MakeImage(root, "Popup", UiKit.Square, new Color(0f, 0f, 0f, 0.7f));
            shade.raycastTarget = true;
            UiKit.Stretch(shade.rectTransform);
            var close = shade.gameObject.AddComponent<Button>();
            close.transition = Selectable.Transition.None;
            close.onClick.AddListener(() => Object.Destroy(shade.gameObject));

            Image card = UiKit.MakeImage(shade.rectTransform, "Card", UiKit.Square, new Color32(0x2E, 0x30, 0x3A, 0xFF));
            UiKit.Center(card.rectTransform, new Vector2(860f, 520f), Vector2.zero);
            Image ic = UiKit.MakeImage(card.rectTransform, "Icon", icon, iconColor);
            ic.rectTransform.anchorMin = ic.rectTransform.anchorMax = new Vector2(0f, 1f);
            ic.rectTransform.pivot = new Vector2(0f, 1f);
            ic.rectTransform.sizeDelta = new Vector2(96f, 96f);
            ic.rectTransform.anchoredPosition = new Vector2(36f, -36f);
            Text t = UiKit.MakeText(card.rectTransform, "Title", 44, TextAnchor.MiddleLeft, UiKit.Yellow);
            t.rectTransform.anchorMin = new Vector2(0f, 1f);
            t.rectTransform.anchorMax = new Vector2(1f, 1f);
            t.rectTransform.pivot = new Vector2(0f, 1f);
            t.rectTransform.offsetMin = new Vector2(150f, -132f);
            t.rectTransform.offsetMax = new Vector2(-30f, -36f);
            t.text = title;
            Text b = UiKit.MakeText(card.rectTransform, "Body", 30, TextAnchor.UpperLeft, UiKit.TextMain);
            UiKit.Stretch(b.rectTransform, 40f, 80f, 40f, 160f);
            b.horizontalOverflow = HorizontalWrapMode.Wrap;
            b.supportRichText = true;
            b.lineSpacing = 1.25f;
            b.text = body;
            Text hint = UiKit.MakeText(card.rectTransform, "Hint", 22, TextAnchor.LowerCenter, UiKit.TextDim);
            UiKit.Stretch(hint.rectTransform, 0f, 20f, 0f, 0f);
            hint.text = "아무 곳이나 눌러 닫기";
        }
    }
}
