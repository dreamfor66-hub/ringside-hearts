using System;
using System.Collections;
using System.Collections.Generic;
using RingsideHearts.Battle.Core;
using UnityEngine;
using UnityEngine.UI;

namespace RingsideHearts.Battle.View
{
    /// <summary>
    /// End of match: a slam-in WIN/LOSE banner, the finisher's awakened art with the finishing move,
    /// the MVP, and the match numbers as tiles. Again / match select.
    /// </summary>
    public sealed class ResultPanel : MonoBehaviour
    {
        public event Action Again;
        public event Action Menu;

        RectTransform _root;

        public static ResultPanel Create(RectTransform root)
        {
            Image bg = UiKit.MakeImage(root, "ResultPanel", UiKit.Square, new Color(0.05f, 0.05f, 0.07f, 0.97f));
            bg.raycastTarget = true;
            UiKit.Stretch(bg.rectTransform);
            var p = bg.gameObject.AddComponent<ResultPanel>();
            p._root = bg.rectTransform;
            p.gameObject.SetActive(false);
            return p;
        }

        /// <param name="finisher">Who landed the finale (either side), or null on a turn-limit loss.</param>
        public void Show(bool won, string headline, Fighter finisher, Fighter mvp, float mvpDamage, IList<KeyValuePair<string, string>> stats)
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            foreach (Transform c in _root) Destroy(c.gameObject);

            Color theme = won ? new Color32(0xF2, 0xB1, 0x2C, 0xFF) : new Color32(0x6E, 0x74, 0x90, 0xFF);
            Image band = UiKit.MakeImage(_root, "Band", UiKit.Square, new Color(theme.r, theme.g, theme.b, 0.9f));
            band.rectTransform.anchorMin = new Vector2(0f, 1f);
            band.rectTransform.anchorMax = new Vector2(1f, 1f);
            band.rectTransform.pivot = new Vector2(0.5f, 1f);
            band.rectTransform.sizeDelta = new Vector2(0f, 220f);
            band.rectTransform.anchoredPosition = new Vector2(0f, -60f);
            Text big = UiKit.MakeText(band.rectTransform, "Big", 150, TextAnchor.MiddleCenter, Color.white);
            UiKit.Stretch(big.rectTransform);
            big.text = won ? "WIN!" : "LOSE…";
            var o = big.gameObject.AddComponent<Outline>();
            o.effectColor = Color.black;
            o.effectDistance = new Vector2(5f, -5f);
            StartCoroutine(SlamIn(big.rectTransform));

            Text head = UiKit.MakeText(_root, "Headline", 44, TextAnchor.MiddleCenter, Color.white);
            UiKit.TopRow(head.rectTransform, 300f, 64f);
            head.text = headline;

            if (finisher != null)
            {
                RectTransform art = UiKit.MakeRect(_root, "FinisherArt");
                art.anchorMin = art.anchorMax = new Vector2(0f, 1f);
                art.pivot = new Vector2(0f, 1f);
                art.sizeDelta = new Vector2(560f, 760f);
                art.anchoredPosition = new Vector2(20f, -380f);
                CharacterArt.Place(art, finisher.Data, false);
                Text cap = UiKit.MakeText(_root, "FinisherName", 32, TextAnchor.MiddleCenter, theme);
                cap.rectTransform.anchorMin = cap.rectTransform.anchorMax = new Vector2(0f, 1f);
                cap.rectTransform.pivot = new Vector2(0f, 1f);
                cap.rectTransform.sizeDelta = new Vector2(560f, 50f);
                cap.rectTransform.anchoredPosition = new Vector2(20f, -1150f);
                cap.text = "피니시: " + finisher.Data.Name;
            }

            if (mvp != null)
            {
                Image mvpCard = UiKit.MakeImage(_root, "MVP", UiKit.Square, new Color32(0x2A, 0x2C, 0x35, 0xFF));
                mvpCard.rectTransform.anchorMin = mvpCard.rectTransform.anchorMax = new Vector2(1f, 1f);
                mvpCard.rectTransform.pivot = new Vector2(1f, 1f);
                mvpCard.rectTransform.sizeDelta = new Vector2(460f, 300f);
                mvpCard.rectTransform.anchoredPosition = new Vector2(-30f, -390f);
                RectTransform portrait = UiKit.MakeRect(mvpCard.rectTransform, "Art");
                portrait.anchorMin = portrait.anchorMax = new Vector2(0f, 0.5f);
                portrait.pivot = new Vector2(0f, 0.5f);
                portrait.sizeDelta = new Vector2(190f, 280f);
                portrait.anchoredPosition = new Vector2(8f, 0f);
                CharacterArt.Place(portrait, mvp.Data, false);
                Text t = UiKit.MakeText(mvpCard.rectTransform, "Text", 30, TextAnchor.MiddleLeft, Color.white);
                t.rectTransform.anchorMin = new Vector2(0f, 0f);
                t.rectTransform.anchorMax = new Vector2(1f, 1f);
                t.rectTransform.offsetMin = new Vector2(210f, 10f);
                t.rectTransform.offsetMax = new Vector2(-10f, -10f);
                t.supportRichText = true;
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                t.text = "<color=#F2B12C><size=40>MVP</size></color>\n" + mvp.Data.Name + "\n입힌 피해 " + Mathf.RoundToInt(mvpDamage);
            }

            // stat tiles (2 columns)
            for (int i = 0; i < stats.Count; i++)
            {
                Image tile = UiKit.MakeImage(_root, "Stat", UiKit.Square, new Color32(0x24, 0x26, 0x2E, 0xFF));
                tile.rectTransform.anchorMin = tile.rectTransform.anchorMax = new Vector2(1f, 1f);
                tile.rectTransform.pivot = new Vector2(1f, 1f);
                tile.rectTransform.sizeDelta = new Vector2(220f, 110f);
                int col = i % 2, row = i / 2;
                tile.rectTransform.anchoredPosition = new Vector2(-30f - (1 - col) * 240f, -720f - row * 124f);
                Text v = UiKit.MakeText(tile.rectTransform, "Value", 44, TextAnchor.MiddleCenter, Color.white);
                UiKit.Stretch(v.rectTransform, 0f, 30f, 0f, 0f);
                v.text = stats[i].Value;
                Text l = UiKit.MakeText(tile.rectTransform, "Label", 22, TextAnchor.LowerCenter, UiKit.TextDim);
                UiKit.Stretch(l.rectTransform, 0f, 8f, 0f, 0f);
                l.text = stats[i].Key;
            }

            Button again = UiKit.MakeButton(_root, "Again", "다시 하기", new Vector2(420f, 130f));
            RectTransform ar = (RectTransform)again.transform;
            ar.anchorMin = ar.anchorMax = new Vector2(0.5f, 0f);
            ar.anchoredPosition = new Vector2(-230f, 120f);
            again.onClick.AddListener(() => Again?.Invoke());
            Button menu = UiKit.MakeButton(_root, "Menu", "매치 선택", new Vector2(420f, 130f));
            RectTransform mr = (RectTransform)menu.transform;
            mr.anchorMin = mr.anchorMax = new Vector2(0.5f, 0f);
            mr.anchoredPosition = new Vector2(230f, 120f);
            menu.onClick.AddListener(() => Menu?.Invoke());
        }

        static IEnumerator SlamIn(RectTransform rt)
        {
            float t = 0f;
            while (t < 0.35f)
            {
                t += Time.unscaledDeltaTime;
                rt.localScale = Vector3.one * Mathf.Lerp(3f, 1f, Mathf.Clamp01(t / 0.25f));
                yield return null;
            }
            rt.localScale = Vector3.one;
        }
    }
}
