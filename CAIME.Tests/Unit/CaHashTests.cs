using System.Linq;
using EsfLibrary;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Unit
{
    [TestClass]
    public class CaHashTests
    {
        [TestMethod]
        [DataRow("",    0x00000000u)]
        [DataRow("ab",  0x00620061u)]
        [DataRow("abc", 0x006200C4u)]
        public void RomeII_SumsEvenCharactersLowAndOddCharactersHigh(string value, uint expected)
        {
            Assert.AreEqual(expected, CaStringHash.RomeII(value));
        }

        [TestMethod]
        [DataRow("",   0xF73A51C9u)]
        [DataRow("a",  0xF73A51A8u)]
        [DataRow("ab", 0xF73A30ABu)]
        public void Attila_XorsCharactersInFromTheEnd(string value, uint expected)
        {
            Assert.AreEqual(expected, CaStringHash.Attila(value));
        }

        [TestMethod]
        public void Hashes_TakeTheAsciiBytesTheFileHolds()
        {
            Assert.AreEqual(CaStringHash.Attila("a?b"), CaStringHash.Attila("aéb"));
            Assert.AreEqual(CaStringHash.RomeII("a?b"), CaStringHash.RomeII("aéb"));
        }

        [TestMethod]
        public void Iterate_KeepsFourBucketsThroughTheFourthKey()
        {
            var order = CaHashMapOrder.Iterate(new uint[] { 4, 8, 1, 2 }, key => key);

            CollectionAssert.AreEqual(new uint[] { 4, 8, 1, 2 }, order);
        }

        [TestMethod]
        public void Iterate_RehashesToNineBucketsOnTheFifthKey()
        {
            var order = CaHashMapOrder.Iterate(new uint[] { 4, 8, 1, 2, 3 }, key => key);

            CollectionAssert.AreEqual(new uint[] { 1, 2, 3, 4, 8 }, order);
        }

        [TestMethod]
        public void Iterate_KeepsNineBucketsThroughTheNinthKey()
        {
            var order = CaHashMapOrder.Iterate(Enumerable.Range(1, 9).Select(i => (uint)i), key => key);

            CollectionAssert.AreEqual(new uint[] { 9, 1, 2, 3, 4, 5, 6, 7, 8 }, order);
        }

        [TestMethod]
        public void Iterate_RehashesToNineteenBucketsOnTheTenthKey()
        {
            var order = CaHashMapOrder.Iterate(Enumerable.Range(1, 10).Select(i => (uint)i), key => key);

            CollectionAssert.AreEqual(Enumerable.Range(1, 10).Select(i => (uint)i).ToArray(), order);
        }
    }
}
