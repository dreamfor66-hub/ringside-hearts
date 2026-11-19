using RingsideHearts.Battle.Core;
using UnityEngine;
using UnityEngine.UI;

namespace RingsideHearts.Battle.View
{
    /// <summary>A team's crowd heat gauge (0/50/100 marks, current damage bonus) and tag chain slots.</summary>
    public sealed class TeamBar : MonoBehaviour
    {
        RectTransform _heatFill;
        Text _heatText;
        readonly Image[] _chain = new Image[3];
        Text _warnings;

        public static TeamBar Create(RectTransform parent, string name)
        {
            RectTransform rt = UiKit.MakeRect(parent, name);
            var bar = rt.gameObject.AddComponent<TeamBar>();
            Image back = UiKit.MakeImage(rt, "HeatBack", UiKit.Square, new Color(0f, 0f, 0f, 0.5f));
            back.rectTransform.anchorMin = new Vector2(0f, 0f);
            back.rectTransform.anchorMax = new Vector2(0.62f, 1f);
            back.rectTransform.offsetMin = new Vector2(0f, 6f);
            back.rectTransform.offsetMax = new Vector2(0f, -6f);
            Image fill = UiKit.MakeImage(back.rectTransform, "Heat", UiKit.Square, new Color32(0xE8, 0x6A, 0x3A, 0xFF));
            bar._heatFill = fill.rectTransform;
            bar._heatFill.anchorMin = Vector2.zero;
            bar._heatFill.anchorMax = new Vector2(0.5f, 1f);
            bar._heatFill.offsetMin = bar._heatFill.offsetMax = Vector2.zero;
            foreach (float mark in new[] { 0.5f })
            {
                Image m = UiKit.MakeImage(back.rectTransform, "Mark", UiKit.Square, new Color(1f, 1f, 1f, 0.6f));
                m.rectTransform.anchorMin = new Vector2(mark, 0f);
                m.rectTransform.anchorMax = new Vector2(mark, 1f);
                m.rectTransform.sizeDelta = new Vector2(3f, 0f);
            }
            bar._heatText = UiKit.MakeText(back.rectTransform, "HeatText", 22, TextAnchor.MiddleCenter, UiKit.TextMain);
            UiKit.Stretch(bar._heatText.rectTransform);
            bar._heatText.gameObject.AddComponent<Outline>().effectColor = Color.black;

            string[] roles = { "선봉", "연결", "마무리" };
            for (int i = 0; i < 3; i++)
            {
                Image slot = UiKit.MakeImage(rt, "Chain" + i, UiKit.Square, new Color(1f, 1f, 1f, 0.12f));
                slot.rectTransform.anchorMin = new Vector2(0.64f + i * 0.095f, 0f);
                slot.rectTransform.anchorMax = new Vector2(0.725f + i * 0.095f, 1f);
                slot.rectTransform.offsetMin = new Vector2(0f, 6f);
                slot.rectTransform.offsetMax = new Vector2(0f, -6f);
                Text t = UiKit.MakeText(slot.rectTransform, "Role", 20, TextAnchor.MiddleCenter, UiKit.TextMain);
                UiKit.Stretch(t.rectTransform);
                t.text = roles[i];
                bar._chain[i] = slot;
            }
            bar._warnings = UiKit.MakeText(rt, "Warnings", 20, TextAnchor.MiddleRight, UiKit.Red);
            bar._warnings.rectTransform.anchorMin = new Vector2(0.93f, 0f);
            bar._warnings.rectTransform.anchorMax = new Vector2(1f, 1f);
            bar._warnings.rectTransform.offsetMin = bar._warnings.rectTransform.offsetMax = Vector2.zero;
            return bar;
        }

        public void Refresh(TeamState team, Tuning t)
        {
            _heatFill.anchorMax = new Vector2(team.Heat / 100f, 1f);
            float span = t.Get("heat.damageSpan");
            int bonus = Mathf.RoundToInt((team.Heat - 50f) / 50f * span * 100f);
            float lo = t.Get("mash.heatMulMin");
            float hi = t.Get("mash.heatMulMax");
            float fin = lo + (hi - lo) * team.Heat / 100f;
            _heatText.text = "열기 " + Mathf.RoundToInt(team.Heat) + "  위력 " + (bonus >= 0 ? "+" : "") + bonus + "% · 피날레 ×" + fin.ToString("0.00");
            for (int i = 0; i < 3; i++)
                _chain[i].color = i < team.Chain ? new Color32(0xEF, 0x9F, 0x27, 0xFF) : new Color(1f, 1f, 1f, 0.12f);
            _warnings.text = team.Warnings > 0 ? "경고 " + team.Warnings : "";
        }
    }
}
