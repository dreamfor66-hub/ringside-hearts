using System;

namespace RingsideHearts.Battle.Core
{
    public enum Hand { Left, Right }

    public enum MashResult { Running, AttackerWins, DefenderWins }

    /// <summary>
    /// A finale tug of war with two hands (design v0.2 7). Attacker power A comes from power,
    /// heat and second skills; defender strength D from guts and remaining Body (no hidden
    /// catch-up). A human side plays with Left/Right presses; an AI side plays by its team's
    /// tap rate and timing accuracy.
    /// </summary>
    public abstract class FinaleBout
    {
        public FinalePlan Kind { get; protected set; }
        public Fighter Attacker { get; private set; }
        public Fighter Defender { get; private set; }
        public bool AttackerHuman { get; private set; }
        public bool DefenderHuman { get; private set; }
        public MashResult Result { get; protected set; }
        public float Elapsed { get; private set; }
        public float TimeLimit { get; protected set; }
        /// <summary>Attacker strength (power × heat × second).</summary>
        public float A { get; private set; }
        /// <summary>Defender strength (guts × body, lower after a special).</summary>
        public float D { get; private set; }
        public bool AttackerCanIntervene { get; private set; }
        public bool DefenderCanIntervene { get; private set; }
        public string LastIntervention { get; private set; } = "";
        /// <summary>Seconds of the intro left: the scene and what to do are shown, nothing moves, input is ignored.</summary>
        public float IntroLeft { get; private set; }
        public bool InIntro => IntroLeft > 0f;

        protected Tuning T;
        protected Rng R;
        protected TeamState AtkTeam;
        protected TeamState DefTeam;
        Hand? _lastHand;
        float _lastLeft = -9f;
        float _lastRight = -9f;
        float _boostAtk, _boostDef, _freezeAtk, _freezeDef;

        public bool HasHuman => AttackerHuman || DefenderHuman;
        public bool HumanIsAttacker => AttackerHuman;

        public static FinaleBout Create(Match m, FinaleTrigger f)
        {
            FinaleBout b;
            switch (f.Plan)
            {
                case FinalePlan.Pinfall: b = new PinfallBout(); break;
                case FinalePlan.Submission: b = new SubmissionBout(); break;
                default: b = new RingoutBout(); break;
            }
            b.Init(m, f);
            return b;
        }

        void Init(Match m, FinaleTrigger f)
        {
            T = m.Tuning;
            R = m.Rng;
            Kind = f.Plan;
            Attacker = f.Attacker;
            Defender = f.Defender;
            AtkTeam = m.Teams[f.AttackerTeam];
            DefTeam = m.Teams[f.Defender.Team];
            AttackerHuman = AtkTeam.IsHuman;
            DefenderHuman = DefTeam.IsHuman && !AttackerHuman;
            float lo = T.Get("mash.heatMulMin");
            float hi = T.Get("mash.heatMulMax");
            A = Attacker.Power / 100f * (lo + (hi - lo) * AtkTeam.Heat / 100f) * Attacker.FinalePowerMul;
            float body = T.Get("finale.bodyFloor") + (1f - T.Get("finale.bodyFloor")) * Defender.BodyRatio;
            D = Defender.Guts / 100f * body * (Defender.DownedBySpecial ? T.Get("finale.specialResist") : 1f);
            AttackerCanIntervene = !AtkTeam.InterventionUsed && Attacker.Second != null;
            DefenderCanIntervene = !DefTeam.InterventionUsed && Defender.Second != null;
            IntroLeft = HasHuman ? T.Get("finale.intro") : 0f;
            Setup(m, f, StartPenalty());
        }

        float StartPenalty() => AtkTeam.Warnings >= T.Get("warnings.penaltyAt") ? T.Get("warnings.penalty") : 0f;

        protected abstract void Setup(Match m, FinaleTrigger f, float startPenalty);
        protected abstract void OnPress(Hand h, bool alternated, bool both, bool attackerSide);
        protected abstract void Step(float dt);
        /// <summary>0..1: how close the attacker is to winning (for UI and AI intervention).</summary>
        public abstract float AttackerProgress { get; }

        protected float AtkMul => (Elapsed < _freezeAtk ? 0f : 1f) * (Elapsed < _boostAtk ? T.Get("intervene.faceMul") : 1f);
        protected float DefMul => (Elapsed < _freezeDef ? 0f : 1f) * (Elapsed < _boostDef ? T.Get("intervene.faceMul") : 1f);

        public void Press(Hand h)
        {
            if (Result != MashResult.Running || !HasHuman || InIntro) return;
            float now = Elapsed;
            bool alternated = _lastHand != h;
            float other = h == Hand.Left ? _lastRight : _lastLeft;
            bool both = now - other <= T.Get("input.bothWindow");
            if (h == Hand.Left) _lastLeft = now;
            else _lastRight = now;
            _lastHand = h;
            OnPress(h, alternated, both, AttackerHuman);
        }

        public void Advance(float dt)
        {
            if (Result != MashResult.Running || dt <= 0f) return;
            if (IntroLeft > 0f)
            {
                IntroLeft -= dt;
                if (IntroLeft > 0f) return;
                dt = -IntroLeft;
                IntroLeft = 0f;
            }
            Elapsed += dt;
            AiIntervene();
            Step(dt);
            if (Result == MashResult.Running && TimeLimit > 0f && Elapsed >= TimeLimit) Result = MashResult.DefenderWins;
        }

        void AiIntervene()
        {
            if (!AttackerHuman && AttackerCanIntervene && AttackerProgress < T.Get("intervene.aiBelow")) Intervene(true);
            if (!DefenderHuman && DefenderCanIntervene && AttackerProgress > 1f - T.Get("intervene.aiBelow")) Intervene(false);
        }

        /// <summary>
        /// The second steps in, once per match per team. Face: that side's input ×1.5 for 3 s.
        /// Heel: the other side freezes for 2 s, and the referee warns the team.
        /// </summary>
        public void Intervene(bool attackerSide)
        {
            if (Result != MashResult.Running) return;
            if (attackerSide ? !AttackerCanIntervene : !DefenderCanIntervene) return;
            Fighter owner = attackerSide ? Attacker : Defender;
            TeamState team = attackerSide ? AtkTeam : DefTeam;
            TeamState enemy = attackerSide ? DefTeam : AtkTeam;
            team.InterventionUsed = true;
            team.Interventions++;
            if (attackerSide) AttackerCanIntervene = false;
            else DefenderCanIntervene = false;
            if (owner.Second.Faction == Faction.Face)
            {
                float until = Elapsed + T.Get("intervene.faceSeconds") + owner.InterventionTimeAdd;
                if (attackerSide) _boostAtk = until;
                else _boostDef = until;
                LastIntervention = owner.Second.Short + " 응원!";
            }
            else
            {
                float weaken = 1f;
                foreach (Fighter f in enemy.Members) weaken = Math.Min(weaken, f.InterventionMul);
                float until = Elapsed + (T.Get("intervene.heelSeconds") + owner.InterventionTimeAdd) * weaken;
                if (attackerSide) _freezeDef = until;
                else _freezeAtk = until;
                team.Warnings++;
                LastIntervention = owner.Second.Short + " 난입!";
            }
        }

        protected static float Clamp100(float v) => v < 0f ? 0f : v > 100f ? 100f : v;
        protected float AiRate(TeamState team) => team.Setup.AiTapRate;
        protected float AiQuality(TeamState team) => Math.Max(0f, Math.Min(1f, team.Setup.AiTiming + R.Range(-0.15f, 0.15f)));
    }

    /// <summary>
    /// Pinfall. Human attacker: alternate hands to keep the press gauge up; the referee counts while
    /// it is at or above pin.threshold. Human defender: a kickout timing window each count, both
    /// hands together; the window is wider the stronger the defender is against the attacker.
    /// </summary>
    public sealed class PinfallBout : FinaleBout
    {
        public float Gauge { get; private set; }
        public int Count { get; private set; }
        public float CountProgress { get; private set; }
        public float RefDelay { get; private set; }
        public bool RefCounting => Elapsed >= RefDelay;
        /// <summary>Human defender: half width of the kickout window, seconds.</summary>
        public float Window { get; private set; }
        public int Counts { get; private set; }
        public float CountSeconds { get; private set; }
        public float Threshold { get; private set; }
        bool _attemptUsed;
        int _attemptCount = -1;

        protected override void Setup(Match m, FinaleTrigger f, float penalty)
        {
            Gauge = Clamp100(T.Get("pin.start") - penalty);
            float dist = (m.Field.Referee - f.Defender.Position).Length;
            RefDelay = T.Get("pin.refDelayBase") + dist * T.Get("pin.refDelayPerUnit");
            Counts = (int)T.Get("pin.counts");
            Threshold = T.Get("pin.threshold");
            CountSeconds = T.Get("pin.countSeconds");
            float ratio = A > 0f ? D / A : 1f;
            Window = Math.Max(T.Get("pin.windowMin"), Math.Min(T.Get("pin.windowMax"), T.Get("pin.window") * ratio));
        }

        /// <summary>Human defender: seconds from now to the center of this count's kickout window (negative = passed).</summary>
        public float TimeToWindow => !RefCounting ? RefDelay + CountSeconds * 0.75f - Elapsed : CountSeconds * (0.75f - CountProgress);

        public override float AttackerProgress => (Count + CountProgress) / Math.Max(1, Counts);

        protected override void OnPress(Hand h, bool alternated, bool both, bool attackerSide)
        {
            if (attackerSide)
            {
                if (alternated) Gauge = Clamp100(Gauge + T.Get("pin.tap") * A * AtkMul);
                return;
            }
            if (!both || !RefCounting || DefMul <= 0f) return;
            if (_attemptCount == Count && _attemptUsed) return;
            _attemptCount = Count;
            _attemptUsed = true;
            if (Math.Abs(TimeToWindow) <= Window * Math.Max(1f, DefMul)) Result = MashResult.DefenderWins;
        }

        protected override void Step(float dt)
        {
            bool timing = DefenderHuman;
            if (!timing)
            {
                if (!AttackerHuman) Gauge += AiRate(AtkTeam) * T.Get("pin.tap") * A * AtkMul * dt;
                Gauge -= T.Get("pin.defend") * D * DefMul * dt;
                Gauge = Clamp100(Gauge);
                if (Gauge <= 0f)
                {
                    Result = MashResult.DefenderWins;
                    return;
                }
            }
            if (!RefCounting) return;
            if (timing || Gauge >= Threshold)
            {
                CountProgress += dt / CountSeconds;
                while (CountProgress >= 1f && Result == MashResult.Running)
                {
                    CountProgress -= 1f;
                    Count++;
                    _attemptUsed = false;
                    if (Count >= Counts) Result = MashResult.AttackerWins;
                }
            }
        }

        /// <summary>For bots: a both-hands press with a timing error (seconds) against this count's window.</summary>
        public bool InWindow(float error) => Math.Abs(error) <= Window;
    }

    /// <summary>
    /// Submission. The attacker squeezes (alternate hands) toward 100 against the defender's body
    /// resistance; the defender crawls to the rope (alternate hands). Rope reached → break
    /// (unless no-rope-break); time out → escape.
    /// </summary>
    public sealed class SubmissionBout : FinaleBout
    {
        public float Squeeze { get; private set; }
        public float Crawl { get; private set; }
        public bool RopeBreak { get; private set; }

        protected override void Setup(Match m, FinaleTrigger f, float penalty)
        {
            Squeeze = Clamp100(T.Get("sub.start") - penalty);
            RopeBreak = !m.Rules.NoRopeBreak;
            Crawl = f.NearRope && RopeBreak ? T.Get("sub.nearRopeStart") : 0f;
            TimeLimit = T.Get("sub.seconds");
        }

        public override float AttackerProgress => Squeeze / 100f;

        float CrawlPower => T.Get("sub.crawl") * D * Attacker.CrawlMul;

        protected override void OnPress(Hand h, bool alternated, bool both, bool attackerSide)
        {
            if (!alternated) return;
            if (attackerSide) Squeeze = Clamp100(Squeeze + T.Get("sub.tap") * A * AtkMul);
            else Crawl = Clamp100(Crawl + CrawlPower * DefMul);
            Check();
        }

        protected override void Step(float dt)
        {
            if (!AttackerHuman) Squeeze += AiRate(AtkTeam) * T.Get("sub.tap") * A * AtkMul * dt;
            Squeeze -= T.Get("sub.resist") * D * DefMul * dt;
            if (!DefenderHuman) Crawl += AiRate(DefTeam) * CrawlPower * DefMul * dt;
            Squeeze = Clamp100(Squeeze);
            Crawl = Clamp100(Crawl);
            Check();
        }

        void Check()
        {
            if (Squeeze >= 100f) Result = MashResult.AttackerWins;
            else if (RopeBreak && Crawl >= 100f) Result = MashResult.DefenderWins;
        }
    }

    /// <summary>
    /// Ringout. A needle swings in and out (period ringout.period). Attacker: both hands at the
    /// outer end pushes the defender over; defender: both hands at the inner end pulls back.
    /// 100 = over the rope, 0 = back in, time out = back in.
    /// </summary>
    public sealed class RingoutBout : FinaleBout
    {
        public float Over { get; private set; }
        public float Period { get; private set; }
        int _lastAtkExtreme = -1;
        int _lastDefExtreme = -1;
        int _aiAtkExtreme = -1;
        int _aiDefExtreme = -1;

        protected override void Setup(Match m, FinaleTrigger f, float penalty)
        {
            Over = Clamp100(T.Get("ringout.start") - penalty - f.PushDistance * T.Get("ringout.pushPenalty"));
            Period = T.Get("ringout.period");
            TimeLimit = T.Get("ringout.seconds");
        }

        /// <summary>-1 (inner end) .. 1 (outer end).</summary>
        public float Needle => (float)Math.Sin(2 * Math.PI * Elapsed / Period);
        public float Window => T.Get("ringout.window");
        public override float AttackerProgress => Over / 100f;

        /// <summary>Index and signed time offset of the nearest outer (or inner) extreme.</summary>
        void Nearest(bool outer, out int index, out float error)
        {
            float phase = outer ? 0.25f : 0.75f;
            float k = (float)Math.Round(Elapsed / Period - phase);
            index = (int)k;
            error = Elapsed - (k + phase) * Period;
        }

        protected override void OnPress(Hand h, bool alternated, bool both, bool attackerSide)
        {
            if (!both) return;
            Nearest(attackerSide, out int index, out float err);
            float w = Window;
            if (Math.Abs(err) > w) return;
            if (attackerSide)
            {
                if (index == _lastAtkExtreme) return;
                _lastAtkExtreme = index;
                Over = Clamp100(Over + T.Get("ringout.push") * A * AtkMul * (1f - Math.Abs(err) / w));
            }
            else
            {
                if (index == _lastDefExtreme) return;
                _lastDefExtreme = index;
                Over = Clamp100(Over - T.Get("ringout.pull") * D * DefMul * (1f - Math.Abs(err) / w));
            }
            Check();
        }

        protected override void Step(float dt)
        {
            int outer = (int)Math.Floor(Elapsed / Period - 0.25f);
            int inner = (int)Math.Floor(Elapsed / Period - 0.75f);
            if (!AttackerHuman && outer >= 0 && outer != _aiAtkExtreme)
            {
                _aiAtkExtreme = outer;
                Over = Clamp100(Over + T.Get("ringout.push") * A * AtkMul * AiQuality(AtkTeam));
            }
            if (!DefenderHuman && inner >= 0 && inner != _aiDefExtreme)
            {
                _aiDefExtreme = inner;
                Over = Clamp100(Over - T.Get("ringout.pull") * D * DefMul * AiQuality(DefTeam));
            }
            Check();
        }

        void Check()
        {
            if (Over >= 100f) Result = MashResult.AttackerWins;
            else if (Over <= 0f) Result = MashResult.DefenderWins;
        }

        /// <summary>For bots: seconds to the next outer (attacker) or inner (defender) extreme.</summary>
        public float TimeToNextExtreme(bool outer)
        {
            float phase = outer ? 0.25f : 0.75f;
            float k = (float)Math.Ceiling(Elapsed / Period - phase);
            return (k + phase) * Period - Elapsed;
        }
    }
}
