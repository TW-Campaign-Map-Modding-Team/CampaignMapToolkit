using System;
using System.IO;
using CAIME.Rpfm;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Unit
{
    /// <summary>
    /// Unit tests for <see cref="MetadataService"/>'s mod pack name. A project stores its mod by
    /// .pack file name only - RPFM locates it and the mods it depends on - so whatever form the name
    /// arrives in (bare name, file name, full path from the Browse dialog or an older metadata file)
    /// it must end up as the one file name RPFM looks the mod up by.
    /// </summary>
    [TestClass]
    public class MetadataServiceTests
    {
        private string _projectDir;

        [TestInitialize]
        public void Setup()
        {
            _projectDir = Path.Combine(Path.GetTempPath(), "caime_metadata_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_projectDir);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_projectDir))
            {
                Directory.Delete(_projectDir, recursive: true);
            }
        }

        [DataTestMethod]
        [DataRow(@"D:\workshop\content\1142710\3081800026\!My_Campaign.pack", "!My_Campaign.pack", DisplayName = "Path keeps only its file name, case untouched")]
        [DataRow("  my_campaign  ", "my_campaign.pack", DisplayName = "Bare name gains .pack")]
        [DataRow("my_campaign.PACK", "my_campaign.PACK", DisplayName = "Existing extension in any case is kept")]
        public void SetThenGetModPackName_Normalizes(string given, string expected)
        {
            MetadataService.SetModPackName(_projectDir, given);

            Assert.AreEqual(expected, MetadataService.GetModPackName(_projectDir));
        }

        [TestMethod]
        public void SetModPackName_Blank_ClearsIt()
        {
            MetadataService.SetModPackName(_projectDir, "my_campaign.pack");
            MetadataService.SetModPackName(_projectDir, "   ");

            Assert.IsNull(MetadataService.GetModPackName(_projectDir));
        }

        [TestMethod]
        public void GetModPackName_NoMetadataFile_ReturnsNull()
        {
            Assert.IsNull(MetadataService.GetModPackName(_projectDir));
        }

        [TestMethod]
        public void GetModPackName_Version3Metadata_KeepsTheFirstPacksFileName()
        {
            File.WriteAllText(MetadataService.GetMetadataPath(_projectDir),
                @"{
                    ""version"": 3,
                    ""map_data_config_path"": ""config.json"",
                    ""pack_file_paths"": [
                        ""C:\\Steam\\steamapps\\common\\Total War WARHAMMER III\\data\\!my_campaign.pack"",
                        ""D:\\workshop\\content\\1142710\\123\\other_mod.pack""
                    ]
                }");

            Assert.AreEqual("!my_campaign.pack", MetadataService.GetModPackName(_projectDir),
                "The v3 list was stored sorted by file name, so its first entry is the one kept.");
            Assert.AreEqual("config.json", MetadataService.Load(_projectDir).MapDataConfigPath,
                "Migrating the pack list must leave the other metadata fields alone.");
        }

        [TestMethod]
        public void GetModPackName_Version3MetadataWithNoPacks_ReturnsNull()
        {
            File.WriteAllText(MetadataService.GetMetadataPath(_projectDir), @"{ ""version"": 3, ""pack_file_paths"": [] }");

            Assert.IsNull(MetadataService.GetModPackName(_projectDir));
        }
    }
}
