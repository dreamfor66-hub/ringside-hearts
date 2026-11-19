using System.Linq;
using NUnit.Framework;
using RingsideHearts.Battle.Core;

namespace RingsideHearts.Battle.Tests
{
    public class RingSimTypeTests
    {
        static RingSim Sim() => new RingSim(TestTuning.Default());

        static Body Fighter(float x, float y, int group) => new Body(new Vec2(x, y), 0.7f) { Group = group };

        [Test]
        public void SpeedToTravel_GlidesThatFar()
        {
            var sim = Sim();
            float v = sim.SpeedToTravel(4f);
            var r = sim.Simulate(new[] { new Body(new Vec2(2, 5), 0.7f) }, 0, new Vec2(v, 0));
            Assert.AreEqual(6f, r.FinalPosition(0).X, 0.1f);
        }

        [Test]
        public void Fixed_MoverBouncesOff_FixedStays()
        {
            var table = new Body(new Vec2(5, 6), 0.9f) { Fixed = true };
            var r = Sim().Simulate(new[] { Fighter(5, 2.5f, 0), table }, 0, new Vec2(0, 15));
            var hit = r.Events.First(e => e.Type == SimEventType.BodyHit);
            Assert.AreEqual(0, hit.Body);
            Assert.AreEqual(1, hit.Other);
            Assert.AreEqual(new Vec2(5, 6), r.FinalPosition(1));
            Assert.Less(r.FinalPosition(0).Y, 6f - 1.6f + 1e-3f);
        }

        [Test]
        public void PassThrough_KeepsGoing_TargetPushedLess()
        {
            var normal = Sim().Simulate(new[] { Fighter(5, 2.5f, 0), Fighter(5, 5f, 1) }, 0, new Vec2(0, 15));
            var shooter = Fighter(5, 2.5f, 0);
            shooter.PassThrough = true;
            var pass = Sim().Simulate(new[] { shooter, Fighter(5, 5f, 1) }, 0, new Vec2(0, 15));
            Assert.AreEqual(1, pass.Events.Count(e => e.Type == SimEventType.BodyHit && e.Time < 0.5f));
            Assert.Greater(pass.Frames[60][0].Y, normal.Frames[60][0].Y + 1f, "the stampeder keeps moving forward");
        }

        [Test]
        public void Grab_StopsShooter_ThrowsTargetToPoint()
        {
            var shooter = Fighter(5, 2.5f, 0);
            shooter.Grab = true;
            shooter.GrabTarget = new Vec2(5, 5);
            var r = Sim().Simulate(new[] { shooter, Fighter(7, 4f, 1) }, 0, new Vec2(10, 7.5f));
            Assert.True(r.Events.Any(e => e.Type == SimEventType.BodyHit));
            Assert.Less((r.FinalPosition(1) - new Vec2(5, 5)).Length, 0.8f);
        }

        [Test]
        public void Flier_TakesOffAtRope_IgnoresBodiesInFlight_Lands()
        {
            var shooter = Fighter(5, 5f, 0);
            shooter.Flier = true;
            // straight down into the bottom rope, back up over a fighter standing right in the way
            var r = Sim().Simulate(new[] { shooter, Fighter(5, 3.0f, 1) }, 0, new Vec2(0, -20));
            var takeoff = r.Events.First(e => e.Type == SimEventType.Takeoff);
            var land = r.Events.First(e => e.Type == SimEventType.Landing);
            Assert.Greater(land.Time, takeoff.Time);
            Assert.False(r.Events.Any(e => e.Type == SimEventType.BodyHit && e.Time > takeoff.Time && e.Time < land.Time));
        }
    }
}
