using System.Collections.Generic;
using System.Linq;
using CAIME;
using CAIME.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Unit
{
    /// <summary>
    /// Covers the warning <see cref="MapHexEditor"/> logs when a list outgrows the per-hex index field
    /// that refers to it. Past that point saving fails as soon as a hex uses one of the extra entries.
    /// </summary>
    [TestClass]
    public class MapHexEditorListLimitTests
    {
        private readonly List<string> _warnings = new List<string>();

        [TestInitialize]
        public void Setup()
        {
            _warnings.Clear();
            LoggerViewModel.ConsoleSink = (message, level) =>
            {
                if (level == LogLevel.Warning)
                    _warnings.Add(message);
            };
        }

        [TestCleanup]
        public void Cleanup()
        {
            LoggerViewModel.ConsoleSink = null;
        }

        [TestMethod]
        public void CreateNewAttrition_WarnsOnlyOnceTheListPassesWhatMapHexCanStore()
        {
            var map    = MapHexHarness.BuildGrid(2, 2);
            var editor = new MapHexEditor(map, null);
            var limit  = (1 << MapHexFile.ATTRITION_INDEX_BITS) - 1;

            while (map.Attritions.Count < limit)
                editor.CreateNewAttrition($"attrition_{map.Attritions.Count:D2}");

            Assert.AreEqual(0, _warnings.Count, "A list that still fits must not warn.");

            editor.CreateNewAttrition("zz_one_too_many");

            Assert.AreEqual(1, _warnings.Count, "Passing the limit must warn.");
            StringAssert.Contains(_warnings.Single(), $"can only use the first {limit}");
        }

        [TestMethod]
        [DataRow(0x14, 31, DisplayName = "Warhammer 3: 31 areas of interest")]
        [DataRow(0x13, 63, DisplayName = "Three Kingdoms: 63 areas of interest")]
        public void CreateNewAreaOfInterest_WarnsPastTheGamesLimit(int minorVersion, int limit)
        {
            var map    = MapHexHarness.BuildGrid(2, 2);
            MapHexHarness.SetProperty(map, nameof(MapHexFile.MinorFileVersion), minorVersion);
            var editor = new MapHexEditor(map, null);

            while (map.AreasOfInterest.Count < limit)
                editor.CreateNewAreaOfInterest($"aoi_{map.AreasOfInterest.Count:D3}");

            Assert.AreEqual(0, _warnings.Count, "A list that still fits must not warn.");

            editor.CreateNewAreaOfInterest("zz_one_too_many");

            Assert.AreEqual(1, _warnings.Count, "Passing the limit must warn.");
            Assert.AreEqual(limit + 1, map.AreasOfInterest.Count, "The entry past the limit must still be added.");
            StringAssert.Contains(_warnings.Single(), $"can only use the first {limit}");
        }
    }
}
