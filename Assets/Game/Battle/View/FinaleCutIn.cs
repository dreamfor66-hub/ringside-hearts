using RingsideHearts.Battle.Core;
using UnityEngine;
using UnityEngine.UI;

namespace RingsideHearts.Battle.View
{
    /// <summary>
    /// Finale cut-in over the ring zone (design 10.3 [확정]): the two fighters overlap like the
    /// real hold instead of standing side by side. A figure is a body (rect) and a head (circle).
    /// The pose follows the gauge: pinfall — the defender's shoulders lift as the gauge drops;
    /// ringout — the defender leans further over the rope as the gauge rises.
    /// </summary>
    public sealed class FinaleCutIn : MonoBehaviour
    {
        RectTransform _stage;
        RectTransform _defender;
        RectTransform _attacker;
        Text _title;
        FinalePlan _kind;

        public static FinaleCutIn Create(RectTransform ringZone)
        {
            Image shade = UiKit.MakeImage(ringZone, "FinaleCutIn", UiKit.Square, new Color(0.08f, 0.08f, 0.1f, 0.92f));
            UiKit.Stretch(shade.rectTransform);
            var c = shade.gameObject.AddComponent<FinaleCutIn>();
            c._title = UiKit.MakeText(shade.rectTransform, "Title", 70, TextAnchor.MiddleCenter, UiKit.Yellow);
            UiKit.TopRow(c._title.rectTransform, 60f, 100f);
            c.gameObject.SetActive(false);
            return c;
        }

        public void Open(FinalePlan kind, string attackerName, Color attackerColor, string defenderName, Color defenderColor)
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            _kind = kind;
            if (_stage != null) Destroy(_stage.gameObject);
            _stage = UiKit.MakeRect(transform, "Stage");
            UiKit.Center(_stage, new Vector2(900f, 700f), new Vector2(0f, -60f));
            string[] titles = { "핀폴", "서브미션", "링아웃" };
            _title.text = titles[(int)kind] + " — " + attackerName + " → " + defenderName;

            if (kind == FinalePlan.Ringout)
            {
                Image rope = UiKit.MakeImage(_stage, "Rope", UiKit.Square, UiKit.Rope);
                UiKit.Center(rope.rectTransform, new Vector2(14f, 640f), new Vector2(230f, 0f));
            }

            // Defender first so the attacker is drawn on top of it.
            bool lying = kind != FinalePlan.Ringout;
            _defender = Figure("Defender", defenderName, defenderColor, lying, kind == FinalePlan.Submission);
            _attacker = Figure("Attacker", attackerName, attackerColor, lying, false);

            switch (kind)
            {
                case FinalePlan.Pinfall:
                    _defender.anchoredPosition = new Vector2(260f, -200f);
                    _attacker.anchoredPosition = new Vector2(300f, -105f);
                    _attacker.localRotation = Quaternion.Euler(0f, 0f, -6f);
                    break;
                case FinalePlan.Submission:
                    _defender.anchoredPosition = new Vector2(260f, -170f);
                    _attacker.anchoredPosition = new Vector2(300f, -40f);
                    _attacker.localRotation = Quaternion.Euler(0f, 0f, 22f);
                    Arm(_stage, new Vector2(-160f, -150f), 220f, 60f, attackerColor);
                    Arm(_stage, new Vector2(170f, -170f), 200f, -55f, attackerColor);
                    break;
                default:
                    _defender.anchoredPosition = new Vector2(150f, -300f);
                    _attacker.anchoredPosition = new Vector2(-80f, -310f);
                    _attacker.localRotation = Quaternion.Euler(0f, 0f, -16f);
                    break;
            }
            SetGauge(0.5f);
        }

        /// <summary>A body + head. Lying figures put the head on the left.</summary>
        RectTransform Figure(string name, string label, Color color, bool lying, bool prone)
        {
            RectTransform root = UiKit.MakeRect(_stage, name);
            UiKit.Center(root, new Vector2(10f, 10f), Vector2.zero);
            // the root sits at the feet, so a rotation reads as lifting (lying) or leaning (standing)
            Vector2 bodySize = lying ? new Vector2(420f, 120f) : new Vector2(130f, 380f);
            Vector2 bodyPos = lying ? new Vector2(-210f, 0f) : new Vector2(0f, 190f);
            Vector2 headPos = lying ? new Vector2(-470f, prone ? -10f : 10f) : new Vector2(0f, 450f);
            Image body = UiKit.MakeImage(root, "Body", UiKit.Square, color);
            UiKit.Center(body.rectTransform, bodySize, bodyPos);
            Image head = UiKit.MakeImage(root, "Head", UiKit.Circle, color);
            UiKit.Center(head.rectTransform, new Vector2(130f, 130f), headPos);
            Text text = UiKit.MakeText(body.rectTransform, "Name", 40, TextAnchor.MiddleCenter, new Color32(0x1A, 0x1A, 0x1A, 0xFF));
            UiKit.Stretch(text.rectTransform);
            text.text = label;
            return root;
        }

        static void Arm(RectTransform parent, Vector2 at, float length, float angle, Color color)
        {
            Image arm = UiKit.MakeImage(parent, "Arm", UiKit.Square, color * 0.8f);
            UiKit.Center(arm.rectTransform, new Vector2(length, 26f), at);
            arm.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        /// <param name="gauge01">Mash gauge 0..1 (1 = attacker side).</param>
        public void SetGauge(float gauge01)
        {
            if (_defender == null) return;
            switch (_kind)
            {
                case FinalePlan.Pinfall:
                    // shoulders (head side, left) lift as the defender pushes the gauge down
                    _defender.localRotation = Quaternion.Euler(0f, 0f, -(1f - gauge01) * 28f);
                    break;
                case FinalePlan.Ringout:
                    _defender.localRotation = Quaternion.Euler(0f, 0f, -(8f + gauge01 * 50f));
                    _defender.anchoredPosition = new Vector2(150f + gauge01 * 90f, -300f);
                    _attacker.anchoredPosition = new Vector2(-80f + gauge01 * 90f, -310f);
                    break;
            }
        }

        public void Close() => gameObject.SetActive(false);
    }
}
