using System;
using System.Collections.Generic;
using System.Linq;
using RingsideHearts.Battle.Core;
using RingsideHearts.Battle.View;
using UnityEngine;
using UnityEngine.UI;

namespace RingsideHearts.Prototype
{
    /// <summary>
    /// Out-of-battle screens (design v0.2 9): match select, formation in the style of World Flipper
    /// (three tall columns with awakened art, stat bars and tappable icons), a character picker,
    /// and the VS screen that shows both rosters before the match.
    /// </summary>
    public sealed class MenuScreen : MonoBehaviour
    {
        RectTransform _root;
        RectTransform _page;
        Dictionary<string, CharacterData> _chars;
        Dictionary<string, TeamSetup> _teams;
        Dictionary<string, MatchRules> _matches;
        List<CharacterData> _playable;
        TeamSetup _team;

        public event Action<string> MatchChosen;

        static readonly string[] StatNames = { "스태미나", "파워", "근성" };

        public static MenuScreen Create(RectTransform root, Dictionary<string, MatchRules> matches, Dictionary<string, CharacterData> chars,
            Dictionary<string, TeamSetup> teams, TeamSetup team)
        {
            Image bg = UiKit.MakeImage(root, "Menu", UiKit.Square, UiKit.Background);
            bg.raycastTarget = true;
            UiKit.Stretch(bg.rectTransform);
            var m = bg.gameObject.AddComponent<MenuScreen>();
            m._root = bg.rectTransform;
            m._chars = chars;
            m._teams = teams;
            m._matches = matches;
            m._playable = chars.Values.Where(c => c.Playable).ToList();
            m._team = team;
            m.ShowSelect();
            return m;
        }

        RectTransform NewPage(string name)
        {
            if (_page != null) Destroy(_page.gameObject);
            _page = UiKit.MakeRect(_root, name);
            UiKit.Stretch(_page);
            return _page;
        }

        static Button Btn(RectTransform parent, string label, Vector2 size, Vector2 anchor, Vector2 pos, Action onClick, int font = 34)
        {
            Button b = UiKit.MakeButton(parent, label, label, size);
            RectTransform rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            b.GetComponentInChildren<Text>().fontSize = font;
            b.onClick.AddListener(() => onClick());
            return b;
        }

        // ---------------- match select ----------------

        void ShowSelect()
        {
            RectTransform page = NewPage("Select");
            Text title = UiKit.MakeText(page, "Title", 50, TextAnchor.MiddleCenter, UiKit.Yellow);
            UiKit.TopRow(title.rectTransform, 40f, 70f);
            title.text = "링사이드 하츠 · 전투 프로토타입";

            // my team strip
            RectTransform strip = UiKit.MakeRect(page, "Team");
            UiKit.TopRow(strip, 130f, 300f, 40f);
            for (int i = 0; i < _team.Slots.Length; i++)
            {
                CharacterData c = _chars[_team.Slots[i]];
                Image frame = UiKit.MakeImage(strip, "Slot", UiKit.Square, UiKit.AttributeColor(c.Attribute) * 0.6f);
                frame.rectTransform.anchorMin = frame.rectTransform.anchorMax = new Vector2(i / 3f + 1f / 6f, 0.5f);
                frame.rectTransform.sizeDelta = new Vector2(300f, 300f);
                frame.gameObject.AddComponent<RectMask2D>();
                RectTransform art = UiKit.MakeRect(frame.rectTransform, "Art");
                art.anchorMin = art.anchorMax = new Vector2(0.5f, 1f);
                art.pivot = new Vector2(0.5f, 1f);
                art.sizeDelta = new Vector2(320f, 460f);
                art.anchoredPosition = new Vector2(0f, 10f);
                CharacterArt.Place(art, c, false);
                Text n = UiKit.MakeText(frame.rectTransform, "Name", 28, TextAnchor.LowerCenter, Color.white);
                UiKit.Stretch(n.rectTransform, 0f, 8f, 0f, 0f);
                n.text = c.Short;
                n.gameObject.AddComponent<Outline>().effectColor = Color.black;
            }
            Btn(page, "편성 바꾸기", new Vector2(460f, 96f), new Vector2(0.5f, 1f), new Vector2(0f, -450f), ShowFormation);

            float y = 570f;
            foreach (MatchRules r in _matches.Values)
            {
                MatchRules rules = r;
                Button b = Btn(page, r.Name, new Vector2(980f, 136f), new Vector2(0.5f, 1f), new Vector2(0f, -y), () => ShowVs(rules.Id), 40);
                Text label = b.GetComponentInChildren<Text>();
                label.alignment = TextAnchor.UpperLeft;
                label.rectTransform.offsetMin = new Vector2(30f, 0f);
                label.rectTransform.offsetMax = new Vector2(0f, -16f);
                Text desc = UiKit.MakeText(b.transform, "Desc", 24, TextAnchor.LowerLeft, UiKit.TextDim);
                UiKit.Stretch(desc.rectTransform, 30f, 16f, 30f, 0f);
                desc.text = r.Description;
                TeamSetup enemy = _teams[r.EnemyTeam];
                Text vs = UiKit.MakeText(b.transform, "Enemy", 24, TextAnchor.UpperRight, UiKit.TextMain);
                UiKit.Stretch(vs.rectTransform, 0f, 0f, 30f, 18f);
                vs.text = "VS " + enemy.Name;
                y += 150f;
            }
        }

        // ---------------- formation ----------------

        void ShowFormation()
        {
            RectTransform page = NewPage("Formation");
            Text title = UiKit.MakeText(page, "Title", 44, TextAnchor.MiddleCenter, UiKit.Yellow);
            UiKit.TopRow(title.rectTransform, 30f, 60f);
            title.text = "편성 — 그림을 누르면 선수, 아래 칸을 누르면 세컨드 변경";
            title.fontSize = 34;
            for (int i = 0; i < 3; i++) Column(page, i);
            Btn(page, "완료", new Vector2(420f, 110f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), ShowSelect);
        }

        void Column(RectTransform page, int slot)
        {
            CharacterData c = _chars[_team.Slots[slot]];
            Color col = UiKit.AttributeColor(c.Attribute);
            Image colBg = UiKit.MakeImage(page, "Column" + slot, UiKit.Square, new Color(col.r * 0.35f, col.g * 0.35f, col.b * 0.35f, 1f));
            RectTransform rt = colBg.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(340f, 1360f);
            rt.anchoredPosition = new Vector2((slot - 1) * 352f, -110f);

            // slot label + role pips at the top
            Text slotLabel = UiKit.MakeText(rt, "Slot", 24, TextAnchor.UpperLeft, UiKit.TextDim);
            UiKit.Stretch(slotLabel.rectTransform, 14f, 0f, 0f, 8f);
            slotLabel.text = slot == 0 ? "1번 · 먼저 입장" : (slot + 1) + "번";

            // art (tap → fighter picker)
            Image artFrame = UiKit.MakeImage(rt, "ArtFrame", UiKit.Square, new Color(col.r, col.g, col.b, 0.25f));
            artFrame.raycastTarget = true;
            artFrame.rectTransform.anchorMin = new Vector2(0f, 1f);
            artFrame.rectTransform.anchorMax = new Vector2(1f, 1f);
            artFrame.rectTransform.pivot = new Vector2(0.5f, 1f);
            artFrame.rectTransform.sizeDelta = new Vector2(-12f, 640f);
            artFrame.rectTransform.anchoredPosition = new Vector2(0f, -40f);
            artFrame.gameObject.AddComponent<RectMask2D>();
            RectTransform art = UiKit.MakeRect(artFrame.rectTransform, "Art");
            UiKit.Stretch(art, -20f, -60f, -20f, 0f);
            CharacterArt.Place(art, c, false);
            var pick = artFrame.gameObject.AddComponent<Button>();
            pick.transition = Selectable.Transition.None;
            pick.onClick.AddListener(() => ShowPicker(slot, false));

            float y = 690f;
            Text name = UiKit.MakeText(rt, "Name", 34, TextAnchor.UpperLeft, Color.white);
            UiKit.TopRow(name.rectTransform, y, 44f, 14f);
            name.text = c.Name;
            Text grade = UiKit.MakeText(rt, "Grade", 26, TextAnchor.UpperRight, UiKit.Yellow);
            UiKit.TopRow(grade.rectTransform, y + 4f, 40f, 14f);
            grade.text = new string('★', c.Grade);
            y += 50f;

            // type + role + attribute
            RectTransform badges = UiKit.MakeRect(rt, "Badges");
            UiKit.TopRow(badges, y, 44f, 14f);
            RectTransform tb = IconKit.TypeBadge(badges, c.Type, 44f);
            tb.anchorMin = tb.anchorMax = tb.pivot = new Vector2(0f, 0.5f);
            Text tn = UiKit.MakeText(badges, "Type", 24, TextAnchor.MiddleLeft, Color.white);
            tn.rectTransform.anchorMin = tn.rectTransform.anchorMax = tn.rectTransform.pivot = new Vector2(0f, 0.5f);
            tn.rectTransform.sizeDelta = new Vector2(150f, 40f);
            tn.rectTransform.anchoredPosition = new Vector2(52f, 0f);
            tn.text = CharacterData.TypeNames[(int)c.Type];
            RectTransform pips = IconKit.RolePips(badges, c.Role, 18f);
            pips.anchorMin = pips.anchorMax = pips.pivot = new Vector2(1f, 0.5f);
            Text rn = UiKit.MakeText(badges, "Role", 20, TextAnchor.MiddleRight, UiKit.TextDim);
            rn.rectTransform.anchorMin = rn.rectTransform.anchorMax = rn.rectTransform.pivot = new Vector2(1f, 0.5f);
            rn.rectTransform.sizeDelta = new Vector2(80f, 30f);
            rn.rectTransform.anchoredPosition = new Vector2(-70f, 0f);
            rn.text = CharacterData.RoleNames[(int)c.Role];
            y += 54f;

            // attribute + faction chip line
            Text attr = UiKit.MakeText(rt, "Attr", 22, TextAnchor.UpperLeft, col);
            UiKit.TopRow(attr.rectTransform, y, 32f, 14f);
            attr.text = "● " + CharacterData.AttributeNames[(int)c.Attribute] + "   " + (c.Faction == Faction.Face ? "페이스" : "힐");
            y += 40f;

            // stat bars
            float[] values = { c.Stamina, c.Power, c.Guts };
            float[] max = { 1400f, 140f, 140f };
            for (int i = 0; i < 3; i++)
            {
                Text sl = UiKit.MakeText(rt, StatNames[i], 22, TextAnchor.MiddleLeft, UiKit.TextDim);
                UiKit.TopRow(sl.rectTransform, y, 30f, 14f);
                sl.text = StatNames[i];
                Image back = UiKit.MakeImage(rt, "Bar", UiKit.Square, new Color(0f, 0f, 0f, 0.5f));
                UiKit.TopRow(back.rectTransform, y + 4f, 22f, 0f);
                back.rectTransform.offsetMin = new Vector2(110f, back.rectTransform.offsetMin.y);
                back.rectTransform.offsetMax = new Vector2(-80f, back.rectTransform.offsetMax.y);
                Image fill = UiKit.MakeImage(back.rectTransform, "Fill", UiKit.Square, col);
                fill.rectTransform.anchorMin = Vector2.zero;
                fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(values[i] / max[i]), 1f);
                fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
                Text sv = UiKit.MakeText(rt, "Value", 24, TextAnchor.MiddleRight, Color.white);
                UiKit.TopRow(sv.rectTransform, y, 30f, 14f);
                sv.text = Mathf.RoundToInt(values[i]).ToString();
                y += 38f;
            }
            y += 6f;

            // icon row: special, assist, second skill (each opens a popup)
            CharacterData second = string.IsNullOrEmpty(_team.Seconds[slot]) ? null : _chars[_team.Seconds[slot]];
            IconButton(rt, IconKit.Bolt, UiKit.Yellow, new Vector2(14f, -y), "필살기", () => InfoPopup.Show(_root, IconKit.Bolt, UiKit.Yellow, c.SpecialName,
                $"위력 ×{c.SpecialMul} · 충전 {c.SpecialTurns}턴 (턴마다 1씩, 어필하면 1 더)\n발동: {(c.SpecialAtRope ? "샷이 처음 로프에 닿는 순간 (로프를 박차며 가속)" : "샷을 놓는 순간")}\n\n{TypeSpecial(c.Type)}"));
            IconButton(rt, IconKit.Hand, new Color(0.55f, 0.9f, 0.55f), new Vector2(118f, -y), CharacterData.AssistNames[(int)c.Assist], () => InfoPopup.Show(_root, IconKit.Hand, new Color(0.55f, 0.9f, 0.55f),
                "어시스트 · " + CharacterData.AssistNames[(int)c.Assist], AssistText(c.Assist)));
            IconButton(rt, IconKit.TypeIcon(c.Type), Color.white, new Vector2(222f, -y), "타입", () => InfoPopup.Show(_root, IconKit.TypeIcon(c.Type), Color.white,
                CharacterData.TypeNames[(int)c.Type], TypeText(c.Type)));
            y += 120f;

            // second slot (tap → second picker)
            Image secBox = UiKit.MakeImage(rt, "Second", UiKit.Square, new Color(0f, 0f, 0f, 0.35f));
            secBox.raycastTarget = true;
            UiKit.TopRow(secBox.rectTransform, y, 170f, 8f);
            var secBtn = secBox.gameObject.AddComponent<Button>();
            secBtn.transition = Selectable.Transition.None;
            secBtn.onClick.AddListener(() => ShowPicker(slot, true));
            Image secFrame = UiKit.MakeImage(secBox.rectTransform, "Portrait", UiKit.Square, second != null ? UiKit.AttributeColor(second.Attribute) * 0.6f : new Color(1f, 1f, 1f, 0.08f));
            secFrame.rectTransform.anchorMin = secFrame.rectTransform.anchorMax = secFrame.rectTransform.pivot = new Vector2(0f, 0.5f);
            secFrame.rectTransform.sizeDelta = new Vector2(150f, 150f);
            secFrame.rectTransform.anchoredPosition = new Vector2(10f, 0f);
            secFrame.gameObject.AddComponent<RectMask2D>();
            if (second != null)
            {
                RectTransform sArt = UiKit.MakeRect(secFrame.rectTransform, "Art");
                sArt.anchorMin = sArt.anchorMax = sArt.pivot = new Vector2(0.5f, 1f);
                sArt.sizeDelta = new Vector2(170f, 250f);
                CharacterArt.Place(sArt, second, false);
            }
            Text secText = UiKit.MakeText(secBox.rectTransform, "Text", 22, TextAnchor.UpperLeft, Color.white);
            UiKit.Stretch(secText.rectTransform, 170f, 8f, 8f, 8f);
            secText.horizontalOverflow = HorizontalWrapMode.Wrap;
            secText.supportRichText = true;
            secText.text = second == null ? "<color=#A8AAB2>세컨드</color>\n없음\n(눌러서 선택)"
                : "<color=#A8AAB2>세컨드</color>\n" + second.Short + "\n<size=18>능력치 +20%\n" + second.SecondText + "</size>";
        }

        void IconButton(RectTransform parent, Sprite icon, Color color, Vector2 pos, string label, Action onClick)
        {
            Image b = UiKit.MakeImage(parent, label, UiKit.Square, new Color(0f, 0f, 0f, 0.4f));
            b.raycastTarget = true;
            b.rectTransform.anchorMin = b.rectTransform.anchorMax = b.rectTransform.pivot = new Vector2(0f, 1f);
            b.rectTransform.sizeDelta = new Vector2(96f, 108f);
            b.rectTransform.anchoredPosition = pos;
            Image ic = UiKit.MakeImage(b.rectTransform, "Icon", icon, color);
            UiKit.Center(ic.rectTransform, new Vector2(60f, 60f), new Vector2(0f, 14f));
            Text t = UiKit.MakeText(b.rectTransform, "Label", 18, TextAnchor.LowerCenter, UiKit.TextMain);
            UiKit.Stretch(t.rectTransform, 0f, 6f, 0f, 0f);
            t.text = label;
            var btn = b.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick());
        }

        static string TypeText(FighterType t)
        {
            switch (t)
            {
                case FighterType.Striker: return "로프를 거치지 않고 바로 맞히면 위력 ×1.4 (직격), 맞은 상대가 1.6배 멀리 날아감.\n<color=#F2B12C>상대를 로프·코너로 날려 라리아트나 링아웃을 만든다.</color>";
                case FighterType.Showman: return "로프에 튕길 때마다 다음 타격 +30% (최대 3번, 필살기 5번), 튕길 때마다 열기 상승.\n<color=#F2B12C>로프를 여러 번 거쳐서 맞히면 크게 들어간다.</color>";
                case FighterType.Grappler: return "처음 닿은 상대를 붙잡아 링 정중앙으로 들어 내리꽂는다 (×1.2).\n<color=#F2B12C>로프 옆 상대를 중앙으로 끌어오면, 다운돼도 동료와 태그하지 못하고 서브미션에서도 로프로 도망치기 어렵다.</color>";
                case FighterType.Stampeder: return "상대를 뚫고 지나간다 (속도 90% 유지). 지나간 뒤 로프에 튕겨 다시 칠 수 있다.\n<color=#F2B12C>한 샷에 여러 번 타격.</color>";
                default: return "처음 로프를 박차면 3.5만큼 날아올라 그 사이 아무와도 부딪히지 않고, 착지 지점 반경 2 안의 상대를 모두 ×1.3으로 덮친다.\n<color=#F2B12C>앞을 막는 몸이나 심판을 넘어 착지점을 노린다.</color>";
            }
        }

        static string TypeSpecial(FighterType t)
        {
            switch (t)
            {
                case FighterType.Striker: return "필살기: 밀어내기 2.4배";
                case FighterType.Showman: return "필살기: 반동 보너스 5번까지";
                case FighterType.Grappler: return "필살기: 위력 배율만 적용";
                case FighterType.Stampeder: return "필살기: 뚫어도 속도가 안 줄고 감속 절반";
                default: return "필살기: 로프마다 날아오르고 착지 범위 1.5배";
            }
        }

        static string AssistText(AssistKind a)
        {
            switch (a)
            {
                case AssistKind.Lariat: return "에이프런의 이 선수를 내 샷이 치고 지나가면, 링 중앙 방향으로 직선 라리아트를 날려 그 선 위의 상대를 친다.";
                case AssistKind.Stomp: return "에이프런의 이 선수를 내 샷이 치고 지나가면, 닿은 지점 주변(반경 2.5)의 상대를 짓밟는다.";
                case AssistKind.Cheer: return "에이프런의 이 선수를 내 샷이 치고 지나가면, 관중을 달궈 열기 +10.";
                case AssistKind.Icing: return "에이프런의 이 선수를 내 샷이 치고 지나가면, 쏜 선수의 스태미나를 8% 회복시킨다.";
                default: return "어시스트 없음.";
            }
        }

        // ---------------- picker ----------------

        void ShowPicker(int slot, bool second)
        {
            Image shade = UiKit.MakeImage(_root, "Picker", UiKit.Square, new Color(0.04f, 0.04f, 0.06f, 0.96f));
            shade.raycastTarget = true;
            UiKit.Stretch(shade.rectTransform);
            Text title = UiKit.MakeText(shade.rectTransform, "Title", 40, TextAnchor.MiddleCenter, UiKit.Yellow);
            UiKit.TopRow(title.rectTransform, 30f, 60f);
            title.text = second ? (slot + 1) + "번 슬롯의 세컨드 선택" : (slot + 1) + "번 슬롯 선수 선택";
            var options = new List<CharacterData>(_playable);
            if (second) options.Insert(0, null);
            for (int i = 0; i < options.Count; i++)
            {
                CharacterData c = options[i];
                bool used = c != null && (_team.Slots.Where((s, j) => !(j == slot && !second)).Contains(c.Id) || _team.Seconds.Where((s, j) => !(j == slot && second)).Contains(c.Id));
                Image tile = UiKit.MakeImage(shade.rectTransform, "Tile", UiKit.Square, c != null ? UiKit.AttributeColor(c.Attribute) * 0.5f : new Color(1f, 1f, 1f, 0.08f));
                tile.raycastTarget = !used;
                tile.rectTransform.anchorMin = tile.rectTransform.anchorMax = tile.rectTransform.pivot = new Vector2(0f, 1f);
                tile.rectTransform.sizeDelta = new Vector2(250f, 300f);
                tile.rectTransform.anchoredPosition = new Vector2(20f + (i % 4) * 262f, -110f - (i / 4) * 312f);
                tile.gameObject.AddComponent<RectMask2D>();
                if (c != null)
                {
                    RectTransform art = UiKit.MakeRect(tile.rectTransform, "Art");
                    art.anchorMin = art.anchorMax = art.pivot = new Vector2(0.5f, 1f);
                    art.sizeDelta = new Vector2(270f, 380f);
                    CharacterArt.Place(art, c, false, used ? 0.3f : 1f);
                    RectTransform tb = IconKit.TypeBadge(tile.rectTransform, c.Type, 40f);
                    tb.anchorMin = tb.anchorMax = tb.pivot = new Vector2(0f, 1f);
                    tb.anchoredPosition = new Vector2(6f, -6f);
                }
                Image label = UiKit.MakeImage(tile.rectTransform, "Label", UiKit.Square, new Color(0f, 0f, 0f, 0.65f));
                label.rectTransform.anchorMin = new Vector2(0f, 0f);
                label.rectTransform.anchorMax = new Vector2(1f, 0f);
                label.rectTransform.pivot = new Vector2(0.5f, 0f);
                label.rectTransform.sizeDelta = new Vector2(0f, 64f);
                Text t = UiKit.MakeText(label.rectTransform, "Name", 22, TextAnchor.MiddleCenter, used ? UiKit.TextDim : Color.white);
                UiKit.Stretch(t.rectTransform);
                t.supportRichText = true;
                t.text = c == null ? "세컨드 없음" : c.Short + " ★" + c.Grade + "\n<size=17>" + CharacterData.TypeNames[(int)c.Type] + " · " + CharacterData.RoleNames[(int)c.Role] + (used ? " · 사용 중" : "") + "</size>";
                if (used) continue;
                var btn = tile.gameObject.AddComponent<Button>();
                string id = c?.Id ?? "";
                btn.onClick.AddListener(() =>
                {
                    if (second) _team.Seconds[slot] = id;
                    else _team.Slots[slot] = id;
                    Destroy(shade.gameObject);
                    ShowFormation();
                });
            }
            Btn(shade.rectTransform, "취소", new Vector2(360f, 100f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), () => Destroy(shade.gameObject));
        }

        // ---------------- VS ----------------

        void ShowVs(string matchId)
        {
            MatchRules rules = _matches[matchId];
            TeamSetup enemy = _teams[rules.EnemyTeam];
            RectTransform page = NewPage("Vs");
            Roster(page, enemy.Slots, true, enemy.Name, new Color32(0x7A, 0x24, 0x30, 0xFF));
            Roster(page, _team.Slots, false, "내 팀", new Color32(0x1F, 0x3E, 0x78, 0xFF));
            Text vs = UiKit.MakeText(page, "VS", 180, TextAnchor.MiddleCenter, Color.white);
            UiKit.Center(vs.rectTransform, new Vector2(500f, 220f), new Vector2(0f, 80f));
            vs.text = "VS";
            var o = vs.gameObject.AddComponent<Outline>();
            o.effectColor = Color.black;
            o.effectDistance = new Vector2(6f, -6f);
            Text info = UiKit.MakeText(page, "Info", 34, TextAnchor.MiddleCenter, UiKit.Yellow);
            UiKit.Center(info.rectTransform, new Vector2(1000f, 120f), new Vector2(0f, -110f));
            info.horizontalOverflow = HorizontalWrapMode.Wrap;
            info.text = rules.Name + "\n<size=24><color=#EDEDED>" + rules.Description + " · 턴 제한 " + rules.TurnLimit + "</color></size>";
            info.supportRichText = true;
            Btn(page, "시합 시작", new Vector2(460f, 120f), new Vector2(0.5f, 0f), new Vector2(-240f, 40f), () => MatchChosen?.Invoke(matchId), 40);
            Btn(page, "뒤로", new Vector2(300f, 120f), new Vector2(0.5f, 0f), new Vector2(240f, 40f), ShowSelect);
        }

        void Roster(RectTransform page, string[] ids, bool top, string name, Color band)
        {
            Image bg = UiKit.MakeImage(page, top ? "Enemy" : "Mine", UiKit.Square, band);
            bg.rectTransform.anchorMin = new Vector2(0f, top ? 0.56f : 0.1f);
            bg.rectTransform.anchorMax = new Vector2(1f, top ? 1f : 0.48f);
            bg.rectTransform.offsetMin = bg.rectTransform.offsetMax = Vector2.zero;
            bg.gameObject.AddComponent<RectMask2D>();
            for (int i = 0; i < ids.Length; i++)
            {
                CharacterData c = _chars[ids[i]];
                RectTransform area = UiKit.MakeRect(bg.rectTransform, "Art");
                area.anchorMin = new Vector2(i / (float)ids.Length, 0f);
                area.anchorMax = new Vector2((i + 1) / (float)ids.Length, 1f);
                area.offsetMin = new Vector2(4f, 50f);
                area.offsetMax = new Vector2(-4f, -10f);
                CharacterArt.Place(area, c, true);
                Text n = UiKit.MakeText(bg.rectTransform, "Name", 26, TextAnchor.LowerCenter, Color.white);
                n.rectTransform.anchorMin = new Vector2(i / (float)ids.Length, 0f);
                n.rectTransform.anchorMax = new Vector2((i + 1) / (float)ids.Length, 0f);
                n.rectTransform.pivot = new Vector2(0.5f, 0f);
                n.rectTransform.sizeDelta = new Vector2(0f, 46f);
                n.supportRichText = true;
                n.text = c.Short + " <size=18>" + CharacterData.TypeNames[(int)c.Type] + "</size>";
                n.gameObject.AddComponent<Outline>().effectColor = Color.black;
            }
            Text title = UiKit.MakeText(bg.rectTransform, "Team", 36, top ? TextAnchor.UpperLeft : TextAnchor.UpperRight, Color.white);
            UiKit.Stretch(title.rectTransform, 20f, 0f, 20f, 12f);
            title.text = name;
            title.gameObject.AddComponent<Outline>().effectColor = Color.black;
        }
    }
}
