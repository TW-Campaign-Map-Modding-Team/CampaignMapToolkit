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

        /// <summary>
        /// Templates/main_attila_map holds the inputs of TestData/dynamic_resources/main_attila_map.esf,
        /// Attila's own build. Everything must match except the hex order of the one group
        /// <see cref="DynamicResourceRegions"/> documents as not reproduced.
        /// </summary>
        [TestMethod]
        public void Find_AttilaMainMap_MatchesTheGamesFile()
        {
            var reference   = FindTestData(Path.Combine("dynamic_resources", "main_attila_map.esf"));
            var templates   = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(reference))), "Templates", "main_attila_map");

            var map = new MapHexFile();
            Assert.IsTrue(map.Load(Path.Combine(templates, "map.hex")));

            var found = DynamicResourceRegions.Find(map,
                DynamicResourcesImage.Load(Path.Combine(templates, "dynamic_resources.png")),
                DynamicResourceType.LoadDatabase(Path.Combine(templates, "dynamic_resources_database.xml")));

            var expected = ReadRegions(reference);

            CollectionAssert.AreEqual(expected.Select(region => region.Region).ToArray(), found.Select(region => region.Region).ToArray(), "region order");

            int groupsInADifferentHexOrder = 0;
            for (int r = 0; r < expected.Count; ++r)
            {
                var expectedAreas   = expected[r].Areas;
                var foundAreas      = found[r].Areas;
                Assert.AreEqual(expectedAreas.Count, foundAreas.Count, $"region {expected[r].Region}: areas");

                for (int a = 0; a < expectedAreas.Count; ++a)
                {
                    var context = $"region {expected[r].Region}, area {a}";
                    Assert.AreEqual(expectedAreas[a].BuildingSuperchain, foundAreas[a].BuildingSuperchain, context);
                    Assert.AreEqual(expectedAreas[a].Level, foundAreas[a].Level, context);
                    Assert.AreEqual(expectedAreas[a].TileUpgradeId, foundAreas[a].TileUpgradeId, context);
                    Assert.AreEqual(expectedAreas[a].HexGroups.Count, foundAreas[a].HexGroups.Count, $"{context}: groups");

                    for (int g = 0; g < expectedAreas[a].HexGroups.Count; ++g)
                    {
                        var expectedHexes   = Pairs(expectedAreas[a].HexGroups[g]);
                        var foundHexes      = Pairs(foundAreas[a].HexGroups[g]);

                        CollectionAssert.AreEquivalent(expectedHexes, foundHexes, $"{context}, group {g}: hexes");
                        if (expectedHexes.SequenceEqual(foundHexes) == false)
                        {
                            ++groupsInADifferentHexOrder;
                        }
                    }
                }
            }

            Assert.AreEqual(1, groupsInADifferentHexOrder, "Only the one documented group may list its hexes in another order.");
        }

        private static List<(ushort, ushort)> Pairs(List<ushort> coordinates)
        {
            var pairs = new List<(ushort, ushort)>();
            for (int i = 0; i < coordinates.Count; i += 2)
            {
                pairs.Add((coordinates[i], coordinates[i + 1]));
            }

            return pairs;
        }

        private static List<DynamicResourceRegion> ReadRegions(string path)
        {
            var root    = (ParentNode)EsfCodecUtil.LoadEsfFile(path).RootNode;
            var regions = new List<DynamicResourceRegion>();

            foreach (var regionEntry in root.Children.Single().Children)
            {
                var region = new DynamicResourceRegion { Region = ((EsfValueNode<ushort>)regionEntry.Values[0]).Value & 0x1FF };

                foreach (var areaEntry in regionEntry.Children.Single().Children)
                {
                    var area = new DynamicResourceArea
                    {
                        BuildingSuperchain  = ((EsfValueNode<string>)areaEntry.Values[0]).Value,
                        Level               = ((EsfValueNode<uint>)areaEntry.Values[1]).Value,
                        TileUpgradeId       = ((EsfValueNode<string>)areaEntry.Values[2]).Value,
                    };

                    foreach (var groupEntry in areaEntry.Children.Single().Children)
                    {
                        area.HexGroups.Add(((EsfValueNode<ushort[]>)groupEntry.Values[0]).Value.ToList());
                    }

                    region.Areas.Add(area);
                }

                regions.Add(region);
            }

            return regions;
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
