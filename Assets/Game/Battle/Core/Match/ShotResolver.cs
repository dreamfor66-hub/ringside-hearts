using System;
using System.Collections.Generic;

namespace RingsideHearts.Battle.Core
{
    public enum HitKind { Direct, Landing, Assist, Object, Cage, Bowling }

    /// <summary>Damage dealt to one fighter during a shot, at the moment it happened.</summary>
    public sealed class HitInfo
    {
        public float Time;
        public Fighter Target;
        public Fighter Source;
        public float Damage;
        public float BodyDamage;
        public string Tags = "";
        public bool CausedDown;
        public HitKind Kind;
        public Vec2 Position;
    }

    public enum FieldEventKind { Assist, Knockoff, RefDown, ObjectBreak, Weapon, Takeoff, Landing, Grab, Dive, NoCount, Slam, PassThrough }

    public sealed class FieldEvent
    {
        public float Time;
        public FieldEventKind Kind;
        public Fighter Fighter;
        public FieldObject Object;
        public Vec2 Position;
        public string Text = "";
    }

    /// <summary>A finale this shot set off; the shot is cut at <see cref="Time"/>.</summary>
    public sealed class FinaleTrigger
    {
        public int AttackerTeam;
        public Fighter Attacker;
        public Fighter Defender;
        public FinalePlan Plan;
        public bool NearRope;
        public float Time;
        public Vec2 Position;
        /// <summary>Ringout: where the defender is pressed against the rope, the push direction and distance.</summary>
        public Vec2 RopePoint;
        public Vec2 PushDir;
        public float PushDistance;
        /// <summary>Attacker's position when it fired.</summary>
        public Vec2 AttackerAt;
    }

    public sealed class ShotOutcome
    {
        public ShotResult Shot;
        /// <summary>What each simulated body is: a <see cref="Fighter"/>, the string "ref", or a <see cref="FieldObject"/>.</summary>
        public readonly List<object> Owners = new List<object>();
        public readonly List<HitInfo> Hits = new List<HitInfo>();
        public readonly List<FieldEvent> Events = new List<FieldEvent>();
        public FinaleTrigger Finale;
        public ShotKind Kind;
        public Fighter Shooter;
        public readonly Dictionary<Fighter, float> StaminaAfter = new Dictionary<Fighter, float>();
        public readonly Dictionary<Fighter, float> BodyAfter = new Dictionary<Fighter, float>();
        public readonly HashSet<Fighter> Downed = new HashSet<Fighter>();
        public readonly List<Fighter> KnockedOff = new List<Fighter>();
        public readonly List<FieldObject> Broken = new List<FieldObject>();
        public readonly List<Fighter> AssistsUsed = new List<Fighter>();
        public bool RefDown;
        public float HeatGain;
        public bool Flashy;
        public bool ShooterHit;
        public int LariatCount;
        public float TotalDamage;
        public float AssistDamage;
        public float ShooterHeal;
        public bool EndsOnTurnbuckle;
        public bool DownedBySpecial;

        public int BodyIndex(object owner) => Owners.IndexOf(owner);
        public Vec2 FinalPosition(object owner) => Shot.FinalPosition(BodyIndex(owner));
    }

    /// <summary>
    /// Works out everything one shot does (design v0.2 3–5) without changing the match.
    /// Body 0 is always the shooter.
    /// </summary>
    public static class ShotResolver
    {
        public const string RefereeOwner = "ref";

        sealed class Ctx
        {
            public Match M;
            public Tuning T;
            public Fighter Shooter;
            public TeamState Team;
            public ShotKind Kind;
            public ShotOutcome O;
            public readonly Dictionary<Fighter, float> Stamina = new Dictionary<Fighter, float>();
            public readonly Dictionary<Fighter, float> Body = new Dictionary<Fighter, float>();
            public readonly HashSet<Fighter> HitBefore = new HashSet<Fighter>();
            public readonly HashSet<Fighter> LariatArmed = new HashSet<Fighter>();
            public Fighter GrabbedTarget;
            public readonly HashSet<Fighter> CageHurt = new HashSet<Fighter>();
            /// <summary>Fighters already down when the shot was fired: touching them is a finale, not damage.</summary>
            public readonly HashSet<Fighter> StartDown = new HashSet<Fighter>();
            public int Bounces;
            public int TotalBounces;
            public bool DiveReady;
            public bool WeaponReady;
            public bool FirstShotBonus;
            public bool RefHit;
        }

        public static ShotOutcome Resolve(Match m, Fighter shooter, Vec2 velocity, ShotKind kind)
        {
            Tuning t = m.Tuning;
            MatchRules rules = m.Rules;
            float r = t.Get("unit.radius");
            var c = new Ctx { M = m, T = t, Shooter = shooter, Team = m.Teams[shooter.Team], Kind = kind };
            var o = new ShotOutcome { Kind = kind, Shooter = shooter };
            c.O = o;
            c.DiveReady = shooter.OnTurnbuckle;
            c.FirstShotBonus = !shooter.FirstShotDone && shooter.FirstShotPowerMul != 1f;

            var bodies = new List<Body>();
            bodies.Add(ShooterBody(m, shooter, kind, r));
            o.Owners.Add(shooter);
            foreach (TeamState team in m.Teams)
                foreach (Fighter f in team.InRing)
                {
                    if (f == shooter) continue;
                    bodies.Add(new Body(f.Position, r) { Group = f.Team });
                    o.Owners.Add(f);
                }
            bodies.Add(new Body(m.Field.Referee, t.Get("ref.radius"), m.Field.RefereeDown == 0));
            o.Owners.Add(RefereeOwner);
            foreach (FieldObject obj in m.Field.Objects)
            {
                if (obj.Broken) continue;
                bodies.Add(new Body(obj.Position, obj.Radius) { Fixed = true });
                o.Owners.Add(obj);
            }
            foreach (TeamState team in m.Teams)
                foreach (Fighter f in team.Members)
                {
                    c.Stamina[f] = f.Stamina;
                    c.Body[f] = f.Body;
                    if (f.IsDown) c.StartDown.Add(f);
                }

            ShotResult shot = m.SimForRules.Simulate(bodies, 0, velocity);
            o.Shot = shot;

            foreach (Moment mo in Moments(m, shot, bodies, r))
            {
                if (o.Finale != null) break;
                if (mo.Apron != null) Touch(c, mo.Apron, mo.Time, mo.Frame);
                else Event(c, mo.Ev, mo.Frame);
            }

            if (o.Finale != null) o.Shot = Truncate(shot, o.Finale.Time);

            foreach (var kv in c.Stamina) o.StaminaAfter[kv.Key] = kv.Value;
            foreach (var kv in c.Body) o.BodyAfter[kv.Key] = kv.Value;
            o.EndsOnTurnbuckle = o.Finale == null && NearCorner(o.Shot.FinalPosition(0), t);
            if (o.ShooterHit && kind != ShotKind.Normal) o.DownedBySpecial = o.Downed.Count > 0;
            return o;
        }

        static Body ShooterBody(Match m, Fighter s, ShotKind kind, float r)
        {
            Tuning t = m.Tuning;
            bool special = kind != ShotKind.Normal;
            var b = new Body(s.Position, r) { Group = s.Team, FrictionMul = s.FrictionMul, ThrowSpeed = t.Get("type.throwSpeed") };
            if (special && s.Data.SpecialAtRope) b.RopeBoost = t.Get("special.ropeBoost");
            switch (s.Data.Type)
            {
                case FighterType.Striker:
                    b.KnockMul = special ? t.Get("type.strikerKnockSpecial") : t.Get("type.strikerKnock");
                    break;
                case FighterType.Grappler:
                    b.Grab = true;
                    float size = t.Get("ring.size");
                    b.GrabTarget = new Vec2(size * 0.5f, size * 0.5f);
                    break;
                case FighterType.Stampeder:
                    b.PassThrough = true;
                    b.PassKeep = special ? 1f : t.Get("type.stampKeep");
                    b.PassKnock = t.Get("type.stampKnock");
                    if (special) b.FrictionMul *= 0.5f;
                    break;
                case FighterType.Highflier:
                    b.Flier = true;
                    b.FlyEveryRope = special;
                    b.FlightDistance = t.Get("type.flyDistance");
                    break;
            }
            return b;
        }

        struct Moment
        {
            public float Time;
            public int Order;
            public SimEvent Ev;
            public Fighter Apron;
            public int Frame;
        }

        /// <summary>Simulation events plus shooter-touches-apron moments, in time order.</summary>
        static List<Moment> Moments(Match m, ShotResult shot, List<Body> bodies, float r)
        {
            var list = new List<Moment>();
            for (int i = 0; i < shot.Events.Count; i++)
            {
                SimEvent e = shot.Events[i];
                list.Add(new Moment { Time = e.Time, Order = i, Ev = e, Frame = FrameAt(shot, e.Time) });
            }
            if (!m.Rules.Cage)
            {
                float reach = r + m.Tuning.Get("apron.radius") + 0.05f;
                foreach (TeamState team in m.Teams)
                    foreach (Fighter f in team.Members)
                    {
                        if (f.InRing || f.Fallen > 0) continue;
                        for (int k = 1; k < shot.Frames.Count; k++)
                        {
                            if ((shot.Frames[k][0] - f.Position).Length > reach) continue;
                            list.Add(new Moment { Time = k * shot.Dt, Order = -1, Apron = f, Frame = k });
                            break;
                        }
                    }
            }
            list.Sort((a, b) => a.Time != b.Time ? a.Time.CompareTo(b.Time) : a.Order.CompareTo(b.Order));
            return list;
        }

        static int FrameAt(ShotResult r, float time)
        {
            int i = (int)Math.Round(time / r.Dt);
            return Math.Max(0, Math.Min(r.Frames.Count - 1, i));
        }

        static Vec2 PosAt(Ctx c, object owner, int frame) => c.O.Shot.Frames[frame][c.O.Owners.IndexOf(owner)];

        static void Event(Ctx c, SimEvent e, int frame)
        {
            ShotOutcome o = c.O;
            object a = e.Body >= 0 && e.Body < o.Owners.Count ? o.Owners[e.Body] : null;
            object b = e.Other >= 0 && e.Other < o.Owners.Count ? o.Owners[e.Other] : null;
            Tuning t = c.T;

            switch (e.Type)
            {
                case SimEventType.RopeBounce:
                    if (a == c.Shooter)
                    {
                        c.Bounces++;
                        c.TotalBounces++;
                        if (c.Shooter.Data.Type == FighterType.Showman) o.HeatGain += t.Get("heat.showmanBounce");
                    }
                    else if (a is Fighter f && f.Team != c.Shooter.Team)
                    {
                        if (c.HitBefore.Contains(f)) c.LariatArmed.Add(f);
                        if (c.M.Rules.Cage && c.HitBefore.Contains(f) && !c.CageHurt.Contains(f) && !c.StartDown.Contains(f))
                        {
                            c.CageHurt.Add(f);
                            Damage(c, f, null, t.Get("cage.damage"), HitKind.Cage, "철망", e.Time, PosAt(c, f, frame));
                        }
                    }
                    break;

                case SimEventType.Slam:
                    if (a is Fighter slammed && slammed == c.GrabbedTarget)
                    {
                        o.Events.Add(new FieldEvent { Time = e.Time, Kind = FieldEventKind.Slam, Fighter = slammed, Position = e.Position, Text = "슬램!" });
                        Direct(c, slammed, e.Time, e.Position, t.Get("type.grabMul"), "슬램", HitKind.Direct);
                        foreach (FieldObject piece in c.M.Field.Objects)
                        {
                            if (piece.Broken || o.Broken.Contains(piece)) continue;
                            if ((piece.Position - e.Position).Length > piece.Radius + t.Get("unit.radius")) continue;
                            o.Broken.Add(piece);
                            o.Events.Add(new FieldEvent { Time = e.Time, Kind = FieldEventKind.ObjectBreak, Object = piece, Position = piece.Position, Text = piece.Name + "에 내리꽂기!" });
                            Damage(c, slammed, null, t.Get(piece.Kind == ObjectKind.Table ? "object.tableDamage" : "object.chairDamage"), HitKind.Object, piece.Name, e.Time, piece.Position);
                        }
                    }
                    break;

                case SimEventType.Takeoff:
                    if (a == c.Shooter) o.Events.Add(new FieldEvent { Time = e.Time, Kind = FieldEventKind.Takeoff, Fighter = c.Shooter, Position = e.Position });
                    break;

                case SimEventType.Landing:
                    if (a != c.Shooter) break;
                    o.Flashy = true;
                    o.HeatGain += t.Get("heat.aerial");
                    o.Events.Add(new FieldEvent { Time = e.Time, Kind = FieldEventKind.Landing, Fighter = c.Shooter, Position = e.Position });
                    float radius = t.Get("type.landRadius") * (c.Kind != ShotKind.Normal ? 1.5f : 1f);
                    foreach (Fighter target in Opponents(c))
                    {
                        Vec2 p = PosAt(c, target, frame);
                        if ((p - e.Position).Length > radius) continue;
                        if (c.StartDown.Contains(target)) { ContactDown(c, target, e.Time, p); if (o.Finale != null) return; continue; }
                        Direct(c, target, e.Time, p, t.Get("type.landMul"), "공중기", HitKind.Landing);
                    }
                    break;

                case SimEventType.BodyHit:
                    if (a == null || b == null) break;
                    if ((ReferenceEquals(a, RefereeOwner) || ReferenceEquals(b, RefereeOwner)) && !c.RefHit && c.M.Field.RefereeDown == 0)
                    {
                        c.RefHit = true;
                        o.RefDown = true;
                        o.Events.Add(new FieldEvent { Time = e.Time, Kind = FieldEventKind.RefDown, Position = e.Position, Text = "심판 다운!" });
                        break;
                    }
                    if (b is FieldObject obj)
                    {
                        if (o.Broken.Contains(obj)) break;
                        if (a == c.Shooter && !c.WeaponReady)
                        {
                            c.WeaponReady = true;
                            o.Broken.Add(obj);
                            o.Events.Add(new FieldEvent { Time = e.Time, Kind = FieldEventKind.Weapon, Object = obj, Position = obj.Position, Text = obj.Name + "를 집었다!" });
                        }
                        else if (a is Fighter knocked && knocked.Team != c.Shooter.Team && c.HitBefore.Contains(knocked))
                        {
                            o.Broken.Add(obj);
                            o.Events.Add(new FieldEvent { Time = e.Time, Kind = FieldEventKind.ObjectBreak, Object = obj, Position = obj.Position, Text = obj.Name + " 박살!" });
                            Damage(c, knocked, null, t.Get(obj.Kind == ObjectKind.Table ? "object.tableDamage" : "object.chairDamage"), HitKind.Object, obj.Name, e.Time, obj.Position);
                        }
                        break;
                    }
                    if (!(a is Fighter fa) || !(b is Fighter fb)) break;
                    Fighter shooter = c.Shooter;
                    if (fa == shooter || fb == shooter)
                    {
                        Fighter other = fa == shooter ? fb : fa;
                        Vec2 at = PosAt(c, other, frame);
                        if (other.Team != shooter.Team)
                        {
                            if (c.StartDown.Contains(other)) ContactDown(c, other, e.Time, at);
                            else
                            {
                                if (shooter.Data.Type == FighterType.Grappler && c.GrabbedTarget == null)
                                {
                                    // the damage lands with the slam (SimEventType.Slam)
                                    c.GrabbedTarget = other;
                                    o.Events.Add(new FieldEvent { Time = e.Time, Kind = FieldEventKind.Grab, Fighter = other, Position = at, Text = "잡았다!" });
                                }
                                else
                                {
                                    if (shooter.Data.Type == FighterType.Stampeder && !o.Events.Exists(x => x.Kind == FieldEventKind.PassThrough && x.Fighter == other))
                                        o.Events.Add(new FieldEvent { Time = e.Time, Kind = FieldEventKind.PassThrough, Fighter = other, Position = at, Text = "관통!" });
                                    Direct(c, other, e.Time, at, 1f, "", HitKind.Direct);
                                }
                            }
                        }
                        else if (c.M.Rules.AssistsAllowed && !o.AssistsUsed.Contains(other))
                        {
                            Assist(c, other, e.Time, frame); // tornado: bumping your in-ring partner
                        }
                    }
                    else if (fa.Team != shooter.Team && fb.Team != shooter.Team)
                    {
                        // a knocked opponent bowling into another opponent
                        Fighter knocked = c.HitBefore.Contains(fa) ? fa : c.HitBefore.Contains(fb) ? fb : null;
                        if (knocked == null) break;
                        Fighter victim = knocked == fa ? fb : fa;
                        if (c.StartDown.Contains(victim) || o.Downed.Contains(victim)) break;
                        float dmg = BaseDamage(c, shooter, victim) * t.Get("hit.bowling");
                        Damage(c, victim, shooter, dmg, HitKind.Bowling, "볼링", e.Time, PosAt(c, victim, frame));
                    }
                    break;
            }
        }

        static IEnumerable<Fighter> Opponents(Ctx c)
        {
            foreach (Fighter f in c.M.Teams[1 - c.Shooter.Team].InRing) yield return f;
        }

        /// <summary>The shooter (or its landing) touches an opponent who was down when the shot began.</summary>
        static void ContactDown(Ctx c, Fighter target, float time, Vec2 at)
        {
            MatchRules rules = c.M.Rules;
            FinalePlan plan = c.Team.Plan;
            if (!c.M.PlanAllowed(plan)) return;

            if (plan == FinalePlan.Pinfall && (c.M.Field.RefereeDown > 0 || c.O.RefDown))
            {
                c.O.Events.Add(new FieldEvent { Time = time, Kind = FieldEventKind.NoCount, Fighter = target, Position = at, Text = "심판이 없다!" });
                return;
            }
            Finale(c, target, plan, time, at);
        }

        static void Finale(Ctx c, Fighter target, FinalePlan plan, float time, Vec2 at)
        {
            if (c.O.Downed.Contains(target)) return; // downed during this shot: plan choice comes first (P9)
            int frame = FrameAt(c.O.Shot, time);
            Vec2 attackerAt = PosAt(c, c.Shooter, frame);
            var f = new FinaleTrigger
            {
                AttackerTeam = c.Shooter.Team,
                Attacker = c.Shooter,
                Defender = target,
                Plan = plan,
                NearRope = IsNearRope(at, c.T),
                Time = time,
                Position = at,
                AttackerAt = attackerAt,
            };
            if (plan == FinalePlan.Ringout)
            {
                // grab and drive the defender straight away from the attacker into the ropes
                Vec2 dir = (at - attackerAt).Normalized;
                if (dir.LengthSquared < 0.5f) dir = new Vec2(0f, 1f);
                float r = c.T.Get("unit.radius");
                float size = c.T.Get("ring.size");
                float tx = dir.X > 1e-4f ? (size - r - at.X) / dir.X : dir.X < -1e-4f ? (r - at.X) / dir.X : float.MaxValue;
                float ty = dir.Y > 1e-4f ? (size - r - at.Y) / dir.Y : dir.Y < -1e-4f ? (r - at.Y) / dir.Y : float.MaxValue;
                float d = Math.Max(0f, Math.Min(tx, ty));
                f.PushDir = dir;
                f.PushDistance = d;
                f.RopePoint = at + dir * d;
            }
            c.O.Finale = f;
        }

        static float HeatMul(TeamState team, Fighter f, Tuning t)
        {
            float span = f.Data.HeatDependent ? t.Get("heat.damageSpanDependent") : t.Get("heat.damageSpan");
            return 1f + (team.Heat - 50f) / 50f * span;
        }

        public static float AttributeMul(FighterAttribute a, FighterAttribute d, Tuning t)
        {
            bool adv = (a == FighterAttribute.Hot && d == FighterAttribute.Open) ||
                       (a == FighterAttribute.Open && d == FighterAttribute.Cool) ||
                       (a == FighterAttribute.Cool && d == FighterAttribute.Hot) ||
                       (a == FighterAttribute.Steady && d == FighterAttribute.Shady) ||
                       (a == FighterAttribute.Shady && d == FighterAttribute.Steady);
            bool dis = (a == FighterAttribute.Hot && d == FighterAttribute.Cool) ||
                       (a == FighterAttribute.Cool && d == FighterAttribute.Open) ||
                       (a == FighterAttribute.Open && d == FighterAttribute.Hot);
            return adv ? t.Get("attr.advantage") : dis ? t.Get("attr.disadvantage") : 1f;
        }

        static float BaseDamage(Ctx c, Fighter source, Fighter target) =>
            source.Power * c.T.Get("damage.scale") * AttributeMul(source.Data.Attribute, target.Data.Attribute, c.T) * HeatMul(c.M.Teams[source.Team], source, c.T);

        static void Direct(Ctx c, Fighter target, float time, Vec2 at, float extraMul, string extraTag, HitKind kind)
        {
            if (c.O.Downed.Contains(target)) return;
            Tuning t = c.T;
            Fighter s = c.Shooter;
            bool showman = s.Data.Type == FighterType.Showman;
            bool special = c.Kind != ShotKind.Normal;
            float per = showman ? t.Get("type.showmanBonus") : t.Get("rope.bonusPerBounce");
            int cap = showman && special ? (int)t.Get("type.showmanMaxSpecial") : (int)t.Get("rope.bonusMax");
            int counted = Math.Min(c.Bounces, cap);
            var tags = new List<string>();
            if (extraTag.Length > 0) tags.Add(extraTag);

            float dmg = BaseDamage(c, s, target) * (1f + per * counted) * extraMul;
            if (counted > 0) tags.Add("반동×" + counted);
            float attr = AttributeMul(s.Data.Attribute, target.Data.Attribute, t);
            if (attr > 1f) tags.Add("속성▲");
            else if (attr < 1f) tags.Add("속성▼");
            if (s.Data.Type == FighterType.Striker && c.TotalBounces == 0 && kind == HitKind.Direct)
            {
                dmg *= t.Get("type.strikerDirect");
                tags.Add("직격");
            }
            if (c.LariatArmed.Contains(target))
            {
                dmg *= t.Get("hit.lariat") * s.LariatMul;
                tags.Add("라리아트");
                c.O.LariatCount++;
                c.O.Flashy = true;
                c.O.HeatGain += t.Get("heat.lariat");
                c.LariatArmed.Remove(target);
            }
            if (c.Kind == ShotKind.Special) { dmg *= s.Data.SpecialMul; tags.Add("필살기"); }
            else if (c.Kind == ShotKind.Joint) { dmg *= t.Get("joint.mul"); tags.Add("합동 필살기"); }
            if (c.DiveReady)
            {
                dmg *= t.Get("dive.mul");
                tags.Add("다이브");
                c.DiveReady = false;
                c.O.Flashy = true;
                c.O.Events.Add(new FieldEvent { Time = time, Kind = FieldEventKind.Dive, Fighter = s, Position = at });
            }
            if (c.WeaponReady)
            {
                dmg *= t.Get("object.weaponMul");
                tags.Add("흉기");
                c.WeaponReady = false;
            }
            if (c.FirstShotBonus)
            {
                dmg *= s.FirstShotPowerMul;
                c.FirstShotBonus = false;
            }
            if (counted > 0) c.O.HeatGain += t.Get("heat.perBounceHit") * counted;
            if (counted >= 2) c.O.Flashy = true;

            c.Bounces = 0;
            c.O.ShooterHit = true;
            Damage(c, target, s, dmg, kind, string.Join(" · ", tags), time, at);
        }

        static void Damage(Ctx c, Fighter target, Fighter source, float dmg, HitKind kind, string tags, float time, Vec2 at)
        {
            if (c.O.Downed.Contains(target) || c.StartDown.Contains(target)) return;
            float st = c.Stamina[target];
            var hit = new HitInfo { Time = time, Target = target, Source = source, Kind = kind, Tags = tags, Position = at };
            if (dmg >= st)
            {
                dmg = st;
                hit.CausedDown = true;
                c.O.Downed.Add(target);
                c.O.HeatGain += c.T.Get("heat.down");
            }
            hit.Damage = dmg;
            hit.BodyDamage = dmg * c.T.Get("body.ratio");
            c.Stamina[target] = st - dmg;
            c.Body[target] = Math.Max(0f, c.Body[target] - hit.BodyDamage);
            c.HitBefore.Add(target);
            c.O.TotalDamage += dmg;
            if (kind == HitKind.Assist) c.O.AssistDamage += dmg;
            c.O.Hits.Add(hit);
        }

        /// <summary>The shooter touches a teammate (on the apron, or in the ring in a tornado match).</summary>
        static void Touch(Ctx c, Fighter apron, float time, int frame)
        {
            if (apron.Team == c.Shooter.Team)
            {
                if (c.M.Rules.AssistsAllowed && !apron.IsDown && !c.O.AssistsUsed.Contains(apron)) Assist(c, apron, time, frame);
                return;
            }
            if (c.O.KnockedOff.Contains(apron)) return;
            c.O.KnockedOff.Add(apron);
            c.O.Events.Add(new FieldEvent { Time = time, Kind = FieldEventKind.Knockoff, Fighter = apron, Position = apron.Position, Text = apron.Short + " 낙하!" });
        }

        static void Assist(Ctx c, Fighter partner, float time, int frame)
        {
            Tuning t = c.T;
            ShotOutcome o = c.O;
            if (partner.Data.Assist == AssistKind.None) return;
            o.AssistsUsed.Add(partner);
            Vec2 from = partner.InRing ? PosAt(c, partner, frame) : partner.Position;
            o.Events.Add(new FieldEvent { Time = time, Kind = FieldEventKind.Assist, Fighter = partner, Position = from, Text = partner.Short + " " + CharacterData.AssistNames[(int)partner.Data.Assist] + "!" });
            float size = t.Get("ring.size");
            switch (partner.Data.Assist)
            {
                case AssistKind.Lariat:
                {
                    var center = new Vec2(size * 0.5f, size * 0.5f);
                    Vec2 dir = (center - from).Normalized;
                    Vec2 end = from + dir * (size + 2f);
                    float reach = t.Get("assist.lariatWidth") + t.Get("unit.radius");
                    foreach (Fighter target in Opponents(c))
                    {
                        Vec2 p = PosAt(c, target, frame);
                        if (DistanceToSegment(p, from, end) > reach || c.StartDown.Contains(target)) continue;
                        Damage(c, target, partner, BaseDamage(c, partner, target) * t.Get("assist.lariatMul"), HitKind.Assist, "어시스트 라리아트", time, p);
                    }
                    break;
                }
                case AssistKind.Stomp:
                {
                    Vec2 at = PosAt(c, c.Shooter, frame);
                    foreach (Fighter target in Opponents(c))
                    {
                        Vec2 p = PosAt(c, target, frame);
                        if ((p - at).Length > t.Get("assist.stompRadius") || c.StartDown.Contains(target)) continue;
                        Damage(c, target, partner, BaseDamage(c, partner, target) * t.Get("assist.stompMul"), HitKind.Assist, "어시스트 스톰프", time, p);
                    }
                    break;
                }
                case AssistKind.Cheer:
                    o.HeatGain += t.Get("assist.cheerHeat");
                    break;
                case AssistKind.Icing:
                    o.ShooterHeal += c.Shooter.MaxStamina * t.Get("assist.icing");
                    break;
            }
        }

        static float DistanceToSegment(Vec2 p, Vec2 a, Vec2 b)
        {
            Vec2 ab = b - a;
            float len2 = Vec2.Dot(ab, ab);
            float k = len2 > 1e-6f ? Math.Max(0f, Math.Min(1f, Vec2.Dot(p - a, ab) / len2)) : 0f;
            return (p - (a + ab * k)).Length;
        }

        /// <summary>Center within ring.nearRopeDiameters unit diameters of a rope line (P31).</summary>
        public static bool IsNearRope(Vec2 p, Tuning t)
        {
            float size = t.Get("ring.size");
            float band = t.Get("ring.nearRopeDiameters") * 2f * t.Get("unit.radius");
            float edge = Math.Min(Math.Min(p.X, p.Y), Math.Min(size - p.X, size - p.Y));
            return edge <= band;
        }

        public static bool NearCorner(Vec2 p, Tuning t)
        {
            float size = t.Get("ring.size");
            float d = t.Get("corner.radius");
            return (p - new Vec2(0, 0)).Length <= d || (p - new Vec2(size, 0)).Length <= d ||
                   (p - new Vec2(0, size)).Length <= d || (p - new Vec2(size, size)).Length <= d;
        }

        static ShotResult Truncate(ShotResult shot, float time)
        {
            var cut = new ShotResult(shot.Dt);
            int last = FrameAt(shot, time);
            for (int i = 0; i <= last; i++) cut.Frames.Add(shot.Frames[i]);
            foreach (SimEvent e in shot.Events)
                if (e.Time <= time + 1e-5f && e.Type != SimEventType.Stopped) cut.Events.Add(e);
            cut.Events.Add(new SimEvent { Type = SimEventType.Stopped, Time = time, Body = 0, Other = -1 });
            cut.Duration = time;
            return cut;
        }
    }
}
