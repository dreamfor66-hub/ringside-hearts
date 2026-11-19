using NUnit.Framework;
using RingsideHearts.Battle.Core;

namespace RingsideHearts.Battle.Tests
{
    public class Vec2Tests
    {
        [Test]
        public void Reflect_FlipsNormalComponent()
        {
            var r = Vec2.Reflect(new Vec2(3, -4), new Vec2(0, 1));
            Assert.AreEqual(3f, r.X, 1e-5f);
            Assert.AreEqual(4f, r.Y, 1e-5f);
        }

        [Test]
        public void Normalized_OfZero_IsZero()
        {
            Assert.AreEqual(0f, Vec2.Zero.Normalized.Length);
        }

        [Test]
        public void Normalized_HasUnitLength()
        {
            Assert.AreEqual(1f, new Vec2(3, 4).Normalized.Length, 1e-5f);
        }
    }
}
