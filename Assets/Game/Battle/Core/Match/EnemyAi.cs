using System;
using System.Collections.Generic;

namespace RingsideHearts.Battle.Core
{
    public enum AiAction { Shoot, Appeal, Hold }

    public struct AiDecision
    {
        /// <summary>The fighter this decision tagged in (already applied to the match), or null.</summary>
        public Fighter TagIn;
        public AiAction Action;
        public Vec2 Velocity;
        public bool Special;
        public bool IsFinale;
        public FinalePlan Plan;
    }

    /// <summary>How an AI (or a bot standing in for a human) plays.</summary>
    public sealed class AiStyle
    {
        public int Candidates = 3;
        public float AimError = 5f;
        public bool UseTag = true;
        public bool UseAppeal = true;
        public bool UseAssists = true;
        public bool UseSpecial = true;
        public bool UseCorners = true;

        public static AiStyle FromTeam(TeamSetup s) => new AiStyle { Candidates = Math.Max(1, s.AiCandidates), AimError = s.AiAimError };
    }

    /// <summary>
    /// AI for whichever team is acting (design v0.1 5.3 extended for v0.2). Applies its tag
    /// to the match itself, then returns the action. Uses only the match's seeded Rng.
    /// </summary>
    public static class EnemyAi
    {
        public static AiDecision Decide(Match m) => Decide(m, AiStyle.FromTeam(m.Acting.Setup));

        public static AiDecision Decide(Match m, AiStyle style)
        {
            Tuning t = m.Tuning;
            var d = new AiDecision();
            if (style.UseTag)
            {
                Fighter tag = TagChoice(m, t);
                if (tag != null)
                {
                    m.Tag(tag);
                    d.TagIn = tag;
                }
            }

            Fighter actor = m.Actor;
            if (actor == null || actor.IsDown)
            {
                d.Action = AiAction.Hold;
                return d;
            }

            float maxSpeed = t.Get("shot.maxSpeed");
            Fighter downed = null;
            foreach (Fighter f in m.Opponent.InRing)
                if (f.IsDown) { downed = f; break; }
            if (downed != null)
            {
                FinalePlan plan = ChoosePlan(m, downed.Position, t);
                m.SetPlan(m.ActingTeam, plan);
                Vec2 dir = (downed.Position - actor.Position).Normalized;
                d.IsFinale = true;
                d.Plan = plan;
                d.Velocity = Aim(dir * maxSpeed, style.AimError, m.Rng);
                return d;
            }

            if (style.UseAppeal && m.Acting.Heat < t.Get("ai.appealBelow") && m.Acting.LastAppealer != actor && m.Rng.NextFloat() < t.Get("ai.appealChance"))
            {
                d.Action = AiAction.Appeal;
                return d;
            }

            bool special = style.UseSpecial && actor.SpecialReady;
            int dirs = Math.Max(1, (int)t.Get("ai.rushDirections"));
            float[] powers = { t.Get("ai.rushPower1"), t.Get("ai.rushPower2"), t.Get("ai.rushPower3") };
            var candidates = new List<(float score, int order, Vec2 v)>();
            foreach (float p in powers)
                for (int i = 0; i < dirs; i++)
                {
                    double a = 2 * Math.PI * i / dirs;
                    var v = new Vec2((float)Math.Cos(a), (float)Math.Sin(a)) * (p * maxSpeed);
                    candidates.Add((Score(m.Preview(v, special), m, style), candidates.Count, v));
                }
            candidates.Sort((x, y) => x.score != y.score ? y.score.CompareTo(x.score) : x.order.CompareTo(y.order));
            int top = Math.Max(1, Math.Min(style.Candidates, candidates.Count));
            d.Velocity = Aim(candidates[m.Rng.Next(top)].v, style.AimError, m.Rng);
            d.Special = special;
            return d;
        }

        /// <summary>Candidate score: damage, downs, lariats, knock-offs, assists, corners, chain.</summary>
        public static float Score(ShotOutcome o, Match m, AiStyle style)
        {
            Tuning t = m.Tuning;
            float s = style.UseAssists ? o.TotalDamage : o.TotalDamage - o.AssistDamage;
            s += o.Downed.Count * t.Get("ai.downBonus");
            s += o.LariatCount * t.Get("ai.lariatBonus");
            s += o.KnockedOff.Count * t.Get("ai.knockoffBonus");
            if (style.UseAssists) s += o.AssistsUsed.Count * 40f;
            if (style.UseCorners && o.EndsOnTurnbuckle) s += t.Get("ai.cornerBonus");
            if (o.RefDown) s -= 60f; // a downed referee can't count a pinfall
            TeamState team = m.Teams[o.Shooter.Team];
            if (o.ShooterHit && team.Chain < 3 && (int)o.Shooter.Data.Role == team.Chain) s += t.Get("ai.chainBonus");
            // never hand the opponent a free finale: hitting nobody next to the downed... is fine; but avoid ending on the rope's edge? keep simple
            return s;
        }

        static Fighter TagChoice(Match m, Tuning t)
        {
            if (!m.Rules.TagAllowed || m.Phase != MatchPhase.AwaitAction) return null;
            Fighter actor = m.Actor;
            if (actor == null) return null;
            Fighter best = null;
            Fighter charged = null;
            Fighter finisher = null;
            foreach (Fighter f in m.Acting.Members)
            {
                if (!m.CanTag(f)) continue;
                if (best == null || f.StaminaRatio > best.StaminaRatio) best = f;
                if (f.SpecialReady && f.StaminaRatio >= 0.4f) charged = f;
                if (f.Data.Role == ChainRole.Finisher && f.StaminaRatio >= 0.4f) finisher = f;
            }
            if (best == null) return null;
            if (actor.IsDown) return best.StaminaRatio > 0.3f ? best : null;
            if (actor.StaminaRatio < t.Get("ai.tagLow") && best.StaminaRatio >= t.Get("ai.tagHealthy")) return best;
            if (m.Acting.Chain >= 3 && actor.Data.Role != ChainRole.Finisher && finisher != null) return finisher;
            if (!actor.SpecialReady && charged != null) return charged;
            return null;
        }

        static FinalePlan ChoosePlan(Match m, Vec2 p, Tuning t)
        {
            if (m.Rules.SubmissionOnly) return FinalePlan.Submission;
            if (m.PlanAllowed(FinalePlan.Ringout) && ShotResolver.IsNearRope(p, t)) return FinalePlan.Ringout;
            bool refUp = m.Field.RefereeDown == 0;
            return refUp && m.PlanAllowed(FinalePlan.Pinfall) ? FinalePlan.Pinfall : FinalePlan.Submission;
        }

        public static Vec2 Aim(Vec2 v, float errorDegrees, Rng rng)
        {
            float off = rng.Range(-errorDegrees, errorDegrees) * (float)(Math.PI / 180.0);
            float c = (float)Math.Cos(off);
            float s = (float)Math.Sin(off);
            return new Vec2(v.X * c - v.Y * s, v.X * s + v.Y * c);
        }
    }
}
