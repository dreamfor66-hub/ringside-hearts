using System.IO;
using System.Linq;
using NUnit.Framework;
using RingsideHearts.Battle.Core;

namespace RingsideHearts.Battle.Tests
{
    public class RingSimHitTests
    {
        static Tuning T() => TestTuning.Default();

        static Vec2 VelocityAfter(ShotResult r, SimEvent e, int body)
        {
            int idx = (int)System.Math.Round(e.Time / r.Dt);
            return (r.Frames[idx + 1][body] - r.Frames[idx][body]) / r.Dt;
        }

        static Body[] HeadOn() => new[] { new Body(new Vec2(5, 2), 0.7f), new Body(new Vec2(5, 6), 0.7f) };

        [Test]
        public void HeadOn_TargetGetsKnockback_AttackerBouncesBack()
        {
            var r = new RingSim(T()).Simulate(HeadOn(), 0, new Vec2(0, 20));
            var hit = r.Events.First(e => e.Type == SimEventType.BodyHit);
            Assert.AreEqual(0, hit.Body);
            Assert.AreEqual(1, hit.Other);
            var target = VelocityAfter(r, hit, 1);
            Assert.AreEqual(0f, target.X, 0.05f);
            Assert.AreEqual(hit.Speed * 0.7f, target.Y, 0.05f);
            Assert.Less(VelocityAfter(r, hit, 0).Y, 0f);
        }

        [Test]
        public void OneContact_OneHitEvent()
        {
            var r = new RingSim(T()).Simulate(HeadOn(), 0, new Vec2(0, 6));
            Assert.AreEqual(1, r.Events.Count(e => e.Type == SimEventType.BodyHit));
            Assert.Greater(r.FinalPosition(1).Y, 6f);
            Assert.Less(r.FinalPosition(0).Y, r.FinalPosition(1).Y);
        }

        [Test]
        public void KnockedTarget_CanBounceOffRope()
        {
            var r = new RingSim(T()).Simulate(HeadOn(), 0, new Vec2(0, 25));
            Assert.True(r.Events.Any(e => e.Type == SimEventType.RopeBounce && e.Body == 1 && e.Side == RopeSide.Top));
        }

        [Test]
        public void OverlappingStart_NoNaN_AndEnds()
        {
            var b = new[] { new Body(new Vec2(5, 5), 0.7f), new Body(new Vec2(5, 5), 0.7f) };
            var r = new RingSim(T()).Simulate(b, 0, new Vec2(3, 0));
            foreach (var f in r.Frames)
                foreach (var p in f)
                    Assert.False(float.IsNaN(p.X) || float.IsNaN(p.Y));
            Assert.LessOrEqual(r.Duration, 20f + 1e-3f);
        }

        [Test]
        public void StartingOnRope_StaysInsideRing()
        {
            var b = new[] { new Body(new Vec2(0.7f, 5), 0.7f) };
            var r = new RingSim(T()).Simulate(b, 0, new Vec2(-15, 2));
            foreach (var f in r.Frames)
            {
                Assert.GreaterOrEqual(f[0].X, 0.7f - 1.0f - 1e-3f); // rope may give up to rope.maxStretch
                Assert.LessOrEqual(f[0].X, 9.3f + 1.0f + 1e-3f);
            }
        }

        [Test]
        public void NonSolidBody_IsPassedThrough()
        {
            var b = new[] { new Body(new Vec2(5, 2), 0.7f), new Body(new Vec2(5, 6), 0.7f, solid: false) };
            var r = new RingSim(T()).Simulate(b, 0, new Vec2(0, 10));
            Assert.AreEqual(0, r.Events.Count(e => e.Type == SimEventType.BodyHit));
        }
    }
}
