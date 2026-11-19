using NUnit.Framework;
using RingsideHearts.Battle.Core;

namespace RingsideHearts.Battle.Tests
{
    public class HitFeelTests
    {
        static Tuning T()
        {
            var t = new Tuning();
            t.Set("hitStop.seconds", 0.06f);
            t.Set("hitStop.perSpeed", 0.002f);
            t.Set("hitStop.maxSeconds", 0.1f);
            t.Set("ropeSlow.minSpeed", 4f);
            t.Set("ropeSlow.fullSpeed", 20f);
            t.Set("ropeSlow.scale", 0.2f);
            t.Set("ropeSlow.seconds", 0.3f);
            return t;
        }

        static SimEvent Hit(float normalSpeed) => new SimEvent { Type = SimEventType.BodyHit, NormalSpeed = normalSpeed };
        static SimEvent Rope(float normalSpeed) => new SimEvent { Type = SimEventType.RopeBounce, NormalSpeed = normalSpeed };

        [Test]
        public void NoEvents_TimePassesNormally()
        {
            var f = new HitFeel(T());
            Assert.AreEqual(0.016f, f.Advance(0.016f), 1e-6f);
        }

        [Test]
        public void BodyHit_FreezesForBasePlusSpeedShare()
        {
            var f = new HitFeel(T());
            f.Trigger(Hit(10f)); // 0.06 + 10 * 0.002 = 0.08 s
            Assert.AreEqual(0f, f.Advance(0.05f), 1e-6f);
            Assert.True(f.Holding);
            Assert.AreEqual(0.02f, f.Advance(0.05f), 1e-5f); // 0.03 of stop left, the rest passes
            Assert.False(f.Holding);
        }

        [Test]
        public void BodyHit_StopIsCappedAtMax()
        {
            var f = new HitFeel(T());
            f.Trigger(Hit(100f));
            Assert.AreEqual(0f, f.Advance(0.1f - 1e-4f), 1e-6f);
            Assert.AreEqual(0.05f, f.Advance(0.05f + 1e-4f), 1e-4f);
        }

        [Test]
        public void WeakRopeHit_NoSlow()
        {
            var f = new HitFeel(T());
            f.Trigger(Rope(3f));
            Assert.AreEqual(1f, f.TimeScale, 1e-6f);
        }

        [Test]
        public void FullRopeHit_SlowsToScale_ThenRecovers()
        {
            var f = new HitFeel(T());
            f.Trigger(Rope(25f));
            Assert.AreEqual(0.2f, f.TimeScale, 1e-4f);
            float passed = 0f;
            for (int i = 0; i < 30; i++) passed += f.Advance(0.01f); // 0.3 s real
            Assert.Less(passed, 0.3f * 0.75f);
            Assert.AreEqual(1f, f.TimeScale, 1e-4f);
            Assert.AreEqual(0.01f, f.Advance(0.01f), 1e-6f);
        }

        [Test]
        public void HarderRopeHit_SlowsMore()
        {
            var soft = new HitFeel(T());
            var hard = new HitFeel(T());
            soft.Trigger(Rope(8f));
            hard.Trigger(Rope(16f));
            Assert.Less(hard.TimeScale, soft.TimeScale);
            Assert.Less(soft.TimeScale, 1f);
        }

        [Test]
        public void WeakerRopeHitDuringSlow_DoesNotCutItShort()
        {
            var f = new HitFeel(T());
            f.Trigger(Rope(20f));
            float before = f.TimeScale;
            f.Trigger(Rope(6f));
            Assert.AreEqual(before, f.TimeScale, 1e-6f);
        }
    }
}
