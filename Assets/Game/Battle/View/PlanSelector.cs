using System;
using RingsideHearts.Battle.Core;
using UnityEngine;
using UnityEngine.UI;

namespace RingsideHearts.Battle.View
{
    /// <summary>
    /// Bottom-zone plan row (design 10.2): three buttons with a one-line read of the situation,
    /// live only while an opponent in the ring is down. Plans the match forbids stay off.
    /// </summary>
    public sealed class PlanSelector : MonoBehaviour
    {
        static readonly string[] Names = { "핀폴", "서브미션", "링아웃" };

        readonly Button[] _buttons = new Button[3];
        readonly Image[] _backs = new Image[3];
        readonly Text[] _notes = new Text[3];
        Text _guide;

        public event Action<FinalePlan> Chosen;

        public static PlanSelector Create(RectTransform parent, float y, float height)
        {
            RectTransform rt = UiKit.MakeRect(parent, "PlanSelector");
            UiKit.TopRow(rt, y, height);
            var p = rt.gameObject.AddComponent<PlanSelector>();
            p._guide = UiKit.MakeText(rt, "Guide", 26, TextAnchor.MiddleCenter, UiKit.TextDim);
            UiKit.Stretch(p._guide.rectTransform);
            for (int i = 0; i < 3; i++)
            {
                Button b = UiKit.MakeButton(rt, "Plan_" + Names[i], Names[i], new Vector2(316f, height));
                RectTransform br = (RectTransform)b.transform;
                br.anchorMin = br.anchorMax = new Vector2(0.5f, 0.5f);
                br.anchoredPosition = new Vector2((i - 1) * 334f, 0f);
                Text label = b.GetComponentInChildren<Text>();
                label.alignment = TextAnchor.UpperCenter;
                label.rectTransform.offsetMax = new Vector2(0f, -10f);
                label.fontSize = 32;
                p._notes[i] = UiKit.MakeText(br, "Note", 21, TextAnchor.LowerCenter, UiKit.TextDim);
                UiKit.Stretch(p._notes[i].rectTransform, 6f, 8f, 6f, 0f);
                var plan = (FinalePlan)i;
                b.onClick.AddListener(() => p.Chosen?.Invoke(plan));
                p._buttons[i] = b;
                p._backs[i] = b.GetComponent<Image>();
            }
            return p;
        }

        public void Show(Match m, int team)
        {
            Fighter target = null;
            foreach (Fighter f in m.Teams[1 - team].InRing)
                if (f.IsDown) { target = f; break; }
            bool live = target != null && m.Phase != MatchPhase.Ended;
            _guide.enabled = !live;
            _guide.text = "상대가 링 안에서 다운되면 마무리 방식을 고릅니다 · 다음 샷이 닿는 순간 실행";
            FinalePlan selected = m.Teams[team].Plan;
            bool near = target != null && ShotResolver.IsNearRope(target.Position, m.Tuning);
            for (int i = 0; i < 3; i++)
            {
                var plan = (FinalePlan)i;
                bool allowed = m.PlanAllowed(plan);
                _buttons[i].gameObject.SetActive(live);
                _buttons[i].interactable = allowed;
                _backs[i].color = (int)selected == i ? new Color32(0x8A, 0x6A, 0x22, 0xFF) : new Color32(0x3E, 0x41, 0x4C, 0xFF);
            }
            if (!live) return;
            bool refDown = m.Field.RefereeDown > 0;
            _notes[0].text = !m.PlanAllowed(FinalePlan.Pinfall) ? "이 매치에선 불가" : refDown ? "심판 다운: 카운트 없음" : "심판 거리 " + (m.Field.Referee - target.Position).Length.ToString("0.0");
            _notes[1].text = near && !m.Rules.NoRopeBreak ? "로프 근처: 기어가기 60%부터" : "로프 멀다: 탈출 어려움";
            _notes[2].text = !m.PlanAllowed(FinalePlan.Ringout) ? "이 매치에선 불가" : near ? "로프 근처: 걸리기 쉬움" : "로프까지 세게 밀어야";
        }
    }
}
