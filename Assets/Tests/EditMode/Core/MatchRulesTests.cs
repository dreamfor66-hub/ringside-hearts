using System;
using System.Linq;
using NUnit.Framework;
using RingsideHearts.Battle.Core;

namespace RingsideHearts.Battle.Tests
{
    public class MatchRulesTests
    {
        [Test]
        public void ProjectData_AllConsistent()
        {
            var chars = TestData.Chars();
            var teams = TestData.Teams();
            var matches = TestData.Matches();
            Assert.AreEqual(18, chars.Count);
            Assert.AreEqual(7, matches.Count);
            foreach (var team in teams.Values)
            {
                foreach (string s in team.Slots) Assert.True(chars.ContainsKey(s), team.Id + " slot " + s);
                foreach (string s in team.Seconds) Assert.True(s.Length == 0 || chars.ContainsKey(s), team.Id + " second " + s);
            }
            foreach (var m in matches.Values) Assert.True(teams.ContainsKey(m.EnemyTeam), m.Id);
            Assert.True(chars.Values.All(c => c.SpecialTurns >= 1));
        }

        [Test]
        public void EffectBlock_RejectsUnknownParts()
        {
            Assert.Throws<FormatException>(() => EffectBlock.Parse("sometimes | to:team | heat+1", "x"));
            Assert.Throws<FormatException>(() => EffectBlock.Parse("always | to:everyone | heat+1", "x"));
            Assert.Throws<FormatException>(() => EffectBlock.Parse("always | to:team | luck+1", "x"));
            Assert.IsNull(EffectBlock.Parse("none", "x"));
        }

        [Test]
        public void Start_TagMatch_Layout()
        {
            Match m = TestData.Start();
            Assert.AreEqual(1, m.Teams[0].InRing.Count);
            Assert.AreEqual(3, m.Teams[0].Members.Count);
            Assert.AreEqual("leo", m.Actor.Data.Id);
            Assert.AreEqual(new Vec2(5, 2.5f), m.Actor.Position);
            foreach (Fighter f in m.Teams[0].Members.Where(f => !f.InRing)) Assert.Less(f.Position.Y, 0f, "my partners stand on the bottom apron");
            foreach (Fighter f in m.Teams[1].Members.Where(f => !f.InRing)) Assert.Greater(f.Position.Y, 10f);
            Assert.AreEqual(0, m.ActingTeam);
            Assert.AreEqual(40f, m.Teams[0].Heat);
        }

        [Test]
        public void Turns_Alternate()
        {
            Match m = TestData.Start();
            TestData.Pass(m);
            Assert.AreEqual(1, m.ActingTeam);
            Assert.AreEqual(1, m.Turn);
            TestData.Pass(m);
            Assert.AreEqual(0, m.ActingTeam);
            Assert.AreEqual(2, m.Turn);
        }

        [Test]
        public void SecondAddsStats()
        {
            Match m = TestData.Start();
            Fighter leo = m.Teams[0].Members[0]; // second: fang (1300 / 130 / 115)
            Assert.AreEqual(1300f + 1300f * 0.2f, leo.MaxStamina, 1e-2f);
            Assert.AreEqual(130f + 130f * 0.2f, leo.Power, 1e-3f);
        }

        [Test]
        public void Tag_SwapsAndLocks()
        {
            Match m = TestData.Start();
            Fighter leo = m.Actor;
            Fighter tiger = m.Teams[0].Members[1];
            m.Tag(tiger);
            Assert.AreEqual(tiger, m.Actor);
            Assert.True(tiger.InRing);
            Assert.False(leo.InRing);
            Assert.Less(leo.Position.Y, 0f);
            Assert.Throws<InvalidOperationException>(() => m.Tag(m.Teams[0].Members[2]), "one tag per action");
            TestData.Pass(m);
            TestData.Pass(m);
            Assert.False(m.CanTag(leo), "just tagged out: locked through the next turn");
            TestData.Pass(m);
            TestData.Pass(m);
            Assert.True(m.CanTag(leo));
        }

        [Test]
        public void SpecialCountdown_TicksAndResets()
        {
            Match m = TestData.Start();
            Fighter leo = m.Actor;
            Assert.AreEqual(5, leo.SpecialCount);
            for (int i = 0; i < 5; i++) { TestData.Pass(m); TestData.Pass(m); }
            Assert.True(leo.SpecialReady);
            Assert.True(m.CanSpecial);
            m.Shoot(new Vec2(0.3f, 0f), special: true);
            Assert.AreEqual(5, leo.SpecialCount);
        }

        [Test]
        public void TouchingOwnApronPartner_FiresAssist()
        {
            Match m = TestData.Start();
            Fighter tiger = m.Teams[0].Members[1]; // cheer, on the bottom apron at x = 3
            m.Actor.Position = new Vec2(tiger.Position.X, 3f);
            float heat = m.Teams[0].Heat;
            ShotOutcome o = m.Shoot(new Vec2(0, -22));
            Assert.Contains(tiger, o.AssistsUsed);
            Assert.GreaterOrEqual(m.Teams[0].Heat, heat + m.Tuning.Get("assist.cheerHeat") - 0.01f);
            Assert.True(o.Events.Any(e => e.Kind == FieldEventKind.Assist && e.Fighter == tiger));
        }

        [Test]
        public void TouchingEnemyApron_KnocksThemOff()
        {
            Match m = TestData.Start();
            Fighter enemyApron = m.Teams[1].Members[1];
            m.Actor.Position = new Vec2(enemyApron.Position.X, 7f);
            m.Teams[1].InRing[0].Position = new Vec2(8.5f, 4f);
            ShotOutcome o = m.Shoot(new Vec2(0, 22));
            Assert.Contains(enemyApron, o.KnockedOff);
            Assert.AreEqual(2, enemyApron.Fallen);
            TestData.Pass(m);
            Assert.False(enemyApron.CanTagIn);
        }

        [Test]
        public void DamageAppliesToStaminaAndBody()
        {
            Match m = TestData.Start();
            Fighter bolt = m.Teams[1].InRing[0];
            ShotOutcome o = m.Shoot(new Vec2(0, 20));
            Assert.Greater(o.TotalDamage, 0f);
            HitInfo first = o.Hits[0];
            Assert.AreEqual(first.Damage * m.Tuning.Get("body.ratio"), first.BodyDamage, 1e-3f);
            Assert.AreEqual(bolt.MaxBody - o.Hits.Where(h => h.Target == bolt).Sum(h => h.BodyDamage), bolt.Body, 1e-2f);
        }

        [Test]
        public void FinaleCutsTheShotAtContact_DownTakesNoDamage()
        {
            Match m = TestData.Start();
            Fighter bolt = m.Teams[1].InRing[0];
            bolt.IsDown = true;
            bolt.Stamina = 100;
            ShotOutcome o = m.Shoot(new Vec2(0, 20));
            Assert.NotNull(o.Finale);
            Assert.AreEqual(bolt, o.Finale.Defender);
            Assert.AreEqual(MatchPhase.AwaitFinale, m.Phase);
            Assert.AreEqual(o.Finale.Time, o.Shot.Duration, 1e-4f);
            Assert.AreEqual(0f, o.TotalDamage);
            Assert.AreEqual(100f, bolt.Stamina);
        }

        [Test]
        public void Ringout_GrabsAndDrivesIntoTheRope()
        {
            Match m = TestData.Start();
            Fighter bolt = m.Teams[1].InRing[0];
            bolt.IsDown = true;
            bolt.Position = new Vec2(5, 5.5f);
            m.SetPlan(0, FinalePlan.Ringout);
            ShotOutcome o = m.Shoot(new Vec2(0, 20));
            Assert.NotNull(o.Finale, "contact alone starts a ringout");
            Assert.AreEqual(FinalePlan.Ringout, o.Finale.Plan);
            Assert.AreEqual(10f - 0.7f, o.Finale.RopePoint.Y, 0.05f);
            Assert.AreEqual(o.Finale.RopePoint, bolt.Position, "the defender is pressed against the rope");
            Assert.Less(m.Teams[0].InRing[0].Position.Y, bolt.Position.Y);
            Assert.Greater(o.Finale.PushDistance, 2.5f);
        }

        [Test]
        public void Grappler_SlamsAtCenter_DamageOnLanding()
        {
            Match m = TestData.Start();
            Fighter iceberg = m.Teams[0].Members.First(f => f.Data.Id == "luka");
            // put a grappler in: swap leo's data for iceberg's for this test
            Fighter leo = m.Actor;
            leo.Data = TestData.Chars()["iceberg"];
            Fighter bolt = m.Teams[1].InRing[0];
            bolt.Position = new Vec2(8f, 8f);
            leo.Position = new Vec2(6f, 6f);
            ShotOutcome o = m.Shoot(new Vec2(12f, 12f));
            FieldEvent grab = o.Events.First(e => e.Kind == FieldEventKind.Grab);
            FieldEvent slam = o.Events.First(e => e.Kind == FieldEventKind.Slam);
            Assert.Greater(slam.Time, grab.Time);
            HitInfo hit = o.Hits.First(h => h.Target == bolt);
            Assert.AreEqual(slam.Time, hit.Time, 1e-4f, "damage lands with the slam");
            Assert.Less((bolt.Position - new Vec2(5, 5)).Length, 0.05f);
            _ = iceberg;
        }

        [Test]
        public void RefereeDown_NoPinfallCount()
        {
            Match m = TestData.Start();
            m.Field.RefereeDown = 2;
            Fighter b = m.Teams[1].InRing[0];
            b.IsDown = true;
            ShotOutcome o = m.Shoot(new Vec2(0, 20));
            Assert.IsNull(o.Finale);
            Assert.True(o.Events.Any(e => e.Kind == FieldEventKind.NoCount));
        }

        [Test]
        public void HittingReferee_KnocksHimDown()
        {
            Match m = TestData.Start();
            m.Field.Referee = new Vec2(5, 5);
            ShotOutcome o = m.Shoot(new Vec2(0, 20));
            Assert.True(o.RefDown);
            Assert.AreEqual(2, m.Field.RefereeDown);
        }

        [Test]
        public void Hardcore_KnockedIntoTable_Hurts()
        {
            Match m = TestData.Start("hardcore");
            FieldObject table = m.Field.Objects.First(o => o.Kind == ObjectKind.Table);
            Fighter bolt = m.Teams[1].InRing[0];
            bolt.Position = new Vec2(5, 3.6f);
            m.Actor.Position = new Vec2(5, 1.5f);
            ShotOutcome o = m.Shoot(new Vec2(0, 20));
            Assert.True(o.Hits.Any(h => h.Kind == HitKind.Object && h.Target == bolt));
            Assert.True(table.Broken);
        }

        [Test]
        public void Cage_NoAssists_KnockedIntoCageHurts()
        {
            Match m = TestData.Start("cage");
            Fighter partner = m.Teams[0].Members[1];
            m.Actor.Position = new Vec2(partner.Position.X, 3f);
            ShotOutcome o = m.Shoot(new Vec2(0, -22));
            Assert.IsEmpty(o.AssistsUsed);
            TestData.Pass(m);
            Match c = TestData.Start("cage");
            c.Teams[1].InRing[0].Position = new Vec2(5, 8.5f);
            ShotOutcome hit = c.Shoot(new Vec2(0, 25));
            Assert.True(hit.Hits.Any(h => h.Kind == HitKind.Cage));
        }

        [Test]
        public void Appeal_HeatAndWarnings()
        {
            Match m = TestData.Start();
            float heat = m.Teams[0].Heat;
            m.Appeal(); // leo, face, 30
            Assert.AreEqual(heat + 30f, m.Teams[0].Heat, 1e-3f);
            Assert.AreEqual(0, m.Teams[0].Warnings);
            Assert.AreEqual(37.5f, m.AppealAmount(), 1e-3f, "bolt is a heel: 25 × 1.5");
            m.Appeal(); // ends the turn: heat decay follows
            Assert.AreEqual(1, m.Teams[1].Warnings);
            TestData.Pass(m); TestData.Pass(m);
            Assert.AreEqual(15f, m.AppealAmount(), 1e-3f, "leo again right after: half");
        }

        [Test]
        public void TurnLimit_ChallengerLoses()
        {
            Match m = TestData.Start();
            for (int i = 0; i < 200 && m.Phase != MatchPhase.Ended; i++)
            {
                if (m.CanHold) m.Hold();
                else TestData.Pass(m);
            }
            Assert.AreEqual(EndReason.TurnLimit, m.EndReason);
            Assert.AreEqual(1, m.Winner);
            Assert.AreEqual(31, m.Turn);
        }

        [Test]
        public void Chain_LightsInOrder_JointForFinisher()
        {
            Match m = TestData.Start();
            m.Teams[0].Chain = 3;
            Assert.True(m.JointReady, "leo is the finisher");
            Assert.AreEqual(ShotKind.Joint, m.KindFor(false));
            m.Shoot(new Vec2(0.3f, 0f));
            Assert.AreEqual(0, m.Teams[0].Chain);
        }

        [Test]
        public void ShowmanBouncesRaiseHeat()
        {
            Match m = TestData.Start();
            m.Tag(m.Teams[0].Members[1]); // tiger, showman
            float heat = m.Teams[0].Heat;
            ShotOutcome o = m.Shoot(new Vec2(-20, 0));
            Assert.Greater(o.HeatGain, 0f);
            Assert.Greater(m.Teams[0].Heat, heat);
        }

        [Test]
        public void DownedFighter_TagsOutOnlyNearRope()
        {
            Match m = TestData.Start();
            Fighter leo = m.Actor;
            Fighter tiger = m.Teams[0].Members[1];
            leo.IsDown = true;
            leo.Position = new Vec2(5, 5);
            Assert.False(m.CanTag(tiger), "down in the middle: cannot reach a tag");
            leo.Position = new Vec2(5, 1.2f);
            Assert.True(m.CanTag(tiger), "down by the rope: crawls and tags");
        }
    }
}
