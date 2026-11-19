using NUnit.Framework;
using RingsideHearts.Battle.Core;

namespace RingsideHearts.Battle.Tests
{
    public class CsvTableTests
    {
        [Test]
        public void Parse_HandlesQuotedCommaEscapedQuoteBomAndSkipsComments()
        {
            var t = CsvTable.Parse("﻿key,value,note\n# 주석\n\na,1,\"x, y\"\nb,2,\"say \"\"hi\"\"\"\n");
            Assert.AreEqual(2, t.RowCount);
            Assert.AreEqual("key", t.Header[0]);
            Assert.AreEqual("x, y", t.Get(0, "note"));
            Assert.AreEqual("say \"hi\"", t.Get(1, "note"));
        }

        [Test]
        public void Parse_HandlesCrLfLineEndings()
        {
            var t = CsvTable.Parse("key,value\r\na,1\r\nb,2\r\n");
            Assert.AreEqual(2, t.RowCount);
            Assert.AreEqual("2", t.Get(1, "value"));
        }

        [Test]
        public void Get_MissingTrailingCell_ReturnsEmpty()
        {
            var t = CsvTable.Parse("key,value,note\na,1\n");
            Assert.AreEqual("", t.Get(0, "note"));
        }
    }
}
