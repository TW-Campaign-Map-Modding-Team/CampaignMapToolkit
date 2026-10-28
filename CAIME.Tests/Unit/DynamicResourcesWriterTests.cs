using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CAIME.Exporters;
using CAIME.Tests.Helpers;
using EsfLibrary;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Unit
{
    [TestClass]
    public class DynamicResourcesWriterTests
    {
        /// <summary>When the Assembly Kit built TestData/dynamic_resources/wh3_main_prologue_map.esf.</summary>
        private static readonly DateTime PrologueBuildTime = new DateTime(2026, 10, 28, 3, 23, 34, DateTimeKind.Utc);

        private string _dir;

        [TestInitialize]
        public void Setup()
        {
            _dir = MapHexHarness.CreateTempDir("caime_dynamic_resources");
        }

        [TestCleanup]
        public void Cleanup()
        {
            MapHexHarness.DeleteTempDir(_dir);
        }

        private static string FindTestData(string relativePath)
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            for (int i = 0; i < 10 && dir != null; ++i)
            {
                var candidate = Path.Combine(dir, "TestData", relativePath);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                dir = Path.GetDirectoryName(dir);
            }

            Assert.Inconclusive($"TestData/{relativePath} was not found.");
            return null;
        }

        [TestMethod]
        public void Write_NoRegions_MatchesTheAssemblyKitsWarhammer3Build()
        {
            var reference = File.ReadAllBytes(FindTestData(Path.Combine("dynamic_resources", "wh3_main_prologue_map.esf")));

            var written = DynamicResourcesWriter.Write(new List<DynamicResourceRegion>(), PrologueBuildTime);

            CollectionAssert.AreEqual(reference, written);
        }

        [TestMethod]
        public void BuildTimeStamp_LeavesTheHourUnpadded()
        {
            Assert.AreEqual("2026102832334", DynamicResourcesWriter.BuildTimeStamp(PrologueBuildTime));
            Assert.AreEqual("20141114153252", DynamicResourcesWriter.BuildTimeStamp(new DateTime(2014, 11, 14, 15, 32, 52, DateTimeKind.Utc)));
        }

        [TestMethod]
        public void Write_Regions_ReadBackAsAttilaLaysThemOut()
        {
            var area = new DynamicResourceArea { BuildingSuperchain = "att_sch_agriculture", Level = 2, TileUpgradeId = "farmland_mediterranean" };
            area.HexGroups.Add(new List<ushort> { 484, 407, 484, 408 });
            var region = new DynamicResourceRegion { Region = 173 };
            region.Areas.Add(area);

            var path = Path.Combine(_dir, "dynamic_resources.esf");
            File.WriteAllBytes(path, DynamicResourcesWriter.Write(new[] { region }, PrologueBuildTime));
            var root = (ParentNode)EsfCodecUtil.LoadEsfFile(path).RootNode;

            var regionEntry = root.Children.Single().Children.Single();
            Assert.AreEqual((ushort)(0xFE00 | 173), ((EsfValueNode<ushort>)regionEntry.Values[0]).Value);

            var areasBlock = regionEntry.Children.Single();
            Assert.AreEqual("REGION_RESOURCE_INFO_AREAS_BLOCK", areasBlock.Name);
            Assert.AreEqual(1, areasBlock.Version);

            var areaEntry = areasBlock.Children.Single();
            Assert.AreEqual("att_sch_agriculture", ((EsfValueNode<string>)areaEntry.Values[0]).Value);
            Assert.AreEqual(2u, ((EsfValueNode<uint>)areaEntry.Values[1]).Value);
            Assert.AreEqual("farmland_mediterranean", ((EsfValueNode<string>)areaEntry.Values[2]).Value);

            var group = areaEntry.Children.Single().Children.Single();
            CollectionAssert.AreEqual(new ushort[] { 484, 407, 484, 408 }, ((EsfValueNode<ushort[]>)group.Values[0]).Value);
        }
    }
}
