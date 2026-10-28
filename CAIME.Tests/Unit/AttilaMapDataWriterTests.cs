using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CAIME.Exporters;
using CAIME.Tests.Helpers;
using EsfLibrary;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Unit
{
    /// <summary>
    /// Builds Attila's main_attila_map map_data.esf from Templates/main_attila_map/map.hex and
    /// compares it, node by node, with the game's own file in TestData.
    ///
    /// <para>
    /// The template's map.hex lacks what the slot positions, the campaign UI's settlement positions
    /// and the port docks are made from, so those are left out of the comparison; everything else
    /// must match.
    /// </para>
    /// </summary>
    [TestClass]
    public class AttilaMapDataWriterTests
    {
        private const string THEATRE_INDEX  = "1564135515";
        private const float  MAX_X          = 678.5f;

        /// <summary>The last three values of REGION_DATA: the dock's region, column and row.</summary>
        private const int DOCK_VALUES = 3;

        private string _dir;

        [TestInitialize]
        public void Setup()
        {
            MapHexHarness.ResetStaticMaskState();
            _dir = MapHexHarness.CreateTempDir("caime_attila_map_data");
        }

        [TestCleanup]
        public void Cleanup()
        {
            MapHexHarness.DeleteTempDir(_dir);
        }

        private static string FindRepoFile(params string[] parts)
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            for (int i = 0; i < 10 && dir != null; ++i)
            {
                var candidate = Path.Combine(new[] { dir }.Concat(parts).ToArray());
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                dir = Path.GetDirectoryName(dir);
            }

            Assert.Inconclusive($"{Path.Combine(parts)} was not found.");
            return null;
        }

        [TestMethod]
        public void Write_MainAttilaMap_MatchesTheGamesFileApartFromPositionsAndDocks()
        {
            var map = new MapHexFile();
            Assert.IsTrue(map.Load(FindRepoFile("Templates", "main_attila_map", "map.hex")));

            var written = Path.Combine(_dir, "map_data.esf");
            File.WriteAllBytes(written, AttilaMapDataWriter.Write(map, new MapDataTheatre { Index = THEATRE_INDEX, MaxX = MAX_X }, 0));

            var expected    = EsfCodecUtil.LoadEsfFile(FindRepoFile("TestData", "campaign_maps", "main_attila_map", "map_data.esf")).RootNode;
            var actual      = EsfCodecUtil.LoadEsfFile(written).RootNode;
            var differences = new List<string>();

            Compare(expected, actual, "", differences);

            Assert.AreEqual(0, differences.Count, string.Join(Environment.NewLine, differences.Take(20)));
        }

        private static void Compare(EsfNode expected, EsfNode actual, string path, List<string> differences)
        {
            if (differences.Count > 20)
            {
                return;
            }

            if (expected is ParentNode expectedParent && actual is ParentNode actualParent)
            {
                path += "/" + expectedParent.Name;
                if (expectedParent.Name != actualParent.Name || expectedParent.Version != actualParent.Version)
                {
                    differences.Add($"{path}: {actualParent.Name} v{actualParent.Version}");
                    return;
                }

                if (expectedParent.Name.EndsWith("POSITIONS_BLOCK") || expectedParent.Name == "REGION_KEYS")
                {
                    return;
                }

                var expectedNodes   = expectedParent.Value;
                var actualNodes     = actualParent.Value;
                if (expectedNodes.Count != actualNodes.Count)
                {
                    differences.Add($"{path}: {actualNodes.Count} nodes, expected {expectedNodes.Count}");
                    return;
                }

                bool isRegion = expectedParent.Name == "REGION_DATA";
                for (int i = 0; i < expectedNodes.Count; ++i)
                {
                    if (isRegion && i >= expectedNodes.Count - DOCK_VALUES)
                    {
                        continue;
                    }

                    Compare(expectedNodes[i], actualNodes[i], $"{path}[{i}]", differences);
                }

                return;
            }

            var expectedValue   = expected.GetType().GetProperty("Value").GetValue(expected);
            var actualValue     = actual.GetType().GetProperty("Value").GetValue(actual);
            bool same = expectedValue is IStructuralEquatable structural
                ? structural.Equals(actualValue, StructuralComparisons.StructuralEqualityComparer)
                : Equals(expectedValue, actualValue);

            if (same == false || expected.TypeCode != actual.TypeCode)
            {
                differences.Add($"{path}: {actualValue} ({actual.TypeCode}), expected {expectedValue} ({expected.TypeCode})");
            }
        }
    }
}
