using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using RingsideHearts.Battle.Core;

namespace RingsideHearts.Battle.Tests
{
    /// <summary>A simulated human player: shot choice, aim, and two-handed bout skill.</summary>
    public sealed class BotProfile
    {
        public string Name;
        public bool Idle;
        public AiStyle Style = new AiStyle();
        /// <summary>Alternating taps per second (both hands together).</summary>
        public float TapsPerSecond;
        /// <summary>Timing error standard deviation, seconds (kickout, ringout).</summary>
        public float TimingSd;
        public bool UsesIntervention = true;

        static BotProfile Make(string name, int cand, float err, float taps, float sd, bool tag = true, bool appeal = true, bool assists = true) =>
            new BotProfile
            {
                Name = name, TapsPerSecond = taps, TimingSd = sd,
                Style = new AiStyle { Candidates = cand, AimError = err, UseTag = tag, UseAppeal = appeal, UseAssists = assists },
            };

        public static readonly BotProfile IdlePlayer = new BotProfile { Name = "아무것도 안 함", Idle = true, UsesIntervention = false };
        public static readonly BotProfile Novice = Make("초보", 20, 12f, 6f, 0.1f);
        public static readonly BotProfile Average = Make("보통", 12, 8f, 7f, 0.07f);
        public static readonly BotProfile Expert = Make("고수", 3, 4f, 9f, 0.04f);
        public static readonly BotProfile AverageNoTag = Make("보통−태그·어시스트 안 씀", 12, 8f, 7f, 0.07f, tag: false, assists: false);
        public static readonly BotProfile AverageNoAppeal = Make("보통−어필 안 씀", 12, 8f, 7f, 0.07f, appeal: false);
        public static readonly BotProfile[] Main = { IdlePlayer, Novice, Average, Expert, AverageNoTag, AverageNoAppeal };
    }

    public sealed class MatchStats
    {
        public bool Won;
        public bool TurnLimit;
        public int Turns;
        public float Minutes;
        public int MyDowns, EnemyDowns;
        public int MyFinales, MyFinaleWins, EnemyFinales, EnemyFinaleWins;
        public int Tags, Assists, Appeals, Specials, Joints;
    }

    /// <summary>
    /// Plays whole matches headless with the real GameVariables values and data files. Team 0 is a
    /// <see cref="BotProfile"/> standing in for the human (a human-controlled team, so bouts use
    /// the human mechanics), team 1 the enemy AI.
    /// </summary>
    public static class BalanceHarness
    {
        public static Tuning LoadAssetTuning(bool allowMissing = false)
        {
            var fields = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string raw in File.ReadAllLines(TestPaths.DataFile("GameVariables.asset")))
            {
                string line = raw.Trim();
                int colon = line.IndexOf(':');
                if (colon <= 0) continue;
                fields[line.Substring(0, colon)] = line.Substring(colon + 1).Trim();
            }
            Tuning t = GameVariableKeys.Defaults();
            string[,] map = GameVariableKeys.Map;
            var missing = new List<string>();
            for (int i = 0; i < map.GetLength(0); i++)
            {
                if (fields.TryGetValue(map[i, 0], out string value))
                    t.Set(map[i, 1], float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture));
                else missing.Add(map[i, 0]);
            }
            if (missing.Count > 0 && !allowMissing)
                throw new InvalidDataException("GameVariables.asset has no " + string.Join(", ", missing) + " — save the asset in Unity once");
            return t;
        }

        const float SecondsPerMyAction = 11f;
        const float SecondsPerEnemyAction = 6f;
        const float SecondsPerBout = 7f;

        /// <summary>Optional per-bout log line (plan, strengths, result) for diagnosis.</summary>
        public static System.Action<string> BoutLog;

        public static MatchStats Play(Tuning t, BotProfile bot, int seed, string matchId = "tag", Dictionary<string, TeamSetup> teams = null)
        {
            teams = teams ?? TestData.Teams();
            MatchRules rules = TestData.Matches()[matchId];
            TeamSetup me = teams["player_default"].Clone();
            me.Control = Control.Human;
            var m = new Match(rules, me, teams[rules.EnemyTeam], TestData.Chars(), t, seed);
            var s = new MatchStats();
            var rng = new Rng(seed * 7919 + 17);
            int myActions = 0, enemyActions = 0, bouts = 0;

            for (int step = 0; step < 800 && m.Phase != MatchPhase.Ended; step++)
            {
                if (m.Phase == MatchPhase.AwaitFinale)
                {
                    bool mine = m.PendingFinale.AttackerTeam == 0;
                    if (mine) s.MyFinales++;
                    else s.EnemyFinales++;
                    FinaleBout bout = m.CreateBout();
                    bool won = RunBout(bout, bot, rng);
                    BoutLog?.Invoke($"BOUT {bout.Kind} atkHuman={bout.AttackerHuman} A={bout.A:0.00} D={bout.D:0.00} body={bout.Defender.BodyRatio:0.00} t={bout.Elapsed:0.0} won={won} near={m.PendingFinale.NearRope} heatA={m.Teams[m.PendingFinale.AttackerTeam].Heat:0} interv={bout.LastIntervention} " + BoutState(bout));
                    if (mine && won) s.MyFinaleWins++;
                    if (!mine && won) s.EnemyFinaleWins++;
                    bouts++;
                    m.ResolveFinale(won);
                    continue;
                }

                var downBefore = new HashSet<Fighter>();
                foreach (Fighter f in m.AllFighters()) if (f.IsDown) downBefore.Add(f);
                int acting = m.ActingTeam;
                if (acting == 0) myActions++;
                else enemyActions++;

                if (acting == 0 && bot.Idle)
                {
                    if (m.CanHold) m.Hold();
                    else m.Shoot(new Vec2(0.3f, 0f));
                }
                else
                {
                    AiDecision d = acting == 0 ? EnemyAi.Decide(m, bot.Style) : EnemyAi.Decide(m);
                    if (d.Action == AiAction.Hold) m.Hold();
                    else if (d.Action == AiAction.Appeal) m.Appeal();
                    else m.Shoot(d.Velocity, d.Special);
                }

                foreach (Fighter f in m.AllFighters())
                    if (f.IsDown && !downBefore.Contains(f))
                    {
                        if (f.Team == 0) s.MyDowns++;
                        else s.EnemyDowns++;
                    }
            }

            TeamState mine0 = m.Teams[0];
            s.Won = m.Winner == 0;
            s.TurnLimit = m.EndReason == EndReason.TurnLimit;
            s.Turns = Math.Min(m.Turn, m.TurnLimit);
            s.Tags = mine0.Tags;
            s.Assists = mine0.Assists;
            s.Appeals = mine0.Appeals;
            s.Specials = mine0.Specials;
            s.Joints = mine0.Joints;
            s.Minutes = (myActions * SecondsPerMyAction + enemyActions * SecondsPerEnemyAction + bouts * SecondsPerBout) / 60f;
            return s;
        }

        static string BoutState(FinaleBout b) =>
            b is PinfallBout p ? $"gauge={p.Gauge:0} count={p.Count}" : b is SubmissionBout s ? $"sq={s.Squeeze:0} crawl={s.Crawl:0}" : b is RingoutBout r ? $"over={r.Over:0}" : "";

        static float Gauss(Rng r, float sd)
        {
            double u1 = Math.Max(1e-6, r.NextFloat());
            double u2 = r.NextFloat();
            return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2)) * sd;
        }

        /// <summary>Plays a bout; the human side is the bot. True if the attacker won.</summary>
        static bool RunBout(FinaleBout b, BotProfile bot, Rng rng)
        {
            const float dt = 1f / 120f;
            bool humanPlays = b.HasHuman && !bot.Idle;
            bool attacker = b.AttackerHuman;
            float nextTap = 0.2f;
            Hand hand = Hand.Left;
            float plannedBoth = -1f;
            int plannedFor = -1;

            for (int i = 0; i < 120 * 40 && b.Result == MashResult.Running; i++)
            {
                if (humanPlays)
                {
                    if (bot.UsesIntervention && (attacker ? b.AttackerCanIntervene && b.AttackerProgress < 0.35f && b.Elapsed > 1f
                                                          : b.DefenderCanIntervene && b.AttackerProgress > 0.6f))
                        b.Intervene(attacker);

                    bool tapGame = b is SubmissionBout || (b is PinfallBout && attacker);
                    if (tapGame)
                    {
                        if (b.Elapsed >= nextTap)
                        {
                            b.Press(hand);
                            hand = hand == Hand.Left ? Hand.Right : Hand.Left;
                            nextTap = b.Elapsed + rng.Range(0.8f, 1.2f) / bot.TapsPerSecond;
                        }
                    }
                    else if (b is PinfallBout pin)
                    {
                        // kickout: aim both hands at this count's window, with timing error
                        int count = pin.Count;
                        if (plannedFor != count && pin.RefCounting)
                        {
                            plannedFor = count;
                            plannedBoth = b.Elapsed + pin.TimeToWindow + Gauss(rng, bot.TimingSd);
                        }
                        if (plannedBoth > 0f && b.Elapsed >= plannedBoth)
                        {
                            b.Press(Hand.Left);
                            b.Press(Hand.Right);
                            plannedBoth = -1f;
                        }
                    }
                    else if (b is RingoutBout ro)
                    {
                        if (plannedBoth < 0f)
                            plannedBoth = b.Elapsed + ro.TimeToNextExtreme(attacker) + Gauss(rng, bot.TimingSd);
                        if (b.Elapsed >= plannedBoth)
                        {
                            b.Press(Hand.Left);
                            b.Press(Hand.Right);
                            plannedBoth = -1f;
                            // skip to the following extreme next time
                            nextTap = b.Elapsed + ro.Period * 0.5f;
                        }
                    }
                }
                b.Advance(dt);
            }
            return b.Result == MashResult.AttackerWins;
        }
    }
}
