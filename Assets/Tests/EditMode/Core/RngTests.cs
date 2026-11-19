using NUnit.Framework;
using RingsideHearts.Battle.Core;

namespace RingsideHearts.Battle.Tests
{
    public class RngTests
    {
        [Test]
        public void SameSeed_SameSequence()
        {
            var a = new Rng(42);
            var b = new Rng(42);
            for (int i = 0; i < 100; i++)
            {
                float x = a.NextFloat();
                Assert.AreEqual(x, b.NextFloat());
                Assert.GreaterOrEqual(x, 0f);
                Assert.Less(x, 1f);
            }
        }

        [Test]
        public void Range_StaysInside()
        {
            var r = new Rng(7);
            for (int i = 0; i < 1000; i++)
            {
                float x = r.Range(1f, 2f);
                Assert.GreaterOrEqual(x, 1f);
                Assert.Less(x, 2f);
                int n = r.Next(3);
                Assert.GreaterOrEqual(n, 0);
                Assert.Less(n, 3);
            }
        }

        [Test]
        public void ZeroSeed_StillVaries()
        {
            var r = new Rng(0);
            Assert.AreNotEqual(r.NextFloat(), r.NextFloat());
        }
    }
}
