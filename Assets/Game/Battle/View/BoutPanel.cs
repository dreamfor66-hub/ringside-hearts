using System;
using System.Collections.Generic;
using RingsideHearts.Battle.Core;
using UnityEngine;
using UnityEngine.UI;

namespace RingsideHearts.Battle.View
{
    /// <summary>
    /// The finale screen in the bottom zone. Reads without text: two big hand pads that light up in
    /// the rhythm the bout wants (alternate ⇄ or together ＋), a live picture of the bout, and
    /// situation chips. During the intro nothing moves: the pads demonstrate the input and a
    /// countdown runs, so everyone gets the same time to read the scene and nobody can act early.
    /// </summary>
    public sealed class BoutPanel : MonoBehaviour
    {
        Text _title;
        Text _chips;
        RectTransform _area;
        RectTransform _barA;
        Text _barALabel;
        RectTransform _barB;
        Text _barBLabel;
        RectTransform _mark;
        RectTransform _ringOuter;
        Image _ringOuterImage;
        RectTransform _ringTarget;
        RectTransform _needleTrack;
        RectTransform _needle;
        Image _zoneIn;
        Image _zoneOut;
        Text _info;
        Image _padL;
        Image _padR;
        Text _connector;
        Text _patternLabel;
        Image _introShade;
        Text _introText;
        Button _intervene;
        Text _intervention;

        float _flashL, _flashR;
        Color _flashColorL, _flashColorR;
        Hand? _lastHand;
        bool _alternate;
        bool _humanAttacker;
        bool _wasIntro;
        float _goFlash;

        public event Action InterveneClicked;

        public static BoutPanel Create(RectTransform bottom)
        {
            Image bg = UiKit.MakeImage(bottom, "BoutPanel", UiKit.Square, new Color32(0x1E, 0x20, 0x27, 0xFF));
            bg.raycastTarget = true;
            UiKit.Stretch(bg.rectTransform);
            var p = bg.gameObject.AddComponent<BoutPanel>();
            RectTransform rt = bg.rectTransform;

            p._title = UiKit.MakeText(rt, "Title", 44, TextAnchor.MiddleCenter, UiKit.Yellow);
            UiKit.TopRow(p._title.rectTransform, 6f, 56f);
            p._chips = UiKit.MakeText(rt, "Chips", 24, TextAnchor.MiddleCenter, UiKit.TextMain);
            UiKit.TopRow(p._chips.rectTransform, 62f, 34f);
            p._chips.supportRichText = true;

            p._area = UiKit.MakeRect(rt, "Area");
            UiKit.TopRow(p._area, 100f, 200f);
            p._barA = Bar(p._area, "A", 44f, -20f, UiKit.Red, out p._barALabel);
            p._barB = Bar(p._area, "B", 44f, -84f, UiKit.Blue, out p._barBLabel);
            Image mark = UiKit.MakeImage(p._area, "Mark", UiKit.Square, Color.white);
            p._mark = mark.rectTransform;
            p._mark.anchorMin = p._mark.anchorMax = new Vector2(0.5f, 1f);
            p._mark.sizeDelta = new Vector2(8f, 64f);
            p._ringTarget = UiKit.MakeImage(p._area, "Target", IconKit.Ring, new Color(1f, 0.85f, 0.3f, 0.9f)).rectTransform;
            UiKit.Center(p._ringTarget, new Vector2(130f, 130f), new Vector2(0f, -10f));
            p._ringOuterImage = UiKit.MakeImage(p._area, "Outer", IconKit.Ring, new Color(1f, 1f, 1f, 0.5f));
            p._ringOuter = p._ringOuterImage.rectTransform;
            UiKit.Center(p._ringOuter, new Vector2(300f, 300f), new Vector2(0f, -10f));
            p._needleTrack = UiKit.MakeImage(p._area, "Track", UiKit.Square, new Color(0f, 0f, 0f, 0.55f)).rectTransform;
            UiKit.Center(p._needleTrack, new Vector2(880f, 40f), new Vector2(0f, -20f));
            p._zoneIn = Zone(p._needleTrack, 0f, 0.13f, new Color(0.3f, 0.55f, 0.95f, 0.75f));
            p._zoneOut = Zone(p._needleTrack, 0.87f, 1f, new Color(0.95f, 0.35f, 0.3f, 0.75f));
            p._needle = UiKit.MakeImage(p._needleTrack, "Needle", UiKit.Square, Color.white).rectTransform;
            p._needle.anchorMin = p._needle.anchorMax = new Vector2(0.5f, 0.5f);
            p._needle.sizeDelta = new Vector2(12f, 72f);

            p._info = UiKit.MakeText(rt, "Info", 48, TextAnchor.MiddleCenter, UiKit.TextMain);
            UiKit.TopRow(p._info.rectTransform, 300f, 64f);
            p._info.gameObject.AddComponent<Outline>().effectColor = Color.black;

            p._padL = Pad(rt, "왼손", "왼클릭 · A", new Vector2(0f, 0f), new Vector2(30f, 20f));
            p._padR = Pad(rt, "오른손", "오른클릭 · D", new Vector2(1f, 0f), new Vector2(-30f, 20f));
            p._connector = UiKit.MakeText(rt, "Connector", 88, TextAnchor.MiddleCenter, Color.white);
            p._connector.rectTransform.anchorMin = p._connector.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            p._connector.rectTransform.sizeDelta = new Vector2(300f, 110f);
            p._connector.rectTransform.anchoredPosition = new Vector2(0f, 150f);
            p._patternLabel = UiKit.MakeText(rt, "Pattern", 30, TextAnchor.MiddleCenter, UiKit.Yellow);
            p._patternLabel.rectTransform.anchorMin = p._patternLabel.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            p._patternLabel.rectTransform.sizeDelta = new Vector2(320f, 40f);
            p._patternLabel.rectTransform.anchoredPosition = new Vector2(0f, 84f);

            p._introShade = UiKit.MakeImage(p._area, "IntroShade", UiKit.Square, new Color(0f, 0f, 0f, 0.55f));
            UiKit.Stretch(p._introShade.rectTransform, -40f, -40f, -40f, -40f);
            p._introText = UiKit.MakeText(p._introShade.rectTransform, "Intro", 70, TextAnchor.MiddleCenter, Color.white);
            UiKit.Stretch(p._introText.rectTransform);
            p._introText.gameObject.AddComponent<Outline>().effectColor = Color.black;

            p._intervene = UiKit.MakeButton(rt, "Intervene", "세컨드 개입", new Vector2(240f, 64f));
            RectTransform ir = (RectTransform)p._intervene.transform;
            ir.anchorMin = ir.anchorMax = new Vector2(1f, 1f);
            ir.pivot = new Vector2(1f, 1f);
            ir.anchoredPosition = new Vector2(-16f, -6f);
            p._intervene.onClick.AddListener(() => p.InterveneClicked?.Invoke());
            p._intervention = UiKit.MakeText(rt, "Intervention", 28, TextAnchor.MiddleCenter, UiKit.Yellow);
            UiKit.TopRow(p._intervention.rectTransform, 266f, 36f);
            p.gameObject.SetActive(false);
            return p;
        }

        static RectTransform Bar(RectTransform parent, string name, float height, float y, Color color, out Text label)
        {
            Image back = UiKit.MakeImage(parent, name + "Back", UiKit.Square, new Color(0f, 0f, 0f, 0.55f));
            back.rectTransform.anchorMin = back.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            back.rectTransform.pivot = new Vector2(0.5f, 1f);
            back.rectTransform.sizeDelta = new Vector2(880f, height);
            back.rectTransform.anchoredPosition = new Vector2(0f, y);
            Image fill = UiKit.MakeImage(back.rectTransform, name, UiKit.Square, color);
            RectTransform f = fill.rectTransform;
            f.anchorMin = Vector2.zero;
            f.anchorMax = new Vector2(0.5f, 1f);
            f.offsetMin = f.offsetMax = Vector2.zero;
            label = UiKit.MakeText(back.rectTransform, name + "Label", 24, TextAnchor.MiddleLeft, UiKit.TextMain);
            UiKit.Stretch(label.rectTransform, 12f, 0f, 12f, 0f);
            label.gameObject.AddComponent<Outline>().effectColor = Color.black;
            return f;
        }

        static Image Zone(RectTransform track, float from, float to, Color c)
        {
            Image z = UiKit.MakeImage(track, "Zone", UiKit.Square, c);
            z.rectTransform.anchorMin = new Vector2(from, 0f);
            z.rectTransform.anchorMax = new Vector2(to, 1f);
            z.rectTransform.offsetMin = z.rectTransform.offsetMax = Vector2.zero;
            return z;
        }

        static Image Pad(RectTransform parent, string title, string keys, Vector2 corner, Vector2 offset)
        {
            Image img = UiKit.MakeImage(parent, "Pad", UiKit.Square, new Color(1f, 1f, 1f, 0.1f));
            img.rectTransform.anchorMin = img.rectTransform.anchorMax = corner;
            img.rectTransform.pivot = corner;
            img.rectTransform.sizeDelta = new Vector2(300f, 200f);
            img.rectTransform.anchoredPosition = offset;
            Image icon = UiKit.MakeImage(img.rectTransform, "Icon", IconKit.Hand, new Color(1f, 1f, 1f, 0.85f));
            UiKit.Center(icon.rectTransform, new Vector2(90f, 90f), new Vector2(0f, 30f));
            if (corner.x > 0.5f) icon.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            Text t = UiKit.MakeText(img.rectTransform, "Title", 34, TextAnchor.LowerCenter, UiKit.TextMain);
            UiKit.Stretch(t.rectTransform, 0f, 44f, 0f, 0f);
            t.text = title;
            Text k = UiKit.MakeText(img.rectTransform, "Keys", 22, TextAnchor.LowerCenter, UiKit.TextDim);
            UiKit.Stretch(k.rectTransform, 0f, 12f, 0f, 0f);
            k.text = keys;
            return img;
        }

        /// <param name="chips">Situation notes shown under the title (near rope, body, special…).</param>
        public void Open(FinaleBout b, IList<string> chips)
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            _humanAttacker = b.AttackerHuman;
            bool def = b.DefenderHuman;
            bool timing = (b.Kind == FinalePlan.Pinfall && def) || b.Kind == FinalePlan.Ringout;
            _alternate = !timing;
            switch (b.Kind)
            {
                case FinalePlan.Pinfall: _title.text = def ? "핀폴 당했다! 킥아웃하라!" : "핀폴!"; break;
                case FinalePlan.Submission: _title.text = def ? "서브미션! 로프로 기어가라!" : "서브미션! 조여라!"; break;
                default: _title.text = def ? "링아웃 위기! 버텨라!" : "링아웃! 밀어 넘겨라!"; break;
            }
            _chips.text = string.Join("   ", chips);
            bool human = b.HasHuman;
            _padL.gameObject.SetActive(human);
            _padR.gameObject.SetActive(human);
            _connector.gameObject.SetActive(human);
            _patternLabel.gameObject.SetActive(human);
            _connector.text = _alternate ? "⇄" : "＋";
            _patternLabel.text = _alternate ? "번갈아 연타!" : b.Kind == FinalePlan.Ringout ? (def ? "파란 끝에서 동시에!" : "빨간 끝에서 동시에!") : "원이 겹칠 때 동시에!";
            _lastHand = null;
            _wasIntro = b.InIntro;
            _info.color = UiKit.TextMain;
            Refresh(b);
        }

        public void Pressed(Hand h, bool intro)
        {
            if (intro) return;
            bool ok = !_alternate || _lastHand != h;
            Color c = ok ? new Color(0.35f, 0.95f, 0.45f, 0.7f) : new Color(1f, 0.3f, 0.3f, 0.7f);
            if (h == Hand.Left) { _flashL = 0.14f; _flashColorL = c; }
            else { _flashR = 0.14f; _flashColorR = c; }
            _lastHand = h;
        }

        public void Refresh(FinaleBout b)
        {
            bool pin = b.Kind == FinalePlan.Pinfall;
            bool timingPin = pin && b.DefenderHuman;
            bool sub = b.Kind == FinalePlan.Submission;
            bool ring = b.Kind == FinalePlan.Ringout;
            _barA.parent.gameObject.SetActive((pin && !timingPin) || sub || ring);
            _barB.parent.gameObject.SetActive(sub);
            _mark.gameObject.SetActive(pin && !timingPin);
            _ringOuter.gameObject.SetActive(timingPin);
            _ringTarget.gameObject.SetActive(timingPin);
            _needleTrack.gameObject.SetActive(ring);
            bool windowNow = false;
            float demoTime = Time.unscaledTime;

            if (b is PinfallBout p)
            {
                string counts = "";
                for (int i = 1; i <= p.Count; i++) counts += i + "… ";
                if (timingPin)
                {
                    float t = b.InIntro ? Mathf.Repeat(demoTime, 1f) - 0.6f : p.TimeToWindow;
                    float scale = 1f + Mathf.Clamp(t, 0f, 0.8f) * 2.5f;
                    _ringOuter.sizeDelta = new Vector2(130f * scale, 130f * scale);
                    windowNow = Mathf.Abs(t) <= (b.InIntro ? 0.08f : p.Window);
                    _ringOuterImage.color = windowNow ? new Color(0.4f, 1f, 0.5f, 1f) : new Color(1f, 1f, 1f, 0.5f);
                    _info.text = b.InIntro ? "" : !p.RefCounting ? "심판이 달려온다…" : counts.Length > 0 ? counts : "카운트!";
                }
                else
                {
                    _barA.anchorMax = new Vector2(p.Gauge / 100f, 1f);
                    _barALabel.text = "누르기 — 흰 줄 위에 있는 동안 심판 카운트";
                    _mark.anchoredPosition = new Vector2(-440f + 880f * p.Threshold / 100f, -42f);
                    _info.text = b.InIntro ? "" : !p.RefCounting ? "심판이 달려온다…" : counts.Length > 0 ? counts : p.Gauge >= p.Threshold ? "카운트 중!" : "흰 줄 위로!";
                }
            }
            else if (b is SubmissionBout s)
            {
                _barA.anchorMax = new Vector2(s.Squeeze / 100f, 1f);
                _barALabel.text = "조이기 → 100이면 기브업 (" + b.Attacker.Short + ")";
                _barB.anchorMax = new Vector2(s.Crawl / 100f, 1f);
                _barBLabel.text = s.RopeBreak ? "로프까지 → 100이면 탈출 (" + b.Defender.Short + ")" : "로프 브레이크 없음 — 시간만 버티면 탈출";
                _info.text = b.InIntro ? "" : "남은 시간 " + Mathf.Max(0f, b.TimeLimit - b.Elapsed).ToString("0.0") + "초";
            }
            else if (b is RingoutBout r)
            {
                _barA.anchorMax = new Vector2(r.Over / 100f, 1f);
                _barALabel.text = "넘김 → 100이면 링아웃";
                float needle = b.InIntro ? Mathf.Sin(demoTime * Mathf.PI * 2f / r.Period) : r.Needle;
                _needle.anchoredPosition = new Vector2(needle * 440f, 0f);
                bool mineOuter = _humanAttacker;
                windowNow = mineOuter ? needle > 0.93f : needle < -0.93f;
                _zoneOut.color = new Color(0.95f, 0.35f, 0.3f, mineOuter && windowNow ? 1f : 0.6f);
                _zoneIn.color = new Color(0.3f, 0.55f, 0.95f, !mineOuter && windowNow ? 1f : 0.6f);
                _info.text = b.InIntro ? "" : "남은 시간 " + Mathf.Max(0f, b.TimeLimit - b.Elapsed).ToString("0.0") + "초";
            }

            // pads: show the rhythm (demo during intro, guide during play)
            float dt = Time.unscaledDeltaTime;
            _flashL -= dt;
            _flashR -= dt;
            float baseL = 0.1f, baseR = 0.1f;
            if (_alternate)
            {
                bool leftBeat = Mathf.Repeat(demoTime * 3.5f, 1f) < 0.5f;
                if (b.InIntro || _lastHand == null) { baseL = leftBeat ? 0.45f : 0.1f; baseR = leftBeat ? 0.1f : 0.45f; }
                else { baseL = _lastHand == Hand.Right ? 0.35f : 0.1f; baseR = _lastHand == Hand.Left ? 0.35f : 0.1f; }
            }
            else if (windowNow)
            {
                baseL = baseR = 0.55f;
            }
            _padL.color = _flashL > 0f ? _flashColorL : new Color(1f, 1f, 1f, baseL);
            _padR.color = _flashR > 0f ? _flashColorR : new Color(1f, 1f, 1f, baseR);
            _connector.color = !_alternate && windowNow ? new Color(0.4f, 1f, 0.5f) : Color.white;

            // intro: frozen scene + countdown, then a GO flash
            _introShade.gameObject.SetActive(b.InIntro || _goFlash > 0f);
            if (b.InIntro)
            {
                _introText.text = "준비 " + Mathf.CeilToInt(b.IntroLeft);
                _introText.color = Color.white;
                _introShade.color = new Color(0f, 0f, 0f, 0.55f);
            }
            else if (_wasIntro)
            {
                _wasIntro = false;
                _goFlash = 0.45f;
            }
            if (_goFlash > 0f)
            {
                _goFlash -= dt;
                _introText.text = "시작!";
                _introText.color = UiKit.Yellow;
                _introShade.color = new Color(0f, 0f, 0f, 0.3f * Mathf.Clamp01(_goFlash / 0.45f));
            }

            bool canIntervene = b.AttackerHuman ? b.AttackerCanIntervene : b.DefenderHuman && b.DefenderCanIntervene;
            _intervene.gameObject.SetActive(canIntervene && !b.InIntro && b.Result == MashResult.Running);
            _intervention.text = b.LastIntervention;
        }

        public void ShowResult(string text, bool good)
        {
            _info.text = text;
            _info.color = good ? UiKit.Yellow : UiKit.Red;
            _info.fontSize = 72;
        }

        public void Close()
        {
            _info.fontSize = 48;
            gameObject.SetActive(false);
        }
    }
}
