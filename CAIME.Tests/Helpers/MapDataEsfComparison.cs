using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EsfLibrary;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Helpers
{
    /// <summary>
    /// Compares a written map_data.esf with a game's own file node by node, optionally leaving out
    /// some bytes of one array, for an input that is older than the game's.
    /// </summary>
    public sealed class MapDataEsfComparison
    {
        private const int MAX_DIFFERENCES = 20;

        /// <summary>For a byte array under the named record, which bytes are compared.</summary>
        public string                       MaskedArrayParent   { get; set; }
        public Func<int, bool>              CompareByte         { get; set; } = index => true;

        /// <summary>
        /// A record whose version is not compared, and which of its nodes in the game's file are
        /// left out, for a game file written by an older builder than the Assembly Kit's.
        /// </summary>
        public string                       OlderRecord         { get; set; }
        public Func<EsfNode, bool>          OlderRecordExtra    { get; set; } = node => false;

        /// <summary>A record that is not compared at all, such as one holding a save time.</summary>
        public string                       SkippedRecord       { get; set; }

        public List<string>                 Differences         { get; } = new List<string>();

        /// <summary>Finds a file by its path from the repository root, or marks the test inconclusive.</summary>
        public static string FindRepoFile(params string[] parts)
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

        public void AssertSame(string expectedPath, string actualPath)
        {
            Compare(EsfCodecUtil.LoadEsfFile(expectedPath).RootNode, EsfCodecUtil.LoadEsfFile(actualPath).RootNode, "", null);
            Assert.AreEqual(0, Differences.Count, string.Join(Environment.NewLine, Differences));
        }

        private void Compare(EsfNode expected, EsfNode actual, string path, string parentName)
        {
            if (Differences.Count >= MAX_DIFFERENCES)
            {
                return;
            }

            if (expected is ParentNode expectedParent && actual is ParentNode actualParent)
            {
                path += "/" + expectedParent.Name;
                if (expectedParent.Name == SkippedRecord && actualParent.Name == SkippedRecord)
                {
                    return;
                }

                bool older = expectedParent.Name == OlderRecord;
                if (expectedParent.Name != actualParent.Name || (expectedParent.Version != actualParent.Version && older == false))
                {
                    Differences.Add($"{path}: {actualParent.Name} v{actualParent.Version}");
                    return;
                }

                var expectedNodes   = older ? expectedParent.Value.Where(node => OlderRecordExtra(node) == false).ToList() : expectedParent.Value;
                var actualNodes     = actualParent.Value;
                if (expectedNodes.Count != actualNodes.Count)
                {
                    Differences.Add($"{path}: {actualNodes.Count} nodes, expected {expectedNodes.Count}");
                    return;
                }

                for (int i = 0; i < expectedNodes.Count; ++i)
                {
                    Compare(expectedNodes[i], actualNodes[i], $"{path}[{i}]", expectedParent.Name);
                }

                return;
            }

            var expectedValue   = expected.GetType().GetProperty("Value").GetValue(expected);
            var actualValue     = actual.GetType().GetProperty("Value").GetValue(actual);

            if (parentName == MaskedArrayParent && expectedValue is byte[] expectedBytes && actualValue is byte[] actualBytes)
            {
                expectedValue   = Masked(expectedBytes);
                actualValue     = Masked(actualBytes);
            }

            bool same = expectedValue is IStructuralEquatable structural
                ? structural.Equals(actualValue, StructuralComparisons.StructuralEqualityComparer)
                : Equals(expectedValue, actualValue);

            if (same == false || expected.TypeCode != actual.TypeCode)
            {
                Differences.Add($"{path}: {actualValue} ({actual.TypeCode}), expected {expectedValue} ({expected.TypeCode})");
            }
        }

        private byte[] Masked(byte[] bytes)
        {
            return bytes.Where((value, index) => CompareByte(index)).ToArray();
        }
    }
}
