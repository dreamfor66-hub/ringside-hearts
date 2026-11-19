using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using RingsideHearts.Battle.Core;

namespace RingsideHearts.Battle.Tests
{
    public class AimTests
    {
        [Test]
        public void Pull_DownLeft_ShootsUpRight_AtScaledSpeed()
        {
            Assert.True(ShotInput.TryFromPull(new Vec2(-190, -190), 380, 25, 0.15f, out var v));
            Assert.Greater(v.X, 0f);
            Assert.Greater(v.Y, 0f);
            Assert.AreEqual(25f * (268.70f / 380f), v.Length, 0.05f);
        }

        [Test]
        public void Pull_BeyondMax_ClampsToMaxSpeed()
        {
            Assert.True(ShotInput.TryFromPull(new Vec2(0, -1000), 380, 25, 0.15f, out var v));
            Assert.AreEqual(25f, v.Length, 1e-3f);
            Assert.AreEqual(0f, v.X, 1e-4f);
            Assert.Greater(v.Y, 0f);
        }

        [Test]
        public void Pull_Short_Cancels()
        {
            Assert.False(ShotInput.TryFromPull(new Vec2(0, -50), 380, 25, 0.15f, out var v));
            Assert.AreEqual(Vec2.Zero, v);
        }

        static RingSim Sim() => new RingSim(TestTuning.Default());

        [Test]
        public void Predict_StraightRight_BouncesStraightBack()
        {
            var g = AimPredictor.Predict(Sim(), new Vec2(5, 5), 0.7f, new Vec2(20, 0));
            Assert.True(g.ReachesRope);
            Assert.AreEqual(RopeSide.Right, g.Side);
            Assert.AreEqual(9.3f, g.Contact.X, 1e-3f);
            Assert.AreEqual(5f, g.Contact.Y, 1e-3f);
            Assert.AreEqual(9.3f, g.Exit.X, 0.25f);
            Assert.AreEqual(-1f, g.Reflected.X, 1e-3f);
            Assert.AreEqual(0f, g.Reflected.Y, 1e-3f);
        }

        [Test]
        public void Predict_Diagonal_LeavesFlatterThanMirror()
        {
            // 45 degrees into the right rope: the part into the rope keeps rope.restitution (0.85),
            // the part along the rope is kept whole, so it leaves at about 40 degrees, not 45.
            var g = AimPredictor.Predict(Sim(), new Vec2(5, 2), 0.7f, new Vec2(14, 14));
            Assert.AreEqual(RopeSide.Right, g.Side);
            Assert.Less(g.Reflected.X, 0f);
            Assert.Greater(g.Reflected.Y, 0f);
            Assert.AreEqual(0.85f, -g.Reflected.X / g.Reflected.Y, 0.01f);
        }

        [Test]
        public void Predict_HarderShot_SlidesFurtherAlongRope()
        {
            var soft = AimPredictor.Predict(Sim(), new Vec2(5, 2), 0.7f, new Vec2(8, 8));
            var hard = AimPredictor.Predict(Sim(), new Vec2(5, 2), 0.7f, new Vec2(17, 17));
            float softSlide = soft.Exit.Y - soft.Contact.Y;
            float hardSlide = hard.Exit.Y - hard.Contact.Y;
            Assert.Greater(softSlide, 0f);
            Assert.Greater(hardSlide, softSlide + 0.3f);
        }

        [Test]
        public void Predict_WeakShot_StopsBeforeRope()
        {
            var g = AimPredictor.Predict(Sim(), new Vec2(5, 5), 0.7f, new Vec2(2, 0));
            Assert.False(g.ReachesRope);
            Assert.Greater(g.Stop.X, 5f);
            Assert.Less(g.Stop.X, 9.3f);
            Assert.Greater(g.Path.Count, 1);
        }

        [Test]
        public void Predict_MatchesRealShot_ForSeededShots()
        {
            var sim = Sim();
            var rng = new Random(1234);
            for (int k = 0; k < 20; k++)
            {
                double a = rng.NextDouble() * Math.PI * 2;
                float speed = 8f + (float)rng.NextDouble() * 17f;
                var v = new Vec2((float)Math.Cos(a), (float)Math.Sin(a)) * speed;
                var origin = new Vec2(5, 5);
                var g = AimPredictor.Predict(sim, origin, 0.7f, v);
                var r = sim.Simulate(new[] { new Body(origin, 0.7f) }, 0, v);
                var leave = r.Events.First(e => e.Type == SimEventType.RopeLeave);
                Assert.AreEqual(leave.Side, g.Side, "shot " + k);
                Assert.Less((g.Exit - leave.Position).Length, 1e-4f, "shot " + k);
                Assert.Less((g.Reflected - leave.Velocity.Normalized).Length, 1e-4f, "shot " + k);
                Assert.AreEqual(g.Path[g.Path.Count - 1], g.Exit, "shot " + k);
            }
        }
    }
}
