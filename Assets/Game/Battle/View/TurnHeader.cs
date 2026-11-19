using UnityEngine;
using UnityEngine.UI;

namespace RingsideHearts.Battle.View
{
    /// <summary>Top zone, left: match name, turn "5 / 30", whose action it is.</summary>
    public sealed class TurnHeader : MonoBehaviour
    {
        Text _match;
        Text _turn;
        Text _who;

        public static TurnHeader Create(RectTransform parent)
        {
            RectTransform rt = UiKit.MakeRect(parent, "TurnHeader");
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(300f, 0f);
            rt.anchoredPosition = new Vector2(24f, 0f);
            var h = rt.gameObject.AddComponent<TurnHeader>();
            h._match = UiKit.MakeText(rt, "Match", 24, TextAnchor.UpperLeft, UiKit.TextDim);
            UiKit.TopRow(h._match.rectTransform, 14f, 34f, 0f);
            h._turn = UiKit.MakeText(rt, "Turn", 48, TextAnchor.UpperLeft, UiKit.TextMain);
            UiKit.TopRow(h._turn.rectTransform, 50f, 60f, 0f);
            h._who = UiKit.MakeText(rt, "Who", 26, TextAnchor.UpperLeft, UiKit.Yellow);
            UiKit.TopRow(h._who.rectTransform, 118f, 36f, 0f);
            return h;
        }

        public void Set(string matchName, int turn, int limit, string who)
        {
            _match.text = matchName;
            _turn.text = "턴 " + turn + " / " + limit;
            _who.text = who;
        }
    }
}
