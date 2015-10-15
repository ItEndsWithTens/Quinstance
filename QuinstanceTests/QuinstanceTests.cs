using NUnit.Framework;
using Quinstance;
using System;

namespace QuinstanceTests
{
    [TestFixture]
    public class StripCommentTests
    {
        [TestCase]
        public void StripComment_RemovesCommentAtStartOfLine()
        {
            string actual = Util.StripComment("// StripComment testing");
            Assert.AreEqual("", actual);
        }

        [TestCase]
        public void StripComment_RemovesCommentWithLeadingWhitespace()
        {
            string actual = Util.StripComment("    // StripComment testing");
            Assert.AreEqual("    ", actual);
        }

        [TestCase]
        public void StripComment_RemovesCommentAfterFgdText()
        {
            string actual = Util.StripComment("@SolidClass = worldspawn : \"World entity\" // StripComment testing");
            Assert.AreEqual("@SolidClass = worldspawn : \"World entity\" ", actual);
        }
    }
}
