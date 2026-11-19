using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RingsideHearts.Battle.Core;
using RingsideHearts.Battle.View;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RingsideHearts.Prototype
{
    /// <summary>
    /// Prototype v0.2: match select → formation → VS → 3:3 battle → result. Team 0 sits at the
    /// bottom of the screen, team 1 at the top; who is human comes only from team data (design 4.1).
    /// Presentation (cut-ins, hit-stop, shakes, lifts, finale staging) lives here and in BattleFx.
    /// </summary>
    public sealed class MatchPrototype : MonoBehaviour
    {
        const float AiThinkSeconds = 0.55f;
        const float BoutResultHold = 1.3f;
        const float CutInSeconds = 1.15f;

        GameVariables _vars;
        Dictionary<string, CharacterData> _chars;
        Dictionary<string, TeamSetup> _teams;
        Dictionary<string, MatchRules> _matches;
        Match _match;

        ScreenLayout _layout;
        RingView _ring;
        FieldView _field;
        BattleFx _fx;
        readonly Dictionary<Fighter, UnitView> _units = new Dictionary<Fighter, UnitView>();
        AimInput _aim;
        AimGuideView _guide;
        ShotPlayer _player;
        TurnHeader _header;
        readonly FighterCard[] _enemyCards = new FighterCard[2];
        TeamBar _enemyBar;
        TeamBar _myBar;
        PlanSelector _plans;
        readonly FighterCard[] _myCards = new FighterCard[3];
        Button _btnSpecial;
        Text _btnSpecialLabel;
        Button _btnAppeal;
        Text _btnAppealLabel;
        Button _btnHold;
        Text _status;
        BoutPanel _boutPanel;
        FinaleCutIn _cutIn;
        ResultPanel _result;
        RectTransform _debug;
        RectTransform _logPanel;
        Text _logText;

        ShotOutcome _current;
        Fighter _shooter;
        int _shownHits;
        int _shownEvents;
        int _ropeLabel;
        bool _ropeCutInPending;
        float _afterimageAt;
        readonly List<(UnitView unit, float from, float to)> _lifts = new List<(UnitView, float, float)>();
        readonly Dictionary<Fighter, Vector2> _live = new Dictionary<Fighter, Vector2>();
        readonly Dictionary<Fighter, float> _dealt = new Dictionary<Fighter, float>();
        FinaleBout _bout;
        Fighter _lastFinaleAttacker;
        float _boutDoneAt = -1f;
        bool _busy;
        bool _specialArmed;
        int _lastActingTeam = -1;
        float _startTime;
        readonly List<Hand> _hands = new List<Hand>();
        readonly List<Vec2> _ropeProbe = new List<Vec2>();
        readonly List<string> _log = new List<string>();

        public Match Match => _match;
        public FinaleBout Bout => _bout;
        public bool Busy => _busy;

        void Start()
        {
            _vars = GameVariables.Load();
            _chars = CharacterData.ParseAll(Resources.Load<TextAsset>("Data/characters").text);
            _teams = TeamSetup.ParseAll(Resources.Load<TextAsset>("Data/teams").text);
            _matches = MatchRules.ParseAll(Resources.Load<TextAsset>("Data/matches").text);
            EnsureEventSystem();
            _layout = ScreenLayout.Create();
            if (PrototypeSession.Formation == null) PrototypeSession.Formation = _teams["player_default"].Clone();

            if (string.IsNullOrEmpty(PrototypeSession.PendingMatch))
            {
                MenuScreen menu = MenuScreen.Create(_layout.Safe, _matches, _chars, _teams, PrototypeSession.Formation);
                menu.MatchChosen += id =>
                {
                    PrototypeSession.PendingMatch = id;
                    SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                };
                return;
            }
            string matchId = PrototypeSession.PendingMatch;
            PrototypeSession.PendingMatch = null;
            StartMatch(matchId);
        }

        void StartMatch(string id)
        {
            MatchRules rules = _matches[id];
            int seed = PrototypeSession.FixedSeed != 0 ? PrototypeSession.FixedSeed : Environment.TickCount & 0x7fffffff;
            PrototypeSession.LastSeed = seed;
            TeamSetup me = PrototypeSession.Formation.Clone();
            me.Control = Control.Human;
            _match = new Match(rules, me, _teams[rules.EnemyTeam], _chars, _vars.ToTuning(), seed);
            _startTime = Time.time;
            BuildBattle();
            Log($"[{rules.Name}] 시작 · 시드 {seed}");
            BeginAction();
        }

        // ================= screen =================

        void BuildBattle()
        {
            Tuning t = _match.Tuning;
            _ring = new GameObject("RingView", typeof(RectTransform)).AddComponent<RingView>();
            _ring.Build(_layout.Ring, t);
            _field = FieldView.Create(_ring, _match);
            foreach (Fighter f in _match.AllFighters())
                _units[f] = UnitView.Create(_ring, f, t.Get("unit.radius"));
            _guide = AimGuideView.Create(_ring);
            _cutIn = FinaleCutIn.Create(_layout.Ring);
            _fx = BattleFx.Create(_layout.Safe, _layout.Ring, _ring);

            _player = gameObject.AddComponent<ShotPlayer>();
            _player.EventReached += OnShotEvent;
            _player.Finished += OnShotFinished;
            _aim = gameObject.AddComponent<AimInput>();
            _aim.Init(_layout.Canvas);
            _aim.PullChanged += OnPull;
            _aim.Released += OnRelease;
            _aim.Cancelled += () => _guide.Hide();
            _aim.Locked = true;

            _header = TurnHeader.Create(_layout.Top);
            for (int i = 0; i < 2; i++)
            {
                _enemyCards[i] = FighterCard.Create(_layout.Top, "EnemyCard" + i, false);
                RectTransform rt = (RectTransform)_enemyCards[i].transform;
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 1f);
                rt.sizeDelta = new Vector2(740f, 100f);
                rt.anchoredPosition = new Vector2(-12f, -8f - i * 104f);
                _enemyCards[i].Bind(null, _vars.downWakeRatio);
            }
            _enemyBar = TeamBar.Create(_layout.Top, "EnemyBar");
            RectTransform eb = (RectTransform)_enemyBar.transform;
            eb.anchorMin = eb.anchorMax = new Vector2(1f, 0f);
            eb.pivot = new Vector2(1f, 0f);
            eb.sizeDelta = new Vector2(740f, 34f);
            eb.anchoredPosition = new Vector2(-12f, 4f);
            SmallButton(_layout.Top, "로그", new Vector2(20f, 8f), ToggleLog);
            SmallButton(_layout.Top, "디버그", new Vector2(146f, 8f), ToggleDebug);

            RectTransform bottom = _layout.Bottom;
            _status = UiKit.MakeText(bottom, "Status", 26, TextAnchor.MiddleCenter, UiKit.TextMain);
            UiKit.TopRow(_status.rectTransform, 4f, 38f, 20f);
            _status.horizontalOverflow = HorizontalWrapMode.Wrap;
            _myBar = TeamBar.Create(bottom, "MyBar");
            UiKit.TopRow((RectTransform)_myBar.transform, 46f, 34f, 20f);
            _plans = PlanSelector.Create(bottom, 86f, 80f);
            _plans.Chosen += plan =>
            {
                if (_match.Phase == MatchPhase.Ended || !_match.PlanAllowed(plan)) return;
                _match.SetPlan(0, plan);
                RefreshAll();
            };
            for (int i = 0; i < 3; i++)
            {
                _myCards[i] = FighterCard.Create(bottom, "MyCard" + i, true);
                UiKit.TopRow((RectTransform)_myCards[i].transform, 172f + i * 106f, 102f, 20f);
                _myCards[i].Clicked += OnCardClicked;
            }
            _btnSpecial = ActionButton(bottom, "필살기", -340f, out _btnSpecialLabel);
            _btnSpecial.onClick.AddListener(() => { _specialArmed = !_specialArmed; RefreshControls(); });
            _btnAppeal = ActionButton(bottom, "어필", 0f, out _btnAppealLabel);
            _btnAppeal.onClick.AddListener(OnAppeal);
            _btnHold = ActionButton(bottom, "버티기", 340f, out _);
            _btnHold.onClick.AddListener(OnHold);

            _boutPanel = BoutPanel.Create(bottom);
            _boutPanel.InterveneClicked += () => _bout?.Intervene(_bout.AttackerHuman);
            _result = ResultPanel.Create(_layout.Safe);
            _result.Again += () =>
            {
                PrototypeSession.PendingMatch = _match.Rules.Id;
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            };
            _result.Menu += BackToMenu;
            BuildDebug();
            BuildLog();

            TeamState mine = _match.Teams[0];
            for (int i = 0; i < 3; i++) _myCards[i].Bind(i < mine.Members.Count ? mine.Members[i] : null, _vars.downWakeRatio);
        }

        static Button SmallButton(RectTransform parent, string label, Vector2 pos, Action onClick)
        {
            Button b = UiKit.MakeButton(parent, label, label, new Vector2(116f, 50f));
            RectTransform rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = pos;
            b.GetComponentInChildren<Text>().fontSize = 24;
            b.onClick.AddListener(() => onClick());
            return b;
        }

        static Button ActionButton(RectTransform parent, string label, float x, out Text text)
        {
            Button b = UiKit.MakeButton(parent, label, label, new Vector2(320f, 74f));
            RectTransform rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(x, -496f);
            text = b.GetComponentInChildren<Text>();
            text.fontSize = 28;
            return b;
        }

        void RefreshAll()
        {
            float wake = _vars.downWakeRatio;
            Fighter actor = _match.Phase == MatchPhase.AwaitAction ? _match.Actor : null;
            foreach (var kv in _units) kv.Value.Refresh(wake, kv.Key == actor, kv.Key.Team == 1);
            _field.Refresh(_match);
            TeamState enemy = _match.Teams[1];
            for (int i = 0; i < 2; i++)
            {
                Fighter f = i < enemy.InRing.Count ? enemy.InRing[i] : null;
                if (_enemyCards[i].Fighter != f) _enemyCards[i].Bind(f, wake);
                if (f != null) _enemyCards[i].Refresh(f == actor, false);
            }
            _enemyBar.Refresh(enemy, _match.Tuning);
            _myBar.Refresh(_match.Teams[0], _match.Tuning);
            bool myTurn = _match.Phase == MatchPhase.AwaitAction && _match.ActingTeam == 0 && !_busy;
            foreach (FighterCard c in _myCards)
                if (c.Fighter != null) c.Refresh(c.Fighter == actor, myTurn && _match.CanTag(c.Fighter));
            _plans.Show(_match, 0);
            string who = _match.Phase == MatchPhase.Ended ? "경기 종료"
                : _match.Phase == MatchPhase.AwaitFinale ? "피날레!"
                : (_match.ActingTeam == 0 ? "내 차례: " : "상대 차례: ") + (actor != null ? actor.Short : "");
            _header.Set(_match.Rules.Name, Math.Min(_match.Turn, _match.TurnLimit), _match.TurnLimit, who);
            RefreshControls();
        }

        void RefreshControls()
        {
            bool myTurn = _match.Phase == MatchPhase.AwaitAction && _match.ActingTeam == 0 && !_busy;
            Fighter actor = myTurn ? _match.Actor : null;
            bool canShoot = myTurn && _match.CanShoot;
            if (!canShoot || (!actor.SpecialReady && !_match.JointReady)) _specialArmed = false;
            _btnSpecial.interactable = canShoot && (actor.SpecialReady || _match.JointReady);
            _btnSpecialLabel.text = canShoot && _match.JointReady ? "합동 필살기 (자동)" : _specialArmed ? "필살기 ON ✓" : canShoot && actor.SpecialReady ? "필살기 켜기" : "필살기 " + (actor != null ? actor.SpecialCount + "턴" : "");
            _btnAppeal.interactable = canShoot;
            _btnAppealLabel.text = canShoot ? "어필 +" + Mathf.RoundToInt(_match.AppealAmount()) : "어필";
            _btnHold.gameObject.SetActive(myTurn && _match.CanHold);
        }

        // ================= flow =================

        void BeginAction()
        {
            _busy = false;
            _aim.Locked = true;
            _specialArmed = false;
            RefreshAll();
            if (_match.Phase == MatchPhase.Ended)
            {
                ShowResult();
                return;
            }
            if (_match.Phase == MatchPhase.AwaitFinale)
            {
                StartCoroutine(FinaleStaging());
                return;
            }
            TeamState acting = _match.Acting;
            Fighter actor = _match.Actor;
            if (_lastActingTeam != _match.ActingTeam)
            {
                _lastActingTeam = _match.ActingTeam;
                if (acting.IsHuman) _fx.Banner("내 차례 · " + actor.Short, new Color32(0x9C, 0xC8, 0xFF, 0xFF), 0.75f);
                else if (actor.SpecialReady && !actor.IsDown) _fx.Banner("⚠ " + actor.Short + "의 필살기가 온다!", UiKit.Red, 1.1f);
                else _fx.Banner("상대 차례 · " + actor.Short, new Color32(0xFF, 0xA8, 0xA8, 0xFF), 0.75f);
            }
            if (!acting.IsHuman)
            {
                _busy = true;
                RefreshAll();
                _status.text = actor.Short + "의 차례…";
                StartCoroutine(AiAct());
                return;
            }
            if (_match.CanShoot)
            {
                _aim.Locked = false;
                bool threat = _match.Teams[1].InRing.Any(f => f.SpecialReady && !f.IsDown);
                _status.text = actor.Short + "의 차례 — 끌었다 놓아서 쏘기 · 대기 동료 카드 = 태그" + (threat ? "  ⚠ 상대 필살기 준비됨" : "");
            }
            else
            {
                _status.text = actor.Short + " 다운! " + (_match.DownedCanReachTag ? "로프 옆이라 태그 가능 — 동료 카드를 누르거나 버티기" : "링 중앙이라 태그 불가 — 버티기");
            }
        }

        IEnumerator AiAct()
        {
            yield return new WaitForSeconds(AiThinkSeconds);
            _match.SetTuning(_vars.ToTuning());
            AiDecision d = EnemyAi.Decide(_match);
            if (d.TagIn != null)
            {
                _fx.Punch(_ring.RingToLocal(d.TagIn.Position), "태그! " + d.TagIn.Short, UiKit.Yellow, 48);
                Log($"턴 {_match.Turn} · 상대 태그: {d.TagIn.Short} 입장");
                RefreshAll();
                yield return new WaitForSeconds(0.5f);
            }
            if (d.Action == AiAction.Hold)
            {
                Log($"턴 {_match.Turn} · {_match.Actor.Short} 버티기");
                _match.Hold();
                AfterAction();
                yield break;
            }
            if (d.Action == AiAction.Appeal)
            {
                DoAppeal();
                yield break;
            }
            if (d.IsFinale) _status.text = _match.Actor.Short + "의 피날레 시도 — " + PlanName(d.Plan);
            Fire(d.Velocity, d.Special);
        }

        void AfterAction()
        {
            ShowNotes();
            BeginAction();
        }

        void OnCardClicked(Fighter f)
        {
            if (_busy || _match.Phase != MatchPhase.AwaitAction || _match.ActingTeam != 0 || !_match.CanTag(f)) return;
            Fighter outgoing = _match.Actor;
            _match.Tag(f);
            Log($"턴 {_match.Turn} · 태그: {outgoing.Short} → {f.Short}");
            _fx.Punch(_ring.RingToLocal(f.Position), "태그! " + f.Short, UiKit.Yellow, 48);
            BeginAction();
        }

        void OnAppeal()
        {
            if (_busy || _match.ActingTeam != 0 || !_match.CanAppeal) return;
            DoAppeal();
        }

        void DoAppeal()
        {
            Fighter f = _match.Actor;
            float amount = _match.AppealAmount();
            _match.Appeal();
            Vector2 at = _ring.RingToLocal(f.Position);
            _fx.Punch(at + new Vector2(0f, 120f), (f.Data.Faction == Faction.Heel ? "도발! " : "어필! ") + "열기 +" + Mathf.RoundToInt(amount), new Color32(0xE8, 0x6A, 0x3A, 0xFF), 50);
            _fx.Shockwave(at, 180f, new Color32(0xE8, 0x6A, 0x3A, 0xC0));
            Log($"턴 {_match.Turn} · {f.Short} 어필 +{Mathf.RoundToInt(amount)} (필살기 카운트 −1)");
            AfterAction();
        }

        void OnHold()
        {
            if (_busy || _match.ActingTeam != 0 || !_match.CanHold) return;
            Log($"턴 {_match.Turn} · {_match.Actor.Short} 버티기");
            _match.Hold();
            AfterAction();
        }

        /// <summary>Editor checks: fire for the acting team with an explicit velocity.</summary>
        public bool FireForTest(float vx, float vy, bool special = false)
        {
            if (_busy || _match == null || _match.Phase != MatchPhase.AwaitAction || !_match.CanShoot) return false;
            Fire(new Vec2(vx, vy), special);
            return true;
        }

        /// <summary>Editor checks: press a hand during a bout.</summary>
        public void PressForTest(Hand h)
        {
            if (_bout == null) return;
            _boutPanel.Pressed(h, _bout.InIntro);
            _bout.Press(h);
        }

        // ================= shots =================

        void Fire(Vec2 velocity, bool special)
        {
            _busy = true;
            _aim.Locked = true;
            _guide.Hide();
            Tuning t = _vars.ToTuning();
            _match.SetTuning(t);
            _player.Feel = new HitFeel(t);
            _live.Clear();
            foreach (Fighter f in _match.AllFighters()) _live[f] = new Vector2(f.Stamina, f.Body);
            _shooter = _match.Actor;
            TeamState team = _match.Acting;
            _current = _match.Shoot(velocity, special);
            _shownHits = 0;
            _shownEvents = 0;
            _ropeLabel = 0;
            _lifts.Clear();
            PrepareLifts();
            var place = new List<Action<Vec2>>();
            foreach (object o in _current.Owners)
            {
                if (o is Fighter f) place.Add(_units[f].SetRingPosition);
                else if (ReferenceEquals(o, ShotResolver.RefereeOwner)) place.Add(_field.SetReferee);
                else place.Add(null);
            }
            RefreshControls();
            _status.text = "";
            bool atRope = _current.Kind == ShotKind.Special && _shooter.Data.SpecialAtRope && _current.Shot.Events.Any(e => e.Type == SimEventType.RopeBounce && e.Body == 0);
            _ropeCutInPending = atRope;
            if (_current.Kind != ShotKind.Normal && !atRope)
                StartCoroutine(CutInThenPlay(team, place, t));
            else
                _player.Play(_current.Shot, place, t.Get("view.playbackSpeed"));
        }

        Fighter[] CutInCast(TeamState team) =>
            _current.Kind == ShotKind.Joint ? team.Members.OrderBy(f => (int)f.Data.Role).ToArray() : new[] { _shooter };

        string CutInName() => _current.Kind == ShotKind.Joint ? "합동 필살기!" : _shooter.Data.SpecialName + "!";

        IEnumerator CutInThenPlay(TeamState team, List<Action<Vec2>> place, Tuning t)
        {
            yield return _fx.SpecialCutIn(CutInCast(team), CutInName(), CutInSeconds);
            _fx.Flash(0.6f, 0.2f);
            _player.Play(_current.Shot, place, t.Get("view.playbackSpeed"));
        }

        /// <summary>Flier jumps and grappler throws: lift windows taken from the replay's events.</summary>
        void PrepareLifts()
        {
            List<SimEvent> ev = _current.Shot.Events;
            for (int i = 0; i < ev.Count; i++)
            {
                SimEvent e = ev[i];
                if (e.Type == SimEventType.Takeoff)
                {
                    SimEvent land = ev.Skip(i + 1).FirstOrDefault(x => x.Type == SimEventType.Landing && x.Body == e.Body);
                    if (land.Type == SimEventType.Landing && _current.Owners[e.Body] is Fighter f)
                        _lifts.Add((_units[f], e.Time, land.Time));
                }
                else if (e.Type == SimEventType.Grab)
                {
                    SimEvent slam = ev.Skip(i + 1).FirstOrDefault(x => x.Type == SimEventType.Slam && x.Body == e.Other);
                    if (slam.Type == SimEventType.Slam && _current.Owners[e.Other] is Fighter f)
                        _lifts.Add((_units[f], e.Time, slam.Time));
                }
            }
        }

        void Update()
        {
            if (_player != null && _player.Playing && _current != null)
            {
                float time = _player.Time;
                ShowLive(time);
                foreach (var l in _lifts)
                {
                    if (time >= l.from && time <= l.to) l.unit.SetLift(Mathf.Sin(Mathf.PI * (time - l.from) / Mathf.Max(0.01f, l.to - l.from)));
                    else if (time > l.to) l.unit.SetLift(0f);
                }
                if (_shooter.Data.Type == FighterType.Stampeder && !_player.Paused && Time.time >= _afterimageAt)
                {
                    _afterimageAt = Time.time + 0.045f;
                    UnitView u = _units[_shooter];
                    _fx.Afterimage(u.Rect.anchoredPosition, u.Diameter, u.BaseColor);
                }
            }
            if (_bout != null) UpdateBout();
        }

        /// <summary>Damage and events appear the moment they happen in the replay, with weight by type.</summary>
        void ShowLive(float time)
        {
            while (_shownHits < _current.Hits.Count && _current.Hits[_shownHits].Time <= time + 1e-4f)
            {
                HitInfo h = _current.Hits[_shownHits++];
                Vector2 st = _live[h.Target];
                st.x -= h.Damage;
                st.y -= h.BodyDamage;
                _live[h.Target] = st;
                Fighter credit = h.Source ?? _shooter;
                _dealt.TryGetValue(credit, out float dealt);
                _dealt[credit] = dealt + h.Damage;
                UnitView u = _units[h.Target];
                u.Flash();
                u.ShowStamina(st.x, h.Target.MaxStamina, st.y, h.Target.MaxBody);
                foreach (FighterCard c in _myCards.Concat(_enemyCards))
                    if (c.Fighter == h.Target) c.ShowStamina(st.x, st.y);
                Vector2 at = _ring.RingToLocal(u.RingPosition);
                int size = Mathf.Clamp(34 + Mathf.RoundToInt(h.Damage / 12f), 34, 84);
                _fx.Punch(at + new Vector2(0f, 110f), "−" + Mathf.RoundToInt(h.Damage), h.Damage > 450f ? new Color32(0xFF, 0x7A, 0x3A, 0xFF) : UiKit.Yellow, size, 0.8f);
                if (h.Tags.Length > 0) FloatingText.Spawn(_ring.FxLayer, at + new Vector2(0f, 50f), h.Tags, UiKit.TextMain);
                if (h.Tags.Contains("직격"))
                {
                    _player.Feel?.AddStop(0.13f);
                    _fx.Shake(20f, 0.3f);
                    _fx.Shockwave(at, 150f, Color.white);
                    _fx.Punch(at + new Vector2(0f, 190f), "직격!", Color.white, 70, 0.7f);
                }
                else if (h.Tags.Contains("라리아트"))
                {
                    _player.Feel?.AddStop(0.1f);
                    _fx.Shake(16f, 0.25f);
                    _fx.Punch(at + new Vector2(0f, 190f), "라리아트!", new Color32(0xFF, 0xB0, 0x40, 0xFF), 70, 0.8f);
                }
                else if (h.Kind == HitKind.Direct) _fx.Shake(7f, 0.12f);
                if (h.Kind == HitKind.Object || h.Kind == HitKind.Cage) _fx.Shake(14f, 0.25f);
                if (h.CausedDown)
                {
                    _player.Feel?.Slow(0.25f, 0.7f);
                    _fx.Shake(18f, 0.35f);
                    _fx.Punch(at + new Vector2(0f, 250f), "DOWN!", UiKit.Red, 84, 1.1f);
                }
            }
            while (_shownEvents < _current.Events.Count && _current.Events[_shownEvents].Time <= time + 1e-4f)
            {
                FieldEvent e = _current.Events[_shownEvents++];
                Vector2 at = _ring.RingToLocal(e.Position);
                switch (e.Kind)
                {
                    case FieldEventKind.Takeoff:
                        _fx.Punch(at + new Vector2(0f, 120f), "점프!", new Color32(0x9C, 0xD8, 0xFF, 0xFF), 46, 0.6f);
                        continue;
                    case FieldEventKind.Landing:
                        _fx.Shockwave(at, _match.Tuning.Get("type.landRadius") * _ring.UnitsPerRing, new Color32(0x9C, 0xD8, 0xFF, 0xE0), 0.5f);
                        _fx.Shake(12f, 0.25f);
                        _fx.Punch(at + new Vector2(0f, 120f), "착지!", Color.white, 52, 0.6f);
                        continue;
                    case FieldEventKind.Grab:
                        _player.Feel?.AddStop(0.32f);
                        _fx.Punch(at + new Vector2(0f, 140f), "잡았다!", UiKit.Yellow, 64, 0.7f);
                        _units[e.Fighter].Flash();
                        _units[_shooter].Flash();
                        continue;
                    case FieldEventKind.Slam:
                        _fx.Shake(26f, 0.4f);
                        _fx.Flash(0.5f, 0.18f);
                        _fx.Shockwave(at, 260f, Color.white, 0.55f);
                        _fx.Punch(at + new Vector2(0f, 160f), "슬램!", Color.white, 84, 0.9f);
                        continue;
                    case FieldEventKind.Assist:
                        if (e.Fighter.Data.Assist == AssistKind.Lariat)
                        {
                            Vector2 center = _ring.RingToLocal(new Vec2(_match.Tuning.Get("ring.size") * 0.5f, _match.Tuning.Get("ring.size") * 0.5f));
                            Vector2 dir = (center - at).normalized;
                            _fx.Beam(at, at + dir * 1300f, 46f, new Color32(0x63, 0xC9, 0x52, 0xB0));
                        }
                        else if (e.Fighter.Data.Assist == AssistKind.Stomp)
                            _fx.Shockwave(_ring.RingToLocal(_units[_shooter].RingPosition), _match.Tuning.Get("assist.stompRadius") * _ring.UnitsPerRing, new Color32(0x63, 0xC9, 0x52, 0xD0));
                        _units[e.Fighter].Flash();
                        _fx.Punch(at + new Vector2(0f, 90f), e.Text, new Color32(0x63, 0xC9, 0x52, 0xFF), 48, 0.8f);
                        continue;
                    case FieldEventKind.Knockoff:
                        _fx.Shake(10f, 0.2f);
                        _fx.Punch(at + new Vector2(0f, 90f), e.Text, UiKit.Red, 48, 0.8f);
                        continue;
                    case FieldEventKind.RefDown:
                        _fx.Punch(at + new Vector2(0f, 90f), "심판 다운!", UiKit.Red, 56, 0.9f);
                        continue;
                    case FieldEventKind.Weapon:
                    case FieldEventKind.ObjectBreak:
                        if (e.Object != null) _field.Break(e.Object);
                        _fx.Shockwave(at, 140f, new Color32(0xC8, 0x9B, 0x6A, 0xE0));
                        _fx.Punch(at + new Vector2(0f, 90f), e.Text, new Color32(0xE8, 0xC0, 0x8A, 0xFF), 50, 0.8f);
                        continue;
                    case FieldEventKind.Dive:
                        _fx.Punch(at + new Vector2(0f, 200f), "코너 다이브!", new Color32(0x9C, 0xD8, 0xFF, 0xFF), 60, 0.8f);
                        _fx.Shake(14f, 0.25f);
                        continue;
                    case FieldEventKind.PassThrough:
                        _fx.Punch(at + new Vector2(0f, 150f), "관통!", Color.white, 58, 0.6f);
                        continue;
                    default:
                        if (e.Text.Length > 0) _fx.Punch(at + new Vector2(0f, 90f), e.Text, UiKit.TextMain, 44, 0.8f);
                        continue;
                }
            }
        }

        void OnShotEvent(SimEvent e)
        {
            if (e.Type != SimEventType.RopeBounce || e.Body != 0) return;
            _ropeLabel++;
            Vector2 at = _ring.RingToLocal(e.Position);
            if (_shooter.Data.Type == FighterType.Showman)
            {
                int cap = (int)_match.Tuning.Get(_current.Kind == ShotKind.Normal ? "rope.bonusMax" : "type.showmanMaxSpecial");
                int lvl = Mathf.Min(_ropeLabel, cap);
                _units[_shooter].SetAura(lvl);
                Color[] steps = { Color.white, new Color(1f, 0.9f, 0.3f), new Color(1f, 0.6f, 0.15f), new Color(1f, 0.3f, 0.15f), new Color(1f, 0.2f, 0.6f) };
                _fx.Punch(at + new Vector2(0f, 90f), lvl >= cap ? "MAX! +" + Mathf.RoundToInt(lvl * _match.Tuning.Get("type.showmanBonus") * 100f) + "%"
                    : "반동 ×" + lvl + "  +" + Mathf.RoundToInt(lvl * _match.Tuning.Get("type.showmanBonus") * 100f) + "%", steps[Mathf.Min(lvl, steps.Length) - 1], 40 + lvl * 8, 0.7f);
                _fx.Shockwave(at, 80f + lvl * 30f, steps[Mathf.Min(lvl, steps.Length) - 1]);
            }
            else FloatingText.Spawn(_ring.FxLayer, at + new Vector2(0f, 60f), "로프 ×" + _ropeLabel, UiKit.TextDim);

            if (_ropeCutInPending)
            {
                _ropeCutInPending = false;
                StartCoroutine(RopeCutIn());
            }
        }

        /// <summary>Rope-timed special: the rope is hauled back hard, time stops, cut-in, then release.</summary>
        IEnumerator RopeCutIn()
        {
            _player.Paused = true;
            float t = 0f;
            while (t < 0.35f)
            {
                t += Time.unscaledDeltaTime;
                _ring.RopeExaggerate = Mathf.Lerp(1f, 3.2f, t / 0.35f);
                yield return null;
            }
            yield return _fx.SpecialCutIn(CutInCast(_match.Teams[_shooter.Team]), CutInName(), CutInSeconds);
            _ring.RopeExaggerate = 1f;
            _fx.Flash(0.6f, 0.2f);
            _fx.Shake(22f, 0.3f);
            _player.Paused = false;
        }

        void OnShotFinished()
        {
            ShowLive(float.MaxValue);
            foreach (var l in _lifts) l.unit.SetLift(0f);
            _units[_shooter].SetAura(0);
            LogShot(_current);
            if (_current.Finale != null)
            {
                BeginAction();
                return;
            }
            AfterAction();
        }

        void ShowNotes()
        {
            foreach (MatchNote n in _match.Notes)
            {
                if (n.Fighter == null) continue;
                Vector2 at = _ring.RingToLocal(n.Fighter.Position);
                switch (n.Kind)
                {
                    case NoteKind.Recovered: FloatingText.Spawn(_ring.FxLayer, at + new Vector2(0f, -90f), "+" + Mathf.RoundToInt(n.Amount), new Color32(0x63, 0xC9, 0x52, 0xFF)); break;
                    case NoteKind.Woke: _fx.Punch(at + new Vector2(0f, 100f), "기상!", Color.white, 48, 0.8f); break;
                    case NoteKind.SpecialReady: _fx.Punch(at + new Vector2(0f, 120f), "필살기 준비!", UiKit.Yellow, 40, 0.9f); break;
                    case NoteKind.Returned: FloatingText.Spawn(_ring.FxLayer, at, "에이프런 복귀", UiKit.TextDim); break;
                    case NoteKind.Chain: _fx.Punch(at + new Vector2(0f, 140f), "체인 " + Mathf.RoundToInt(n.Amount) + "칸!", new Color32(0xEF, 0x9F, 0x27, 0xFF), 50, 0.9f); break;
                    case NoteKind.Warning:
                        _fx.YellowCard(_ring.RingToLocal(_match.Field.Referee), Mathf.RoundToInt(n.Amount), (int)_match.Tuning.Get("warnings.penaltyAt"));
                        break;
                }
            }
            _match.ClearNotes();
        }

        // ================= finale =================

        /// <summary>
        /// The moment of the finale: freeze on contact (pinfall, submission) or drive the defender
        /// into the ropes (ringout), name it, then open the bout with its intro.
        /// </summary>
        IEnumerator FinaleStaging()
        {
            _busy = true;
            _aim.Locked = true;
            FinaleTrigger f = _match.PendingFinale;
            UnitView atk = _units[f.Attacker];
            UnitView def = _units[f.Defender];
            Vector2 at = _ring.RingToLocal(f.Position);
            if (f.Plan == FinalePlan.Ringout)
            {
                _fx.Punch(at + new Vector2(0f, 150f), "잡았다!", UiKit.Yellow, 60, 0.6f);
                atk.Flash();
                def.Flash();
                yield return new WaitForSecondsRealtime(0.3f);
                Vec2 a0 = f.AttackerAt, d0 = f.Position;
                Vec2 d1 = f.RopePoint + f.PushDir * 0.6f; // press into the rope
                Vec2 a1 = f.RopePoint - f.PushDir * (2f * _match.Tuning.Get("unit.radius") - 0.5f);
                float t = 0f;
                float dur = 0.25f + 0.08f * f.PushDistance;
                while (t < dur)
                {
                    t += Time.unscaledDeltaTime;
                    float k = Mathf.Clamp01(t / dur);
                    k = k * k;
                    atk.SetRingPosition(a0 + (a1 - a0) * k);
                    def.SetRingPosition(d0 + (d1 - d0) * k);
                    yield return null;
                }
                _fx.Shake(20f, 0.35f);
                _fx.Punch(_ring.RingToLocal(d1) + new Vector2(0f, 150f), "로프로 몰아붙였다!", Color.white, 56, 0.9f);
            }
            else
            {
                _fx.Flash(0.7f, 0.25f);
                _fx.Shake(16f, 0.3f);
                atk.Flash();
                def.Flash();
                _fx.Punch(at + new Vector2(0f, 160f), f.Plan == FinalePlan.Pinfall ? "덮쳤다!" : "걸었다!", Color.white, 70, 0.9f);
            }
            yield return new WaitForSecondsRealtime(0.75f);
            StartBout();
        }

        void StartBout()
        {
            _match.SetTuning(_vars.ToTuning());
            _bout = _match.CreateBout();
            _lastFinaleAttacker = _bout.Attacker;
            _boutDoneAt = -1f;
            Fighter atk = _bout.Attacker;
            Fighter def = _bout.Defender;
            _cutIn.Open(_bout.Kind, atk.Short, UiKit.AttributeColor(atk.Data.Attribute), def.Short, UiKit.AttributeColor(def.Data.Attribute));
            _boutPanel.Open(_bout, BoutChips(_bout));
            _status.text = "";
            RefreshAll();
        }

        List<string> BoutChips(FinaleBout b)
        {
            Tuning t = _match.Tuning;
            FinaleTrigger f = _match.PendingFinale;
            var chips = new List<string>();
            chips.Add("방어 " + b.Defender.Short + " 바디 " + Mathf.RoundToInt(b.Defender.BodyRatio * 100f) + "%");
            if (b.Defender.DownedBySpecial) chips.Add("<color=#F2B12C>필살기 다운: 저항 ×" + t.Get("finale.specialResist") + "</color>");
            if (b is PinfallBout p) chips.Add("심판까지 " + (_match.Field.Referee - b.Defender.Position).Length.ToString("0.0") + " (카운트 " + p.RefDelay.ToString("0.0") + "초 뒤)");
            if (b is SubmissionBout s) chips.Add(s.RopeBreak ? (f.NearRope ? "<color=#F07070>로프 근처: 기어가기 60%부터</color>" : "로프 멀다") : "로프 브레이크 없음");
            if (b.Kind == FinalePlan.Ringout) chips.Add("밀어붙인 거리 " + f.PushDistance.ToString("0.0") + (f.PushDistance > 2f ? " <color=#F07070>(멀수록 불리)</color>" : ""));
            float lo = t.Get("mash.heatMulMin"), hi = t.Get("mash.heatMulMax");
            chips.Add("열기 ×" + (lo + (hi - lo) * _match.Teams[f.AttackerTeam].Heat / 100f).ToString("0.00"));
            if (_match.Teams[f.AttackerTeam].Warnings >= t.Get("warnings.penaltyAt")) chips.Add("<color=#F07070>경고 누적: 시작 불리</color>");
            return chips;
        }

        void UpdateBout()
        {
            if (_bout.Result == MashResult.Running)
            {
                if (_bout.HasHuman)
                {
                    HandInput.Read(_hands);
                    foreach (Hand h in _hands)
                    {
                        _boutPanel.Pressed(h, _bout.InIntro);
                        _bout.Press(h);
                    }
                }
                _bout.Advance(Time.deltaTime);
            }
            _boutPanel.Refresh(_bout);
            _cutIn.SetGauge(_bout.AttackerProgress);
            if (_bout.Result == MashResult.Running) return;

            bool won = _bout.Result == MashResult.AttackerWins;
            if (_boutDoneAt < 0f)
            {
                _boutDoneAt = Time.time;
                string[] win = { "3카운트!", "기브업!", "링아웃!" };
                string[] fail = { "킥아웃!", _bout is SubmissionBout s && s.RopeBreak && s.Crawl >= 100f ? "로프 브레이크!" : "탈출!", "버텨 냈다!" };
                bool good = won == _bout.AttackerHuman || (!_bout.HasHuman && won);
                _boutPanel.ShowResult(won ? win[(int)_bout.Kind] : fail[(int)_bout.Kind], good);
                _fx.Shake(won ? 24f : 12f, 0.35f);
                Log($"턴 {_match.Turn} · 피날레 {PlanName(_bout.Kind)} {_bout.Attacker.Short}→{_bout.Defender.Short}: {(won ? "성공" : "실패")} (공격 {_bout.A:0.00} / 방어 {_bout.D:0.00}, 바디 {Mathf.RoundToInt(_bout.Defender.BodyRatio * 100f)}%)");
            }
            if (Time.time - _boutDoneAt < BoutResultHold) return;

            Fighter defender = _bout.Defender;
            _bout = null;
            _boutPanel.Close();
            _cutIn.Close();
            _match.ResolveFinale(won);
            if (!won) _fx.Punch(_ring.RingToLocal(defender.Position) + new Vector2(0f, 140f), "니어폴!", UiKit.Yellow, 64, 1f);
            AfterAction();
        }

        // ================= result & log =================

        void ShowResult()
        {
            _aim.Locked = true;
            _busy = true;
            bool won = _match.Winner == 0;
            string headline;
            Fighter finisher = null;
            if (_match.EndReason == EndReason.TurnLimit) headline = "턴 제한 — 끝내지 못했다";
            else
            {
                finisher = _lastFinaleAttacker;
                headline = (finisher != null ? finisher.Data.Name + "의 " : "") + PlanName(_match.WinningPlan ?? FinalePlan.Pinfall) + (won ? " 승리!" : "에 당했다");
            }
            Fighter mvp = _dealt.Where(kv => kv.Key.Team == 0).OrderByDescending(kv => kv.Value).Select(kv => kv.Key).FirstOrDefault();
            float mvpDamage = mvp != null ? _dealt[mvp] : 0f;
            TeamState me = _match.Teams[0];
            TeamState en = _match.Teams[1];
            float minutes = (Time.time - _startTime) / 60f;
            var stats = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("턴", Math.Min(_match.Turn, _match.TurnLimit).ToString()),
                new KeyValuePair<string, string>("시간", minutes.ToString("0.0") + "분"),
                new KeyValuePair<string, string>("상대 다운", en.Members.Sum(f => f.Downs).ToString()),
                new KeyValuePair<string, string>("내 다운", me.Members.Sum(f => f.Downs).ToString()),
                new KeyValuePair<string, string>("태그 · 어시스트", me.Tags + " · " + me.Assists),
                new KeyValuePair<string, string>("필살기 · 합동", me.Specials + " · " + me.Joints),
            };
            _result.Show(won, headline, finisher, mvp, mvpDamage, stats);
            AppendBattleLog(won, headline, minutes);
        }

        void AppendBattleLog(bool won, string reason, float minutes)
        {
            try
            {
                string path = Path.Combine(Application.persistentDataPath, "battle_log.csv");
                bool fresh = !File.Exists(path);
                TeamState me = _match.Teams[0];
                TeamState en = _match.Teams[1];
                string line = string.Join(",", DateTime.Now.ToString("yyyy-MM-dd HH:mm"), _match.Rules.Id, _match.Seed, won ? "승" : "패", reason.Replace(",", " "),
                    Math.Min(_match.Turn, _match.TurnLimit), minutes.ToString("0.0"), me.Members.Sum(f => f.Downs), en.Members.Sum(f => f.Downs),
                    me.FinaleTries, en.FinaleTries, me.Tags, me.Assists, me.Appeals, me.Specials, me.Joints, me.Interventions,
                    string.Join("/", me.Members.Select(f => f.Data.Id)));
                File.AppendAllText(path, (fresh ? "time,match,seed,result,reason,turns,minutes,my_downs,enemy_downs,my_finales,enemy_finales,tags,assists,appeals,specials,joints,interventions,team\n" : "") + line + "\n");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("battle_log.csv: " + ex.Message);
            }
        }

        void LogShot(ShotOutcome o)
        {
            var parts = new List<string>();
            foreach (var g in o.Hits.GroupBy(h => h.Target))
                parts.Add(g.Key.Short + " −" + Mathf.RoundToInt(g.Sum(h => h.Damage)) + (g.Any(h => h.CausedDown) ? " 다운" : ""));
            foreach (FieldEvent e in o.Events)
                if (e.Text.Length > 0) parts.Add(e.Text);
            string kind = o.Kind == ShotKind.Special ? " 필살기" : o.Kind == ShotKind.Joint ? " 합동 필살기" : "";
            Log($"턴 {_match.Turn} · {o.Shooter.Short}{kind}: " + (parts.Count > 0 ? string.Join(", ", parts) : "빗나감") + (o.Finale != null ? " → 피날레!" : ""));
        }

        void Log(string line)
        {
            _log.Add(line);
            if (_log.Count > 80) _log.RemoveAt(0);
            if (_logText != null && _logPanel.gameObject.activeSelf) _logText.text = string.Join("\n", _log.Skip(Math.Max(0, _log.Count - 28)));
        }

        void BuildLog()
        {
            Image bg = UiKit.MakeImage(_layout.Safe, "LogPanel", UiKit.Square, new Color(0.05f, 0.05f, 0.07f, 0.95f));
            bg.raycastTarget = true;
            UiKit.Stretch(bg.rectTransform, 30f, 300f, 30f, 260f);
            _logPanel = bg.rectTransform;
            _logText = UiKit.MakeText(bg.rectTransform, "Text", 26, TextAnchor.UpperLeft, UiKit.TextMain);
            UiKit.Stretch(_logText.rectTransform, 24f, 120f, 24f, 24f);
            _logText.horizontalOverflow = HorizontalWrapMode.Wrap;
            Button close = UiKit.MakeButton(bg.rectTransform, "Close", "닫기", new Vector2(300f, 90f));
            RectTransform cr = (RectTransform)close.transform;
            cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 0f);
            cr.pivot = new Vector2(0.5f, 0f);
            cr.anchoredPosition = new Vector2(0f, 20f);
            close.onClick.AddListener(ToggleLog);
            _logPanel.gameObject.SetActive(false);
        }

        void ToggleLog()
        {
            _logPanel.gameObject.SetActive(!_logPanel.gameObject.activeSelf);
            _logPanel.SetAsLastSibling();
            _logText.text = string.Join("\n", _log.Skip(Math.Max(0, _log.Count - 28)));
        }

        void BuildDebug()
        {
            Image bg = UiKit.MakeImage(_layout.Safe, "DebugPanel", UiKit.Square, new Color(0.05f, 0.05f, 0.07f, 0.95f));
            bg.raycastTarget = true;
            UiKit.Stretch(bg.rectTransform, 120f, 500f, 120f, 300f);
            _debug = bg.rectTransform;
            string[] labels = { "내 열기 +20", "내 필살기 모두 충전", "상대 링 안 스태미나 10%", "상대 링 안 바디 20%", "배속 ×1 / ×2", "포기하고 매치 선택", "닫기" };
            Action[] acts =
            {
                () => { _match.DebugSetHeat(0, _match.Teams[0].Heat + 20f); RefreshAll(); },
                () => { foreach (Fighter f in _match.Teams[0].Members) f.SpecialCount = 0; RefreshAll(); },
                () => { foreach (Fighter f in _match.Teams[1].InRing) f.Stamina = Mathf.Max(1f, f.MaxStamina * 0.1f); RefreshAll(); },
                () => { foreach (Fighter f in _match.Teams[1].InRing) f.Body = f.MaxBody * 0.2f; RefreshAll(); },
                () => { Time.timeScale = Time.timeScale > 1.5f ? 1f : 2f; },
                BackToMenu,
                ToggleDebug,
            };
            for (int i = 0; i < labels.Length; i++)
            {
                Button b = UiKit.MakeButton(bg.rectTransform, labels[i], labels[i], new Vector2(640f, 100f));
                RectTransform rt = (RectTransform)b.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -40f - i * 120f);
                Action a = acts[i];
                b.onClick.AddListener(() => a());
            }
            _debug.gameObject.SetActive(false);
        }

        void ToggleDebug()
        {
            _debug.gameObject.SetActive(!_debug.gameObject.activeSelf);
            _debug.SetAsLastSibling();
        }

        void BackToMenu()
        {
            Time.timeScale = 1f;
            PrototypeSession.PendingMatch = null;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        static string PlanName(FinalePlan p) => p == FinalePlan.Pinfall ? "핀폴" : p == FinalePlan.Submission ? "서브미션" : "링아웃";

        // ================= aiming =================

        void OnPull(Vec2 pull)
        {
            if (_busy || _match.Phase != MatchPhase.AwaitAction || !_match.CanShoot) return;
            Tuning t = _vars.ToTuning();
            _match.SetTuning(t);
            float maxPull = t.Get("view.maxPullUnits");
            if (pull.Length < t.Get("shot.minPullRatio") * maxPull)
            {
                _guide.Hide();
                return;
            }
            ShotInput.TryFromPull(pull, maxPull, t.Get("shot.maxSpeed"), t.Get("shot.minPullRatio"), out Vec2 velocity);
            Fighter me = _match.Actor;
            AimGuide guide = AimPredictor.Predict(_match.SimForRules, me.Position, t.Get("unit.radius"), velocity);
            _guide.Show(me.Position, guide, ShotInput.Power01(pull, maxPull));
        }

        void OnRelease(Vec2 pull)
        {
            _guide.Hide();
            if (_busy || _match.Phase != MatchPhase.AwaitAction || !_match.CanShoot) return;
            Tuning t = _match.Tuning;
            bool ok = ShotInput.TryFromPull(pull, t.Get("view.maxPullUnits"), t.Get("shot.maxSpeed"), t.Get("shot.minPullRatio"), out Vec2 velocity);
            if (ok) Fire(velocity, _specialArmed);
            else _status.text = "짧게 당겨서 취소했습니다";
        }

        void LateUpdate()
        {
            if (_ring == null || _match == null) return;
            _ropeProbe.Clear();
            foreach (var kv in _units)
                if (kv.Key.InRing) _ropeProbe.Add(kv.Value.RingPosition);
            _ring.UpdateRopes(_ropeProbe, _vars.unitRadius);
        }

        static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
            DontDestroyOnLoad(go);
        }
    }
}
