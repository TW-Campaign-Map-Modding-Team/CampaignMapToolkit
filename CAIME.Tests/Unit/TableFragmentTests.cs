using CAIME.Rpfm;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Unit
{
    /// <summary>
    /// Unit tests for <see cref="TableFragment.ParsePackedPath"/>, which reads the paths rpfm_server's
    /// files.list returns. Which table a fragment belongs to and what it is called come only from that
    /// path, and the fragment name decides which row wins a merge, so misreading either silently
    /// exports a map built against the wrong tables. The sample paths are shaped exactly as RPFM 5.3
    /// lists them: <c>db/&lt;table&gt;_tables/&lt;fragment&gt;</c>.
    /// </summary>
    [TestClass]
    public class TableFragmentTests
    {
        private const string Operation = "read tables";

        [TestMethod]
        public void ParsePackedPath_ReadsTableAndFragment()
        {
            var (table, fragmentName) = TableFragment.ParsePackedPath("db/regions_tables/cr_oldworld_campaign", Operation);

            Assert.AreEqual("regions", table);
            Assert.AreEqual("cr_oldworld_campaign", fragmentName);
        }

        [TestMethod]
        public void ParsePackedPath_TableNameEndingInTables_StripsOnlyTheFolderSuffix()
        {
            var (table, fragmentName) = TableFragment.ParsePackedPath("db/campaign_map_playable_areas_tables/data__", Operation);

            Assert.AreEqual("campaign_map_playable_areas", table);
            Assert.AreEqual("data__", fragmentName);
        }

        [TestMethod]
        public void ParsePackedPath_FragmentNameWithDots_KeepsThemAll()
        {
            var (_, fragmentName) = TableFragment.ParsePackedPath("db/regions_tables/!!!my.mod.v2", Operation);

            Assert.AreEqual("!!!my.mod.v2", fragmentName, "Dots inside the fragment name are part of it.");
        }

        [TestMethod]
        public void ParsePackedPath_OutsideADbTablesFolder_Throws()
        {
            Assert.ThrowsException<RpfmException>(() => TableFragment.ParsePackedPath("text/regions_tables/data__", Operation));
            Assert.ThrowsException<RpfmException>(() => TableFragment.ParsePackedPath("db/regions/data__", Operation));
            Assert.ThrowsException<RpfmException>(() => TableFragment.ParsePackedPath("db/_tables/data__", Operation));
            Assert.ThrowsException<RpfmException>(() => TableFragment.ParsePackedPath("db/regions_tables/", Operation));
            Assert.ThrowsException<RpfmException>(() => TableFragment.ParsePackedPath("db/regions_tables/sub/data__", Operation));
        }
    }
}
