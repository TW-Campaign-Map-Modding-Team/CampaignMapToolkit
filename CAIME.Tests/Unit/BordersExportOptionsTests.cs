using System.IO;
using System.Reflection;
using CAIME;
using CAIME.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Unit
{
    [TestClass]
    public class BordersExportOptionsTests
    {
        private string _dir;

        [TestInitialize]
        public void Setup()
        {
            _dir = MapHexHarness.CreateTempDir("caime_borders_options");
            ForgetExportOptions();
        }

        [TestCleanup]
        public void Cleanup()
        {
            ForgetExportOptions();
            MapHexHarness.DeleteTempDir(_dir);
        }

        [TestMethod]
        public void SeedGameDefaultsIfUnset_WithNoOptionsWindowOpened_SeedsTheGamesDefaults()
        {
            BorderExportOptionsViewModel.SeedGameDefaultsIfUnset(ProjectFor(GameTemplate.Three_Kingdoms, MapHexHarness.BuildGrid(2, 2)));

            Assert.IsNotNull(BorderExportOptionsViewModel.ExportOptions);
            Assert.AreEqual(16, BorderExportOptionsViewModel.ExportOptions.Count);
            Assert.IsTrue(BorderExportOptionsViewModel.Land_Land.Export, "Land-land borders are exported in every game.");
            Assert.IsTrue(BorderExportOptionsViewModel.Land_Sea.Export, "Three Kingdoms exports land-sea borders by default.");
        }

        [TestMethod]
        public void SeedGameDefaultsIfUnset_KeepsTheChoicesAlreadyMade()
        {
            var project = ProjectFor(GameTemplate.Rome2, MapHexHarness.BuildGrid(2, 2));
            BorderExportOptionsViewModel.SeedGameDefaultsIfUnset(project);
            BorderExportOptionsViewModel.Land_Sea.Export = true;

            BorderExportOptionsViewModel.SeedGameDefaultsIfUnset(project);

            Assert.IsTrue(BorderExportOptionsViewModel.Land_Sea.Export, "A choice made in the options window must survive.");
        }

        [TestMethod]
        public void Export_WithoutTheOptionsWindow_WritesTheBordersFile()
        {
            var map = MapHexHarness.BuildGrid(4, 4, hex =>
            {
                hex.RegionId     = hex.Q < 2 ? 0 : 1;
                hex.IsTownSprawl = true;
            });

            var exported = BordersExporter.Export(ProjectFor(GameTemplate.Rome2, map), _dir + @"\");

            Assert.IsTrue(exported);
            Assert.IsTrue(File.Exists(Path.Combine(_dir, @"display\borders\borders.pbd")));
        }

        private static Project ProjectFor(GameTemplate game, MapHexFile map)
        {
            var project = new Project();
            MapHexHarness.SetProperty(project, nameof(Project.MapHexFile), map);
            MapHexHarness.SetProperty(project, nameof(Project.Game), game);
            return project;
        }

        private static void ForgetExportOptions()
        {
            typeof(BorderExportOptionsViewModel)
                .GetProperty(nameof(BorderExportOptionsViewModel.ExportOptions), BindingFlags.Public | BindingFlags.Static)
                .GetSetMethod(nonPublic: true)
                .Invoke(null, new object[] { null });
        }
    }
}
