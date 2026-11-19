using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using RingsideHearts.Battle.Core;

namespace RingsideHearts.Battle.Tests
{
    public class TuningTests
    {
        [Test]
        public void Get_ReturnsValue_AndThrowsNamedKeyWhenMissing()
        {
            var tu = Tuning.FromCsv("key,value,note\nring.size,10,링 크기\n");
            Assert.AreEqual(10f, tu.Get("ring.size"));
            var ex = Assert.Throws<KeyNotFoundException>(() => tu.Get("nope"));
            StringAssert.Contains("nope", ex.Message);
        }

        [Test]
        public void FromCsv_ParsesInvariantCulture()
        {
            Assert.AreEqual(0.85f, Tuning.FromCsv("key,value,note\nr,0.85,\n").Get("r"), 1e-6f);
        }

        [Test]
        public void Set_OverridesValue()
        {
            var tu = Tuning.FromCsv("key,value,note\nr,1,\n");
            tu.Set("r", 2f);
            Assert.AreEqual(2f, tu.Get("r"));
            Assert.True(tu.Has("r"));
            Assert.False(tu.Has("x"));
        }

        [Test]
        public void SameKeyTwice_Throws()
        {
            var ex = Assert.Throws<System.FormatException>(() => Tuning.FromCsv("key,value,note\na,1,\na,2,\n"));
            StringAssert.Contains("'a'", ex.Message);
        }
    }
}
