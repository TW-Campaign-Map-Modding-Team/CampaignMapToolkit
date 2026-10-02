using CAIME.Rpfm;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Unit
{
    /// <summary>
    /// Unit tests for <see cref="ExtractedTableFragment"/>, which reads back the paths rpfm_server
    /// reports having written. Which table a fragment belongs to and what it is called come only
    /// from that path, and the fragment name decides which row wins a merge, so misreading either
    /// silently exports a map built against the wrong tables. The sample paths are shaped exactly as
    /// RPFM 5.0.6 returns them: the destination as given, a backslash, then the in-pack path with
    /// forward slashes and ".tsv" appended.
    /// </summary>
    [TestClass]
    public class ExtractedTableFragmentTests
    {
        private const string Operation = "extract tables";

        [TestMethod]
        public void FromExtractedPath_MixedSeparators_ReadsTableFragmentAndPath()
        {
            var fragment = ExtractedTableFragment.FromExtractedPath(
                @"C:/ak/caime_rpfm/1/extract/mods\db/regions_tables/cr_oldworld_campaign.tsv", Operation);

            Assert.AreEqual("regions", fragment.Table);
            Assert.AreEqual("cr_oldworld_campaign", fragment.FragmentName);
            Assert.AreEqual(@"C:\ak\caime_rpfm\1\extract\mods\db\regions_tables\cr_oldworld_campaign.tsv", fragment.TsvPath);
        }

        [TestMethod]
        public void FromExtractedPath_TableNameEndingInTables_StripsOnlyTheFolderSuffix()
        {
            var fragment = ExtractedTableFragment.FromExtractedPath(@"C:/out\db/campaign_map_playable_areas_tables/data__.tsv", Operation);

            Assert.AreEqual("campaign_map_playable_areas", fragment.Table);
            Assert.AreEqual("data__", fragment.FragmentName);
        }

        [TestMethod]
        public void FromExtractedPath_FragmentNameWithDots_KeepsEverythingBeforeTsv()
        {
            var fragment = ExtractedTableFragment.FromExtractedPath(@"C:/out\db/regions_tables/!!!my.mod.v2.tsv", Operation);

            Assert.AreEqual("!!!my.mod.v2", fragment.FragmentName,
                "Only the .tsv RPFM appends may be stripped; dots inside the fragment name are part of it.");
        }

        [TestMethod]
        public void FromExtractedPath_NotExportedAsTsv_Throws()
        {
            Assert.ThrowsException<RpfmException>(() =>
                ExtractedTableFragment.FromExtractedPath(@"C:/out\db/regions_tables/data__", Operation));
        }

        [TestMethod]
        public void FromExtractedPath_OutsideADbTablesFolder_Throws()
        {
            Assert.ThrowsException<RpfmException>(() =>
                ExtractedTableFragment.FromExtractedPath(@"C:/out\text/regions_tables/data__.tsv", Operation));

            Assert.ThrowsException<RpfmException>(() =>
                ExtractedTableFragment.FromExtractedPath(@"C:/out\db/regions/data__.tsv", Operation));
        }
    }
}
