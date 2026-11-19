using System.IO;
using System.Linq;
using NUnit.Framework;
using RingsideHearts.Battle.Core;

namespace RingsideHearts.Battle.Tests
{
    public class RingSimRopeTests
    {
        static Tuning T() => TestTuning.Default();

        static Vec2 VelocityAfter(ShotResult r, SimEvent e, int body)
        {
            int idx = (int)System.Math.Round(e.Time / r.Dt);
            return (r.Frames[idx + 1][body] - r.Frames[idx][body]) / r.Dt;
        }

        [Test]
        public void StraightShot_StopsAtKinematicDistance()
        {
            var b = new[] { new Body(new Vec2(5, 1), 0.7f) };
            var r = new RingSim(T()).Simulate(b, 0, new Vec2(0, 6));
            // dv/dt = -(c + k v): distance = v0/k - c/k^2 ln(1 + k v0 / c), c=1.5, k=0.6
            double c = 1.5, k = 0.6, v0 = 6;
            double expected = v0 / k - c / (k * k) * System.Math.Log(1 + k * v0 / c);
            Assert.AreEqual(1f + (float)expected, r.FinalPosition(0).Y, 0.1f);
            Assert.AreEqual(5f, r.FinalPosition(0).X, 1e-4f);
            Assert.AreEqual(SimEventType.Stopped, r.Events[r.Events.Count - 1].Type);
        }

        [Test]
        public void RightRope_ReflectsX_KeepsRestitution_AndReportsSide()
        {
            var b = new[] { new Body(new Vec2(5, 5), 0.7f) };
            var r = new RingSim(T()).Simulate(b, 0, new Vec2(20, 0));
            var e = r.Events.First(x => x.Type == SimEventType.RopeBounce);
            Assert.AreEqual(RopeSide.Right, e.Side);
            Assert.AreEqual(0, e.Body);
            Assert.AreEqual(-1, e.Other);
            Assert.AreEqual(10f - 0.7f, e.Position.X, 0.05f);
            Assert.AreEqual(e.Speed, e.NormalSpeed, 1e-3f);
            // after the rope gives the body back (center inside the rope line again)
            int idx = (int)System.Math.Round(e.Time / r.Dt);
            while (r.Frames[idx][0].X > 9.3f) idx++;
            var after = (r.Frames[idx + 1][0] - r.Frames[idx][0]) / r.Dt;
            Assert.Less(after.X, 0f);
            Assert.AreEqual(e.Speed * 0.85f, after.Length, 0.1f);
        }

        [Test]
        public void Rope_StretchesOutward_ThenGivesBack()
        {
            var b = new[] { new Body(new Vec2(5, 5), 0.7f) };
            var r = new RingSim(T()).Simulate(b, 0, new Vec2(20, 0));
            float maxX = r.Frames.Max(f => f[0].X);
            Assert.Greater(maxX, 9.3f + 0.3f, "rope should give");
            Assert.LessOrEqual(maxX, 9.3f + 1.0f + 1e-3f, "capped by rope.maxStretch");
            int outside = r.Frames.Count(f => f[0].X > 9.3f);
            Assert.Greater(outside * r.Dt, 0.05f, "contact should last a moment");
        }

        [Test]
        public void Deceleration_IsGentlerWhenSlow()
        {
            var b = new[] { new Body(new Vec2(5, 1), 0.7f) };
            var r = new RingSim(T()).Simulate(b, 0, new Vec2(0, 8));
            float Speed(int i) => (r.Frames[i + 1][0] - r.Frames[i][0]).Length / r.Dt;
            float fastDecel = Speed(1) - Speed(2);
            int last = r.Frames.Count - 3;
            float slowDecel = Speed(last - 1) - Speed(last);
            Assert.Less(slowDecel, fastDecel);
        }

        [Test]
        public void CornerShot_ReportsTwoSides()
        {
            var b = new[] { new Body(new Vec2(8.8f, 8.8f), 0.7f) };
            var r = new RingSim(T()).Simulate(b, 0, new Vec2(10, 10));
            var sides = r.Events.Where(x => x.Type == SimEventType.RopeBounce).Select(x => x.Side).ToList();
            CollectionAssert.Contains(sides, RopeSide.Right);
            CollectionAssert.Contains(sides, RopeSide.Top);
        }

        [Test]
        public void SameInput_GivesIdenticalFrames()
        {
            var b = new[] { new Body(new Vec2(3, 2), 0.7f) };
            var r1 = new RingSim(T()).Simulate(b, 0, new Vec2(17, 11));
            var r2 = new RingSim(T()).Simulate(b, 0, new Vec2(17, 11));
            Assert.AreEqual(r1.Frames.Count, r2.Frames.Count);
            for (int i = 0; i < r1.Frames.Count; i++)
                Assert.AreEqual(r1.Frames[i][0], r2.Frames[i][0], "frame " + i);
        }

        [Test]
        public void Simulation_AlwaysEnds_WithinMaxSeconds()
        {
            var tu = T();
            tu.Set("ring.friction", 0f); // would roll forever without the cap
            var b = new[] { new Body(new Vec2(5, 5), 0.7f) };
            var r = new RingSim(tu).Simulate(b, 0, new Vec2(10, 3));
            Assert.LessOrEqual(r.Duration, 20f + 1e-3f);
            Assert.AreEqual(SimEventType.Stopped, r.Events[r.Events.Count - 1].Type);
        }

        [Test]
        public void Simulate_DoesNotMutateInputBodies()
        {
            var b = new[] { new Body(new Vec2(5, 5), 0.7f) };
            new RingSim(T()).Simulate(b, 0, new Vec2(12, 7));
            Assert.AreEqual(new Vec2(5, 5), b[0].Position);
            Assert.AreEqual(Vec2.Zero, b[0].Velocity);
        }

        [Test]
        public void FirstFrame_IsStartPositions()
        {
            var b = new[] { new Body(new Vec2(2, 3), 0.7f), new Body(new Vec2(7, 8), 0.7f) };
            var r = new RingSim(T()).Simulate(b, 0, new Vec2(1, 0));
            Assert.AreEqual(new Vec2(2, 3), r.Frames[0][0]);
            Assert.AreEqual(new Vec2(7, 8), r.Frames[0][1]);
        }
    }
}
