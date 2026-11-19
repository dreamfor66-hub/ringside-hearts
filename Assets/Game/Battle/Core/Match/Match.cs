using System;
using System.Collections.Generic;

namespace RingsideHearts.Battle.Core
{
    public enum MatchPhase { AwaitAction, AwaitFinale, Ended }

    public enum EndReason { None, Finale, TurnLimit }

    /// <summary>
    /// One match (design v0.2): alternating turns (team 0 acts, then team 1, then turn end),
    /// tags, shots, specials, appeals, finales and the result. Both teams go through the same
    /// calls; who is human only matters to the screen and to finale bouts (design 4.1).
    /// </summary>
    public sealed class Match
    {
        Tuning _t;
        RingSim _sim;
        RingSim _cageSim;
        bool _tagUsed;
        List<MatchNote> _notes = new List<MatchNote>();

        public MatchRules Rules { get; }
        public TeamState[] Teams { get; }
        public Field Field { get; } = new Field();
        public int Turn { get; private set; } = 1;
        public int TurnLimit { get; }
        public MatchPhase Phase { get; private set; } = MatchPhase.AwaitAction;
        public int ActingTeam { get; private set; }
        public int Winner { get; private set; } = -1;
        public EndReason EndReason { get; private set; }
        public FinaleTrigger PendingFinale { get; private set; }
        public FinalePlan? WinningPlan { get; private set; }
        public Rng Rng { get; }
        public int Seed { get; }
        public Tuning Tuning => _t;
        public IReadOnlyList<MatchNote> Notes => _notes;
        public RingSim Sim => _sim;
        public RingSim SimForRules => Rules.Cage ? _cageSim : _sim;
        public int Actions { get; private set; }

        public Match(MatchRules rules, TeamSetup a, TeamSetup b, IReadOnlyDictionary<string, CharacterData> chars, Tuning t, int seed)
        {
            Rules = rules;
            Seed = seed;
            Rng = new Rng(seed);
            TurnLimit = rules.TurnLimit;
            SetTuning(t);
            Teams = new[] { MakeTeam(a, 0, chars), MakeTeam(b, 1, chars) };
            ApplySecondSkills();
            PlaceStart();
            BeginAction();
        }

        // ---------- setup ----------

        TeamState MakeTeam(TeamSetup setup, int index, IReadOnlyDictionary<string, CharacterData> chars)
        {
            var team = new TeamState { Index = index, Setup = setup, Heat = _t.Get("heat.start") };
            int slots = Rules.Tornado ? Math.Min(2, setup.Slots.Length) : setup.Slots.Length;
            for (int i = 0; i < slots; i++)
            {
                CharacterData data = Find(chars, setup.Slots[i], setup.Id);
                CharacterData second = i < setup.Seconds.Length && setup.Seconds[i].Length > 0 ? Find(chars, setup.Seconds[i], setup.Id) : null;
                team.Members.Add(new Fighter(data, second, setup, index, i, _t.Get("second.share")));
            }
            if (Rules.SubmissionOnly) team.Plan = FinalePlan.Submission;
            return team;
        }

        static CharacterData Find(IReadOnlyDictionary<string, CharacterData> chars, string id, string team)
        {
            if (!chars.TryGetValue(id, out CharacterData d)) throw new ArgumentException($"team '{team}': character '{id}' not found");
            return d;
        }

        void ApplySecondSkills()
        {
            foreach (TeamState team in Teams)
            {
                TeamState enemy = Teams[1 - team.Index];
                foreach (Fighter f in team.Members)
                {
                    if (f.Second == null) continue;
                    EffectBlock e = EffectBlock.Parse(f.Second.SecondSkill, f.Second.Id);
                    if (e == null) continue;
                    if (e.When != "always")
                    {
                        team.Triggers.Add(new KeyValuePair<Fighter, EffectBlock>(f, e));
                        continue;
                    }
                    if (e.To == "partner" || e.To == "self") ApplyStat(f, e);
                    else if (e.To == "team") foreach (Fighter m in team.Members) ApplyStat(m, e);
                    else if (e.To == "enemy_active" || e.To == "enemy_second") foreach (Fighter m in enemy.Members) ApplyStat(m, e);
                }
            }
        }

        static void ApplyStat(Fighter f, EffectBlock e)
        {
            float Mul(float v) => e.Op == '*' ? v * e.Value : v + e.Value;
            switch (e.Stat)
            {
                case "appeal_heat": f.AppealHeatMul = Mul(f.AppealHeatMul); break;
                case "max_stamina": f.MaxStamina = Mul(f.MaxStamina); f.Stamina = f.MaxStamina; break;
                case "down_recovery": f.DownRecoveryMul = Mul(f.DownRecoveryMul); break;
                case "friction": f.FrictionMul = Mul(f.FrictionMul); break;
                case "finale_power": f.FinalePowerMul = Mul(f.FinalePowerMul); break;
                case "lariat": f.LariatMul = Mul(f.LariatMul); break;
                case "submission_resist": f.CrawlMul = Mul(f.CrawlMul); break;
                case "intervention": f.InterventionMul = Mul(f.InterventionMul); break;
                case "intervention_time": f.InterventionTimeAdd = Mul(f.InterventionTimeAdd); break;
                case "power": f.FirstShotPowerMul = Mul(f.FirstShotPowerMul); break;
            }
        }

        void PlaceStart()
        {
            float size = _t.Get("ring.size");
            float ratio = _t.Get("match.startRatio");
            int inRing = Rules.InRingPerTeam;
            for (int ti = 0; ti < 2; ti++)
            {
                TeamState team = Teams[ti];
                float y = ti == 0 ? size * ratio : size * (1f - ratio);
                for (int i = 0; i < team.Members.Count; i++)
                {
                    Fighter f = team.Members[i];
                    if (i < inRing)
                    {
                        f.InRing = true;
                        team.InRing.Add(f);
                        float x = inRing == 1 ? size * 0.5f : size * (i == 0 ? 0.33f : 0.67f);
                        f.Position = new Vec2(x, y);
                    }
                    else
                    {
                        int k = i - inRing;
                        float x = size * (k == 0 ? 0.3f : 0.7f);
                        f.Position = ApronPoint(new Vec2(x, ti == 0 ? 0f : size), ti);
                    }
                }
            }
            Field.Referee = new Vec2(size * 0.85f, size * 0.5f);
            if (Rules.Hardcore)
            {
                Field.Objects.Add(new FieldObject { Kind = ObjectKind.Table, Position = new Vec2(size * 0.5f, size * 0.5f), Radius = _t.Get("object.tableRadius") });
                Field.Objects.Add(new FieldObject { Kind = ObjectKind.Chair, Position = new Vec2(size * 0.2f, size * 0.62f), Radius = _t.Get("object.chairRadius") });
                Field.Objects.Add(new FieldObject { Kind = ObjectKind.Chair, Position = new Vec2(size * 0.8f, size * 0.38f), Radius = _t.Get("object.chairRadius") });
            }
        }

        /// <summary>Apron spot just outside the rope nearest to <paramref name="p"/>, kept clear of other apron fighters.</summary>
        Vec2 ApronPoint(Vec2 p, int team)
        {
            float size = _t.Get("ring.size");
            float off = _t.Get("apron.offset");
            float dl = p.X, dr = size - p.X, db = p.Y, dt = size - p.Y;
            float min = Math.Min(Math.Min(dl, dr), Math.Min(db, dt));
            bool vertical = min == dl || min == dr;
            float along = Clamp(vertical ? p.Y : p.X, 1.2f, size - 1.2f);
            Vec2 spot = vertical ? new Vec2(min == dl ? -off : size + off, along) : new Vec2(along, min == db ? -off : size + off);
            for (int tries = 0; tries < 8; tries++)
            {
                bool clash = false;
                foreach (TeamState t in Teams ?? new TeamState[0])
                    foreach (Fighter f in t.Members)
                        if (!f.InRing && (f.Position - spot).Length < 1.3f) clash = true;
                if (!clash) break;
                along = along + 1.4f > size - 1.2f ? along - 2.8f : along + 1.4f;
                spot = vertical ? new Vec2(spot.X, along) : new Vec2(along, spot.Y);
            }
            return spot;
        }

        static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;

        public void SetTuning(Tuning t)
        {
            _t = t;
            _sim = new RingSim(t);
            Tuning cage = t.Clone();
            cage.Set("rope.restitution", t.Get("cage.restitution"));
            cage.Set("rope.maxStretch", t.Get("cage.stretch"));
            _cageSim = new RingSim(cage);
        }

        // ---------- queries ----------

        public TeamState Acting => Teams[ActingTeam];
        public TeamState Opponent => Teams[1 - ActingTeam];

        /// <summary>The fighter who acts now (tornado: in-ring fighters take turns).</summary>
        public Fighter Actor
        {
            get
            {
                List<Fighter> ring = Acting.InRing;
                if (ring.Count == 0) return null;
                Fighter f = ring[Acting.ActorCursor % ring.Count];
                if (f.IsDown)
                    foreach (Fighter g in ring)
                        if (!g.IsDown) return g;
                return f;
            }
        }

        public bool CanShoot => Phase == MatchPhase.AwaitAction && Actor != null && !Actor.IsDown;
        public bool CanSpecial => CanShoot && Actor.SpecialReady;
        public bool JointReady => CanShoot && Acting.Chain >= 3 && Actor.Data.Role == ChainRole.Finisher;
        public bool CanAppeal => CanShoot;
        public bool CanHold => Phase == MatchPhase.AwaitAction && Actor != null && Actor.IsDown;

        public bool CanTag(Fighter incoming) =>
            Phase == MatchPhase.AwaitAction && Rules.TagAllowed && !_tagUsed && incoming.Team == ActingTeam && incoming.CanTagIn && Actor != null &&
            (!Actor.IsDown || ShotResolver.IsNearRope(Actor.Position, _t));

        /// <summary>A downed fighter can only reach a tag while lying near a rope (crawls to the corner).</summary>
        public bool DownedCanReachTag => Actor != null && Actor.IsDown && ShotResolver.IsNearRope(Actor.Position, _t);

        public bool PlanAllowed(FinalePlan p) =>
            p == FinalePlan.Submission || (p == FinalePlan.Pinfall && Rules.PinfallAllowed) || (p == FinalePlan.Ringout && Rules.RingoutAllowed);

        public IEnumerable<Fighter> AllFighters()
        {
            foreach (TeamState t in Teams)
                foreach (Fighter f in t.Members) yield return f;
        }

        // ---------- actions ----------

        void Require(MatchPhase p)
        {
            if (Phase == MatchPhase.Ended) throw new InvalidOperationException("the match has ended");
            if (Phase != p) throw new InvalidOperationException($"expected {p}, now {Phase}");
        }

        public void SetPlan(int team, FinalePlan plan)
        {
            if (Phase == MatchPhase.Ended) throw new InvalidOperationException("the match has ended");
            if (!PlanAllowed(plan)) throw new InvalidOperationException($"{plan} is not allowed in this match");
            Teams[team].Plan = plan;
        }

        /// <summary>Tag (free, once per action): the acting in-ring fighter goes to the nearest apron spot.</summary>
        public void Tag(Fighter incoming)
        {
            Require(MatchPhase.AwaitAction);
            if (!CanTag(incoming)) throw new InvalidOperationException("this tag is not allowed now");
            Fighter outgoing = Actor;
            TeamState team = Acting;
            Vec2 apronSpot = incoming.Position;
            incoming.Position = new Vec2(-99f, -99f); // its apron spot is free now
            Vec2 outSpot = ApronPoint(outgoing.Position, team.Index);
            outgoing.InRing = false;
            outgoing.OnTurnbuckle = false;
            team.InRing.Remove(outgoing);
            outgoing.Position = outSpot;
            outgoing.TagLock = (int)_t.Get("tag.lockTurns");
            incoming.InRing = true;
            incoming.Position = EntryPoint(apronSpot);
            team.InRing.Insert(0, incoming);
            team.ActorCursor = 0;
            _tagUsed = true;
            team.Tags++;
            _notes.Add(new MatchNote { Kind = NoteKind.TagIn, Fighter = incoming, Team = team.Index });
        }

        Vec2 EntryPoint(Vec2 apron)
        {
            float size = _t.Get("ring.size");
            float r = _t.Get("unit.radius") + 0.1f;
            var p = new Vec2(Clamp(apron.X, r, size - r), Clamp(apron.Y, r, size - r));
            foreach (Fighter f in AllFighters())
            {
                if (!f.InRing) continue;
                Vec2 d = p - f.Position;
                if (d.Length < 1.5f)
                {
                    Vec2 toCenter = (new Vec2(size * 0.5f, size * 0.5f) - p).Normalized;
                    p += toCenter * 1.6f;
                }
            }
            return p;
        }

        public ShotKind KindFor(bool special) => JointReady ? ShotKind.Joint : special && Actor.SpecialReady ? ShotKind.Special : ShotKind.Normal;

        public ShotOutcome Preview(Vec2 velocity, bool special = false)
        {
            Require(MatchPhase.AwaitAction);
            if (!CanShoot) throw new InvalidOperationException("the acting fighter cannot shoot");
            return ShotResolver.Resolve(this, Actor, velocity, KindFor(special));
        }

        public ShotOutcome Shoot(Vec2 velocity, bool special = false)
        {
            Require(MatchPhase.AwaitAction);
            if (!CanShoot) throw new InvalidOperationException("the acting fighter cannot shoot (down)");
            ShotKind kind = KindFor(special);
            Fighter shooter = Actor;
            ShotOutcome o = ShotResolver.Resolve(this, shooter, velocity, kind);
            Apply(o, shooter, kind);
            Actions++;
            if (o.Finale != null)
            {
                if (o.Finale.Plan == FinalePlan.Ringout)
                {
                    // the attacker drives the defender into the ropes
                    o.Finale.Defender.Position = o.Finale.RopePoint;
                    shooter.Position = o.Finale.RopePoint - o.Finale.PushDir * (2f * _t.Get("unit.radius") + 0.05f);
                }
                PendingFinale = o.Finale;
                Teams[o.Finale.AttackerTeam].FinaleTries++;
                Phase = MatchPhase.AwaitFinale;
                return o;
            }
            EndAction();
            return o;
        }

        void Apply(ShotOutcome o, Fighter shooter, ShotKind kind)
        {
            TeamState team = Teams[shooter.Team];
            for (int i = 0; i < o.Owners.Count; i++)
            {
                Vec2 p = o.Shot.FinalPosition(i);
                if (o.Owners[i] is Fighter f) f.Position = p;
                else if (ReferenceEquals(o.Owners[i], ShotResolver.RefereeOwner)) Field.Referee = p;
            }
            foreach (var kv in o.StaminaAfter) kv.Key.Stamina = kv.Value;
            foreach (var kv in o.BodyAfter) kv.Key.Body = kv.Value;
            foreach (Fighter d in o.Downed)
            {
                d.IsDown = true;
                d.Downs++;
                d.DownedBySpecial = kind != ShotKind.Normal;
                d.OnTurnbuckle = false;
                FireTriggers(team, "on:partner_causes_down", shooter);
            }
            foreach (Fighter k in o.KnockedOff)
            {
                k.Fallen = (int)_t.Get("apron.fallTurns");
                _notes.Add(new MatchNote { Kind = NoteKind.Fell, Fighter = k, Team = k.Team });
            }
            foreach (FieldObject b in o.Broken) b.Broken = true;
            if (o.RefDown) Field.RefereeDown = (int)_t.Get("ref.downTurns");
            shooter.Stamina = Math.Min(shooter.MaxStamina, shooter.Stamina + o.ShooterHeal);
            shooter.OnTurnbuckle = o.EndsOnTurnbuckle;
            shooter.FirstShotDone = true;
            team.Assists += o.AssistsUsed.Count;

            float heat = o.HeatGain;
            if (o.ShooterHit)
            {
                team.HitThisTurn = true;
                ChainHit(team, shooter);
            }
            if (o.Flashy) team.SkipNextDecay = true;
            if (kind == ShotKind.Special)
            {
                shooter.SpecialCount = shooter.SpecialTurns;
                team.Specials++;
            }
            else if (kind == ShotKind.Joint)
            {
                team.Chain = 0;
                team.Joints++;
            }
            AddHeat(team, heat);
        }

        void ChainHit(TeamState team, Fighter shooter)
        {
            if (team.Chain >= 3 || (int)shooter.Data.Role != team.Chain) return;
            team.Chain++;
            AddHeat(team, _t.Get("heat.chain"));
            _notes.Add(new MatchNote { Kind = NoteKind.Chain, Fighter = shooter, Team = team.Index, Amount = team.Chain });
        }

        void FireTriggers(TeamState team, string when, Fighter cause)
        {
            foreach (var kv in team.Triggers)
            {
                EffectBlock e = kv.Value;
                if (e.When != when) continue;
                if (when.StartsWith("on:partner") && kv.Key != cause) continue;
                if (e.Stat == "heat") AddHeat(team, e.Op == '+' ? e.Value : 0f);
            }
        }

        void AddHeat(TeamState team, float amount) => team.Heat = Clamp(team.Heat + amount, 0f, 100f);

        public float AppealAmount()
        {
            Fighter f = Actor;
            float amount = f.Data.AppealHeat * f.AppealHeatMul;
            if (f.Data.Faction == Faction.Heel) amount *= _t.Get("appeal.heelMul");
            if (Acting.LastAppealer == f) amount *= _t.Get("appeal.repeatMul");
            return amount;
        }

        public void Appeal()
        {
            Require(MatchPhase.AwaitAction);
            if (!CanAppeal) throw new InvalidOperationException("cannot appeal now");
            Fighter f = Actor;
            TeamState team = Acting;
            float amount = AppealAmount();
            AddHeat(team, amount);
            if (f.Data.Faction == Faction.Heel)
            {
                team.Warnings++;
                _notes.Add(new MatchNote { Kind = NoteKind.Warning, Fighter = f, Team = team.Index, Amount = team.Warnings });
            }
            // the crowd fires up the appealer: special countdown −appeal.specialCharge
            f.SpecialCount = Math.Max(0, f.SpecialCount - (int)_t.Get("appeal.specialCharge"));
            team.LastAppealer = f;
            team.Appeals++;
            FireTriggersAppeal(team, f);
            _notes.Add(new MatchNote { Kind = NoteKind.Appeal, Fighter = f, Team = team.Index, Amount = amount });
            Actions++;
            EndAction();
        }

        void FireTriggersAppeal(TeamState team, Fighter appealer)
        {
            foreach (var kv in team.Triggers)
                if (kv.Value.When == "on:partner_appeal" && kv.Key == appealer && kv.Value.Stat == "heat")
                    AddHeat(team, kv.Value.Value);
        }

        public void Hold()
        {
            Require(MatchPhase.AwaitAction);
            if (!CanHold) throw new InvalidOperationException("Hold is only for a downed fighter");
            Actions++;
            EndAction();
        }

        // ---------- finale ----------

        public FinaleBout CreateBout()
        {
            Require(MatchPhase.AwaitFinale);
            return FinaleBout.Create(this, PendingFinale);
        }

        public void ResolveFinale(bool attackerWon)
        {
            Require(MatchPhase.AwaitFinale);
            FinaleTrigger f = PendingFinale;
            PendingFinale = null;
            if (attackerWon)
            {
                Winner = f.AttackerTeam;
                EndReason = EndReason.Finale;
                WinningPlan = f.Plan;
                Phase = MatchPhase.Ended;
                return;
            }
            Fighter def = f.Defender;
            def.IsDown = false;
            def.DownedBySpecial = false;
            def.Stamina = Math.Max(def.Stamina, def.MaxStamina * _t.Get("finale.failWakeRatio"));
            AddHeat(Teams[f.AttackerTeam], _t.Get("heat.finaleFail"));
            _notes.Add(new MatchNote { Kind = NoteKind.Woke, Fighter = def, Team = def.Team, Amount = def.Stamina });
            EndAction();
        }

        // ---------- turn order ----------

        void EndAction()
        {
            Phase = MatchPhase.AwaitAction;
            Acting.ActorCursor++;
            if (ActingTeam == 0)
            {
                ActingTeam = 1;
                BeginAction();
                return;
            }
            TurnEnd();
            if (Phase == MatchPhase.Ended) return;
            ActingTeam = 0;
            BeginAction();
        }

        void BeginAction()
        {
            _tagUsed = false;
        }

        /// <summary>Clears the notes before the screen shows the next action's results.</summary>
        public void ClearNotes() => _notes = new List<MatchNote>();

        void TurnEnd()
        {
            float wake = _t.Get("down.wakeRatio");
            foreach (TeamState team in Teams)
            {
                TeamState enemy = Teams[1 - team.Index];
                foreach (Fighter f in team.Members)
                {
                    if (f.IsDown)
                    {
                        float amount = f.MaxStamina * (_t.Get("down.recoverBase") + f.Guts * _t.Get("down.recoverPerGuts")) * f.DownRecoveryMul;
                        f.Stamina = Math.Min(f.MaxStamina, f.Stamina + amount);
                        _notes.Add(new MatchNote { Kind = NoteKind.Recovered, Fighter = f, Team = team.Index, Amount = amount });
                        if (f.Stamina >= f.MaxStamina * wake - 1e-3f)
                        {
                            f.IsDown = false;
                            f.DownedBySpecial = false;
                            _notes.Add(new MatchNote { Kind = NoteKind.Woke, Fighter = f, Team = team.Index });
                        }
                    }
                    else if (!f.InRing)
                    {
                        float amount = f.MaxStamina * _t.Get("apron.recover");
                        f.Stamina = Math.Min(f.MaxStamina, f.Stamina + amount);
                    }
                    bool wasReady = f.SpecialReady;
                    if (f.SpecialCount > 0) f.SpecialCount--;
                    if (!wasReady && f.SpecialReady) _notes.Add(new MatchNote { Kind = NoteKind.SpecialReady, Fighter = f, Team = team.Index });
                    if (f.TagLock > 0) f.TagLock--;
                    if (f.Fallen > 0)
                    {
                        f.Fallen--;
                        if (f.Fallen == 0) _notes.Add(new MatchNote { Kind = NoteKind.Returned, Fighter = f, Team = team.Index });
                    }
                }

                // heat decay (battle doc 9.2)
                if (team.SkipNextDecay) team.SkipNextDecay = false;
                else
                {
                    float decay = _t.Get("heat.decay");
                    if (!team.HitThisTurn)
                    {
                        bool cancel = false;
                        foreach (var kv in team.Triggers)
                            if (kv.Value.When == "once:no_hit_turn" && !team.CancelHeatPenaltyUsed) cancel = true;
                        if (cancel) team.CancelHeatPenaltyUsed = true;
                        else decay += _t.Get("heat.noHit");
                    }
                    AddHeat(team, -decay);
                    _notes.Add(new MatchNote { Kind = NoteKind.HeatDecay, Team = team.Index, Amount = decay });
                }
                team.HitThisTurn = false;
                _ = enemy;
            }

            if (Field.RefereeDown > 0)
            {
                Field.RefereeDown--;
                if (Field.RefereeDown == 0) _notes.Add(new MatchNote { Kind = NoteKind.RefUp, Team = -1 });
            }
            WalkReferee();

            Turn++;
            if (Turn > TurnLimit)
            {
                Winner = 1;
                EndReason = EndReason.TurnLimit;
                Phase = MatchPhase.Ended;
                return;
            }
            _notes.Add(new MatchNote { Kind = NoteKind.TurnStart, Team = -1, Amount = Turn });
        }

        /// <summary>The referee walks toward a spot beside the in-ring fighters (design v0.2 3).</summary>
        void WalkReferee()
        {
            if (Field.RefereeDown > 0) return;
            float size = _t.Get("ring.size");
            Vec2 sum = Vec2.Zero;
            int n = 0;
            foreach (Fighter f in AllFighters())
                if (f.InRing) { sum += f.Position; n++; }
            if (n == 0) return;
            Vec2 mid = sum / n;
            var side = new Vec2(mid.X < size * 0.5f ? 1f : -1f, 0f);
            Vec2 goal = mid + side * 2.2f;
            goal = new Vec2(Clamp(goal.X, 1f, size - 1f), Clamp(goal.Y, 1f, size - 1f));
            foreach (Fighter f in AllFighters())
                if (f.InRing && (goal - f.Position).Length < 1.4f) goal += side * 1.2f;
            goal = new Vec2(Clamp(goal.X, 1f, size - 1f), Clamp(goal.Y, 1f, size - 1f));
            Vec2 step = goal - Field.Referee;
            float max = _t.Get("ref.walk");
            if (step.Length > max) step = step.Normalized * max;
            Field.Referee += step;
        }

        /// <summary>Debug menu helper: set a team's heat.</summary>
        public void DebugSetHeat(int team, float heat) => Teams[team].Heat = Clamp(heat, 0f, 100f);
    }
}
