using System.Linq;
using NUnit.Framework;
using RingsideHearts.Battle.Core;

namespace RingsideHearts.Battle.Tests
{
    public class FinaleTests
    {
        /// <summary>Leo (human) has just hit downed Bolt with <paramref name="plan"/>.</summary>
        static Match AwaitFinale(FinalePlan plan, Vec2 boltAt, Vec2 velocity, string matchId = "tag")
        {
            Match m = TestData.Start(matchId);
            Fighter bolt = m.Teams[1].InRing[0];
            bolt.IsDown = true;
            bolt.Stamina = 100;
            bolt.Position = boltAt;
            m.SetPlan(0, plan);
            m.Shoot(velocity);
            Assert.AreEqual(MatchPhase.AwaitFinale, m.Phase, "setup: the shot should start a finale");
            return m;
        }

        static T Ready<T>(T b) where T : FinaleBout
        {
            while (b.InIntro) b.Advance(1f / 60f);
            return b;
        }

        static void Run(FinaleBout b, float seconds)
        {
            for (float t = 0f; t < seconds && b.Result == MashResult.Running; t += 1f / 60f) b.Advance(1f / 60f);
        }

        [Test]
        public void Pinfall_HumanAttacker_AlternateHandsOnly()
        {
            Match m = AwaitFinale(FinalePlan.Pinfall, new Vec2(5, 7.5f), new Vec2(0, 20));
            var b = (PinfallBout)Ready(m.CreateBout());
            Assert.True(b.AttackerHuman);
            float g = b.Gauge;
            b.Press(Hand.Left);
            float afterOne = b.Gauge;
            Assert.Greater(afterOne, g);
            b.Press(Hand.Left);
            Assert.AreEqual(afterOne, b.Gauge, 1e-4f, "same hand twice does nothing");
            b.Press(Hand.Right);
            Assert.Greater(b.Gauge, afterOne);
        }

        [Test]
        public void Pinfall_FastAlternating_Wins_NoInput_Kickout()
        {
            Match m = AwaitFinale(FinalePlan.Pinfall, new Vec2(5, 7.5f), new Vec2(0, 20));
            m.PendingFinale.Defender.Body = m.PendingFinale.Defender.MaxBody * 0.3f; // worn down
            var b = (PinfallBout)Ready(m.CreateBout());
            Hand h = Hand.Left;
            for (int f = 0; f < 60 * 8 && b.Result == MashResult.Running; f++)
            {
                if (f % 6 == 0) { b.Press(h); h = h == Hand.Left ? Hand.Right : Hand.Left; }
                b.Advance(1f / 60f);
            }
            Assert.AreEqual(MashResult.AttackerWins, b.Result);

            Match n = AwaitFinale(FinalePlan.Pinfall, new Vec2(5, 7.5f), new Vec2(0, 20));
            var idle = (PinfallBout)Ready(n.CreateBout());
            Run(idle, 20f);
            Assert.AreEqual(MashResult.DefenderWins, idle.Result);
        }

        [Test]
        public void Pinfall_HumanDefender_KickoutTimingWindow()
        {
            Match m = TestData.Start(meAi: false);
            // Bolt (AI) pins Leo (human): swap roles by letting team 1 shoot downed Leo
            TestData.Pass(m);
            Fighter leo = m.Teams[0].InRing[0];
            leo.IsDown = true;
            m.SetPlan(1, FinalePlan.Pinfall);
            m.Shoot(new Vec2(0, -20));
            Assert.AreEqual(MatchPhase.AwaitFinale, m.Phase);
            var b = (PinfallBout)Ready(m.CreateBout());
            Assert.False(b.AttackerHuman);
            Assert.True(b.DefenderHuman);
            // press both hands right in the window of the first count
            while (b.TimeToWindow > 0f) b.Advance(1f / 120f);
            b.Press(Hand.Left);
            b.Press(Hand.Right);
            Assert.AreEqual(MashResult.DefenderWins, b.Result);
        }

        [Test]
        public void Pinfall_HumanDefender_MissedTimingLoses()
        {
            Match m = TestData.Start();
            TestData.Pass(m);
            m.Teams[0].InRing[0].IsDown = true;
            m.SetPlan(1, FinalePlan.Pinfall);
            m.Shoot(new Vec2(0, -20));
            var b = (PinfallBout)Ready(m.CreateBout());
            b.Press(Hand.Left);
            b.Press(Hand.Right); // way too early, then nothing
            Run(b, 10f);
            Assert.AreEqual(MashResult.AttackerWins, b.Result);
            Assert.AreEqual(3, b.Count);
        }

        [Test]
        public void Submission_CrawlToRope_Breaks_ButNotInNoRopeBreak()
        {
            Match m = AwaitFinale(FinalePlan.Submission, new Vec2(5, 8.6f), new Vec2(0, 20));
            var b = (SubmissionBout)m.CreateBout();
            Assert.AreEqual(60f, b.Crawl, "caught near the rope: crawl starts at 60");
            Run(b, 10f);
            Assert.AreEqual(MashResult.DefenderWins, b.Result);

            Match n = AwaitFinale(FinalePlan.Submission, new Vec2(5, 8.6f), new Vec2(0, 20), "norope");
            var nb = (SubmissionBout)n.CreateBout();
            Assert.False(nb.RopeBreak);
            Assert.AreEqual(0f, nb.Crawl);
        }

        [Test]
        public void Submission_TimeLimit_Escape()
        {
            Match m = AwaitFinale(FinalePlan.Submission, new Vec2(5, 5.5f), new Vec2(0, 20), "norope");
            var b = (SubmissionBout)Ready(m.CreateBout());
            Run(b, 6.5f);
            Assert.AreEqual(MashResult.DefenderWins, b.Result);
            Assert.GreaterOrEqual(b.Elapsed, 6f - 1e-3f);
        }

        [Test]
        public void Ringout_BothHandsAtOuterEnd_Pushes_WrongTimeDoesNot()
        {
            Match m = AwaitFinale(FinalePlan.Ringout, new Vec2(5, 8.6f), new Vec2(0, 25));
            var b = (RingoutBout)Ready(m.CreateBout());
            float start = b.Over;
            // wrong time: at the inner end
            while (b.TimeToNextExtreme(false) > 0.02f) b.Advance(1f / 240f);
            float beforeWrong = b.Over;
            b.Press(Hand.Left); b.Press(Hand.Right);
            Assert.AreEqual(beforeWrong, b.Over, 1e-4f);
            // right time: at the outer end
            while (b.TimeToNextExtreme(true) > 0.01f) b.Advance(1f / 240f);
            float before = b.Over;
            b.Press(Hand.Left); b.Press(Hand.Right);
            Assert.Greater(b.Over, before);
            Assert.Greater(start, 0f);
        }

        [Test]
        public void Intervention_FaceBoosts_HeelFreezesAndWarns()
        {
            Match m = AwaitFinale(FinalePlan.Pinfall, new Vec2(5, 7.5f), new Vec2(0, 20));
            var b = (PinfallBout)Ready(m.CreateBout());
            Assert.True(b.AttackerCanIntervene, "leo has a second (fang, face)");
            float g0 = b.Gauge;
            b.Press(Hand.Left);
            float plain = b.Gauge - g0;
            b.Intervene(true);
            Assert.False(b.AttackerCanIntervene);
            float g1 = b.Gauge;
            b.Press(Hand.Right);
            Assert.AreEqual(plain * 1.5f, b.Gauge - g1, 1e-3f);
            Assert.True(m.Teams[0].InterventionUsed);
        }

        [Test]
        public void FinaleFail_DefenderWakesAt30_AttackerHeatUp()
        {
            Match m = AwaitFinale(FinalePlan.Pinfall, new Vec2(5, 7.5f), new Vec2(0, 20));
            Fighter bolt = m.PendingFinale.Defender;
            float heat = m.Teams[0].Heat;
            m.ResolveFinale(false);
            Assert.False(bolt.IsDown);
            Assert.GreaterOrEqual(bolt.Stamina, bolt.MaxStamina * 0.3f - 0.01f);
            Assert.AreEqual(1, m.ActingTeam);
            Assert.AreEqual(heat + 10f, m.Teams[0].Heat, 1e-3f);
        }

        [Test]
        public void MoreBodyDamage_WeakerDefence()
        {
            Match m = AwaitFinale(FinalePlan.Pinfall, new Vec2(5, 7.5f), new Vec2(0, 20));
            Fighter bolt = m.PendingFinale.Defender;
            float fresh = m.CreateBout().D;
            bolt.Body = bolt.MaxBody * 0.2f;
            float beaten = m.CreateBout().D;
            Assert.Less(beaten, fresh * 0.5f);
        }

        [Test]
        public void AiVsAi_EveryMatchTypeEnds([Values("tag", "cage", "hardcore", "norope", "submission", "boss", "tornado")] string id)
        {
            for (int seed = 1; seed <= 3; seed++)
            {
                Match m = TestData.Start(id, seed, meAi: true);
                for (int step = 0; step < 600 && m.Phase != MatchPhase.Ended; step++)
                {
                    if (m.Phase == MatchPhase.AwaitFinale)
                    {
                        FinaleBout b = m.CreateBout();
                        Run(b, 30f);
                        Assert.AreNotEqual(MashResult.Running, b.Result);
                        m.ResolveFinale(b.Result == MashResult.AttackerWins);
                        continue;
                    }
                    AiDecision d = EnemyAi.Decide(m);
                    if (d.Action == AiAction.Hold) m.Hold();
                    else if (d.Action == AiAction.Appeal) m.Appeal();
                    else m.Shoot(d.Velocity, d.Special);
                }
                Assert.AreEqual(MatchPhase.Ended, m.Phase, id + " seed " + seed);
                if (id == "submission" && m.EndReason == EndReason.Finale) Assert.AreEqual(FinalePlan.Submission, m.WinningPlan);
            }
        }
        [Test]
        public void Intro_FreezesAndIgnoresInput()
        {
            Match m = AwaitFinale(FinalePlan.Pinfall, new Vec2(5, 7.5f), new Vec2(0, 20));
            var b = (PinfallBout)m.CreateBout();
            Assert.True(b.InIntro);
            float g = b.Gauge;
            b.Press(Hand.Left);
            b.Press(Hand.Right);
            b.Advance(0.5f);
            Assert.AreEqual(g, b.Gauge, "nothing moves and nothing counts during the intro");
            Assert.AreEqual(0f, b.Elapsed);
            Ready(b);
            b.Press(Hand.Left);
            Assert.Greater(b.Gauge, g);
        }

        [Test]
        public void Ringout_FromCenter_StartsLowerThanAtRope()
        {
            Match near = AwaitFinale(FinalePlan.Ringout, new Vec2(5, 8.5f), new Vec2(0, 20));
            Match far = AwaitFinale(FinalePlan.Ringout, new Vec2(5, 5.0f), new Vec2(0, 20));
            float nearStart = ((RingoutBout)near.CreateBout()).Over;
            float farStart = ((RingoutBout)far.CreateBout()).Over;
            Assert.Greater(nearStart, farStart + 10f);
        }
    }
}
