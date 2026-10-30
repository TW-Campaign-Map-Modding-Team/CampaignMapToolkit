using System;
using System.Collections.Generic;
using System.Linq;
using CAIME.Exporters;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Unit
{
    /// <summary>
    /// <see cref="DynamicResourceRegions.GroupHexes(MapDataGrid, int[], int)"/> works out the
    /// builder's merge passes from which groups touch. These tests hold it to the builder's own
    /// procedure, which retests every hex of every later group against each group.
    /// </summary>
    [TestClass]
    public class DynamicResourceGroupingTests
    {
        [TestMethod]
        public void GroupHexes_RandomMaps_MatchTheBuildersMergePasses()
        {
            var random = new Random(20261030);
            int mapsMergedOverSeveralPasses = 0;

            for (int map = 0; map < 300; ++map)
            {
                int width       = random.Next(1, 40);
                int height      = random.Next(1, 40);
                int keyCount    = random.Next(1, 4);
                double density  = random.NextDouble();

                var grid    = new MapDataGrid(width, height);
                var hexKeys = Enumerable.Range(0, grid.HexCount)
                    .Select(_ => random.NextDouble() < density ? random.Next(keyCount) : -1)
                    .ToArray();

                var expected    = BuildersGrouping(grid, hexKeys, keyCount, out int passes);
                var actual      = DynamicResourceRegions.GroupHexes(grid, hexKeys, keyCount);

                for (int key = 0; key < keyCount; ++key)
                {
                    Assert.AreEqual(expected[key].Count, actual[key].Count, $"Map {map}, key {key}: group count.");
                    for (int group = 0; group < expected[key].Count; ++group)
                    {
                        CollectionAssert.AreEqual(expected[key][group], actual[key][group], $"Map {map}, key {key}, group {group}.");
                    }
                }

                if (passes > 2)
                {
                    ++mapsMergedOverSeveralPasses;
                }
            }

            Assert.IsTrue(mapsMergedOverSeveralPasses > 0, "No map needed a second merging pass, so the test did not cover one.");
        }

        /// <summary>The builder's procedure, as DynamicResourceRegions first reproduced it.</summary>
        private static List<List<int>>[] BuildersGrouping(MapDataGrid grid, int[] hexKeys, int keyCount, out int mostPasses)
        {
            var groups = Enumerable.Range(0, keyCount).Select(_ => new List<HashSet<int>>()).ToArray();
            var orders = Enumerable.Range(0, keyCount).Select(_ => new List<List<int>>()).ToArray();

            bool Touches(HashSet<int> members, int hexIndex) =>
                Enumerable.Range(0, MapDataGrid.NEIGHBOUR_COUNT).Any(n => members.Contains(grid.Neighbour(hexIndex, n)));

            for (int column = 0; column < grid.Width; ++column)
            {
                for (int row = 0; row < grid.Height; ++row)
                {
                    int hexIndex = row * grid.Width + column;
                    int key      = hexKeys[hexIndex];
                    if (key < 0)
                    {
                        continue;
                    }

                    int group = groups[key].FindIndex(members => Touches(members, hexIndex));
                    if (group < 0)
                    {
                        group = groups[key].Count;
                        groups[key].Add(new HashSet<int>());
                        orders[key].Add(new List<int>());
                    }

                    groups[key][group].Add(hexIndex);
                    orders[key][group].Add(hexIndex);
                }
            }

            mostPasses = 0;
            for (int key = 0; key < keyCount; ++key)
            {
                var members = groups[key];
                var order   = orders[key];

                bool merged;
                int passes = 0;
                do
                {
                    ++passes;
                    merged = false;
                    for (int i = 0; i < members.Count; ++i)
                    {
                        for (int j = i + 1; j < members.Count && order[i].Count > 0; ++j)
                        {
                            if (order[j].Any(hexIndex => Touches(members[i], hexIndex)))
                            {
                                members[i].UnionWith(members[j]);
                                order[i].AddRange(order[j]);
                                members[j].Clear();
                                order[j].Clear();
                                merged = true;
                            }
                        }
                    }

                    for (int i = order.Count - 1; i >= 0; --i)
                    {
                        if (order[i].Count == 0)
                        {
                            members.RemoveAt(i);
                            order.RemoveAt(i);
                        }
                    }
                }
                while (merged);

                mostPasses = Math.Max(mostPasses, passes);
            }

            return orders;
        }
    }
}
