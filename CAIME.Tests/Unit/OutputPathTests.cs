using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Unit
{
    /// <summary>
    /// Unit tests for <see cref="PreferencesViewModel.GetCampaignMapOutputPath"/>, where every
    /// processed file is written. <see cref="PreferencesViewModel"/> is a process-wide singleton, so
    /// each test restores what it changed.
    /// </summary>
    [TestClass]
    public class OutputPathTests
    {
        private const GameTemplate GAME = GameTemplate.Warhammer3;

        private string _savedOutputFolder;
        private string _savedAssKitPath;

        [TestInitialize]
        public void Setup()
        {
            var prefs = PreferencesViewModel.Instance;
            _savedOutputFolder  = prefs.OutputFolder;
            _savedAssKitPath    = prefs.GetAssKitPath(GAME);
        }

        [TestCleanup]
        public void Cleanup()
        {
            var prefs = PreferencesViewModel.Instance;
            prefs.OutputFolder = _savedOutputFolder;
            prefs.SetAssKitPath(GAME, _savedAssKitPath);
        }

        [TestMethod]
        public void OutputFolder_IsUsedAsADataRoot()
        {
            var prefs = PreferencesViewModel.Instance;
            prefs.OutputFolder = @"C:\out";
            prefs.SetAssKitPath(GAME, @"C:\ak");

            Assert.AreEqual(Path.Combine(@"C:\out", "campaign_maps", "my_map") + "\\", prefs.GetCampaignMapOutputPath(GAME, "my_map"));
        }

        [TestMethod]
        public void NoOutputFolder_FallsBackToTheAssemblyKitsWorkingData()
        {
            var prefs = PreferencesViewModel.Instance;
            prefs.OutputFolder = "  ";
            prefs.SetAssKitPath(GAME, @"C:\ak");

            Assert.AreEqual(Path.Combine(@"C:\ak", "working_data", "campaign_maps", "my_map") + "\\", prefs.GetCampaignMapOutputPath(GAME, "my_map"));
        }

        [TestMethod]
        public void NeitherSet_IsNull()
        {
            var prefs = PreferencesViewModel.Instance;
            prefs.OutputFolder = null;
            prefs.SetAssKitPath(GAME, null);

            Assert.IsNull(prefs.GetCampaignMapOutputPath(GAME, "my_map"));
        }
    }
}
