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

        // One string for each tail length, and one with whole blocks and a tail.
        [TestMethod]
        [DataRow("",                 0x5FE25914u)]
        [DataRow("a",                0xBC04625Eu)]
        [DataRow("ab",               0xB7368998u)]
        [DataRow("abc",              0x7195E3E8u)]
        [DataRow("abcd",             0xDF83A461u)]
        [DataRow("wh_main_sea_lake", 0xE36BE33Au)]
        public void Murmur_IsMurmurHash3SeededWithTheBuildersSeed(string value, uint expected)
        {
            Assert.AreEqual(expected, CaStringHash.Murmur(value));
        }

        [TestMethod]
        public void Iterate_FourBucketMap_KeepsFourBucketsThroughTheFourthKey()
        {
            var order = CaHashMap.Attila.Iterate(new uint[] { 4, 8, 1, 2 }, key => key);

            CollectionAssert.AreEqual(new uint[] { 4, 8, 1, 2 }, order);
        }

        [TestMethod]
        public void Iterate_FourBucketMap_RehashesToNineBucketsOnTheFifthKey()
        {
            var order = CaHashMap.Attila.Iterate(new uint[] { 4, 8, 1, 2, 3 }, key => key);

            CollectionAssert.AreEqual(new uint[] { 1, 2, 3, 4, 8 }, order);
        }

        [TestMethod]
        public void Iterate_FourBucketMap_KeepsNineBucketsThroughTheNinthKey()
        {
            var order = CaHashMap.Attila.Iterate(Enumerable.Range(1, 9).Select(i => (uint)i), key => key);

            CollectionAssert.AreEqual(new uint[] { 9, 1, 2, 3, 4, 5, 6, 7, 8 }, order);
        }

        [TestMethod]
        public void Iterate_FourBucketMap_RehashesToNineteenBucketsOnTheTenthKey()
        {
            var order = CaHashMap.Attila.Iterate(Enumerable.Range(1, 10).Select(i => (uint)i), key => key);

            CollectionAssert.AreEqual(Enumerable.Range(1, 10).Select(i => (uint)i).ToArray(), order);
        }

        [TestMethod]
        public void Iterate_OneBucketMap_RehashesToThreeBucketsOnTheSecondKey()
        {
            var order = CaHashMap.Murmur.Iterate(new uint[] { 2, 3 }, key => key);

            CollectionAssert.AreEqual(new uint[] { 3, 2 }, order);
        }

        [TestMethod]
        public void Iterate_OneBucketMap_KeepsThreeBucketsThroughTheThirdKey()
        {
            var order = CaHashMap.Murmur.Iterate(new uint[] { 2, 3, 1 }, key => key);

            CollectionAssert.AreEqual(new uint[] { 3, 1, 2 }, order);
        }

        [TestMethod]
        public void Iterate_OneBucketMap_RehashesToSevenBucketsOnTheFourthKey()
        {
            var order = CaHashMap.Murmur.Iterate(new uint[] { 2, 3, 1, 6 }, key => key);

            CollectionAssert.AreEqual(new uint[] { 1, 2, 3, 6 }, order);
        }
    }
}
