using System.IO;
using System.Linq;
using System.Xml.Linq;
using CAIME.Rpfm;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Unit
{
    /// <summary>
    /// Unit tests for <see cref="DatabaseTableConverter"/>. The inline TSV mirrors the exact format
    /// RPFM's TSV export produces (column-names line, then a "#&lt;table&gt;;&lt;version&gt;;..." metadata
    /// line, then true/false booleans) and the inline schema mirrors a TWaD_*.xml Assembly Kit schema.
    /// The key behaviour verified is that yes/no columns become "1"/"0" - the form the existing
    /// database loader expects.
    /// </summary>
    [TestClass]
    public class RpfmDatabaseTableConverterTests
    {
        private const string TwadSchema =
@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""no""?>
<!DOCTYPE TWaD_test_table>
<root>
<edit_uuid>00000000-0000-0000-0000-000000000000</edit_uuid>
<field><primary_key>1</primary_key><name>type</name><field_type>text</field_type><required>1</required></field>
<field><primary_key>0</primary_key><name>movement_cost</name><field_type>integer</field_type><required>1</required><default_value>0</default_value></field>
<field><primary_key>0</primary_key><name>can_ambush</name><field_type>yesno</field_type><required>1</required></field>
<field><primary_key>0</primary_key><name>is_sea</name><field_type>yesno</field_type><required>1</required><default_value>false</default_value></field>
</root>";

        // Column order deliberately differs from the schema order, as RPFM's does.
        private const string Tsv =
"type\tmovement_cost\tcan_ambush\tis_sea\n" +
"#test_table_tables;7;db/test_table_tables/rom_test\t\t\t\n" +
"brownlands\t80\ttrue\tfalse\n" +
"deep_sea\t1800\tfalse\ttrue\n";

        private string _dir;

        [TestInitialize]
        public void Setup()
        {
            _dir = Path.Combine(Path.GetTempPath(), "caime_rpfm_conv_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, recursive: true);
            }
        }

        [TestMethod]
        public void GetBooleanColumns_ReturnsYesNoFieldsOnly()
        {
            var schemaPath = Path.Combine(_dir, "TWaD_test_table.xml");
            File.WriteAllText(schemaPath, TwadSchema);

            var booleans = DatabaseTableConverter.GetBooleanColumns(schemaPath);

            CollectionAssert.AreEquivalent(new[] { "can_ambush", "is_sea" }, booleans.ToArray());
        }

        [TestMethod]
        public void TsvToXml_ProducesAssemblyKitXml_WithBooleansAsOneAndZero()
        {
            var schemaPath = Path.Combine(_dir, "TWaD_test_table.xml");
            var tsvPath    = Path.Combine(_dir, "rom_test.tsv");
            var xmlPath    = Path.Combine(_dir, "test_table.xml");

            File.WriteAllText(schemaPath, TwadSchema);
            File.WriteAllText(tsvPath, Tsv);

            var booleans = DatabaseTableConverter.GetBooleanColumns(schemaPath);
            DatabaseTableConverter.TsvToXml(tsvPath, "test_table", booleans, xmlPath);

            var doc     = XDocument.Load(xmlPath);
            var records = doc.Root.Elements("test_table").ToList();

            Assert.AreEqual("dataroot", doc.Root.Name.LocalName, "Root element must be dataroot.");
            Assert.AreEqual(2, records.Count, "Both data rows should be converted; the # metadata line skipped.");

            var first = records[0];
            Assert.AreEqual("brownlands", first.Element("type").Value);
            Assert.AreEqual("80", first.Element("movement_cost").Value);
            Assert.AreEqual("1", first.Element("can_ambush").Value, "true must convert to 1.");
            Assert.AreEqual("0", first.Element("is_sea").Value, "false must convert to 0.");

            var second = records[1];
            Assert.AreEqual("deep_sea", second.Element("type").Value);
            Assert.AreEqual("0", second.Element("can_ambush").Value);
            Assert.AreEqual("1", second.Element("is_sea").Value);
        }

        [TestMethod]
        public void TsvToXml_ExpandsRpfmColourColumnIntoRgb_AndSkipsUnmappableNames()
        {
            // Mirrors the real RPFM "regions" table: a colour packed into a single hex column whose
            // header is not a valid XML name. Row 2 has an empty colour (must not emit r/g/b).
            var tsv =
"key\tis_sea\tunnamed colour group_1\n" +
"#regions_tables;3;db/regions_tables/rom_regions\t\t\n" +
"reg_a\tfalse\tB66216\n" +
"reg_b\ttrue\t\n";

            var schemaPath = Path.Combine(_dir, "TWaD_regions.xml");
            var tsvPath    = Path.Combine(_dir, "rom_regions.tsv");
            var xmlPath    = Path.Combine(_dir, "regions.xml");

            // Minimal regions schema: key (text pk), is_sea (yesno), r/g/b (integer).
            File.WriteAllText(schemaPath,
@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""no""?>
<!DOCTYPE TWaD_regions>
<root>
<edit_uuid>00000000-0000-0000-0000-000000000000</edit_uuid>
<field><primary_key>1</primary_key><name>key</name><field_type>text</field_type><required>1</required></field>
<field><primary_key>0</primary_key><name>is_sea</name><field_type>yesno</field_type><required>1</required><default_value>false</default_value></field>
<field><primary_key>0</primary_key><name>r</name><field_type>integer</field_type><required>1</required><default_value>0</default_value></field>
<field><primary_key>0</primary_key><name>g</name><field_type>integer</field_type><required>1</required><default_value>0</default_value></field>
<field><primary_key>0</primary_key><name>b</name><field_type>integer</field_type><required>1</required><default_value>0</default_value></field>
</root>");
            File.WriteAllText(tsvPath, tsv);

            var booleans = DatabaseTableConverter.GetBooleanColumns(schemaPath);
            DatabaseTableConverter.TsvToXml(tsvPath, "regions", booleans, xmlPath);

            var records = XDocument.Load(xmlPath).Root.Elements("regions").ToList();
            Assert.AreEqual(2, records.Count);

            // B66216 -> r=182 (0xB6), g=98 (0x62), b=22 (0x16).
            Assert.AreEqual("182", records[0].Element("r").Value);
            Assert.AreEqual("98", records[0].Element("g").Value);
            Assert.AreEqual("22", records[0].Element("b").Value);
            Assert.IsFalse(records[0].Elements().Any(e => e.Name.LocalName.Contains(" ")),
                "No element with an invalid (space-containing) name should be emitted.");

            // Empty colour: no r/g/b emitted; the loader falls back to the schema default (0).
            Assert.IsNull(records[1].Element("r"));
        }

        [TestMethod]
        public void MergeTsv_SameFragmentNameFromTwoSources_EarlierListedFragmentWins()
        {
            // Two fragments that happen to share the exact same in-pack fragment name (e.g. two packs
            // both shipping a "!!!mod" fragment for this table) - fragment-name sorting alone cannot
            // break this tie, so MergeTsv must fall back to a stable sort and let whichever
            // fragment the caller listed first win. RpfmWorkflowSession relies on this: it lists a
            // mod's fragments before the game's, so a mod wins this tie.
            var schemaPath = Path.Combine(_dir, "TWaD_test_table.xml");
            File.WriteAllText(schemaPath, TwadSchema);

            var tsvFromFirstPack = Path.Combine(_dir, "pack1.tsv");
            File.WriteAllText(tsvFromFirstPack,
                "type\tmovement_cost\tcan_ambush\tis_sea\n" +
                "#test_table_tables;7;db/test_table_tables/!!!mod\t\t\t\n" +
                "brownlands\t111\ttrue\tfalse\n");

            var tsvFromSecondPack = Path.Combine(_dir, "pack2.tsv");
            File.WriteAllText(tsvFromSecondPack,
                "type\tmovement_cost\tcan_ambush\tis_sea\n" +
                "#test_table_tables;7;db/test_table_tables/!!!mod\t\t\t\n" +
                "brownlands\t222\ttrue\tfalse\n");

            var booleans          = DatabaseTableConverter.GetBooleanColumns(schemaPath);
            var primaryKeyColumns = DatabaseTableConverter.GetPrimaryKeyColumns(schemaPath);
            var schemaFields      = DatabaseTableConverter.GetFields(schemaPath);

            var merged = DatabaseTableConverter.MergeTsv(
                new[] { (tsvFromFirstPack, "!!!mod"), (tsvFromSecondPack, "!!!mod") },
                "test_table", booleans, primaryKeyColumns, schemaFields, null, null);

            var records = merged.Root.Elements("test_table").ToList();
            Assert.AreEqual(1, records.Count, "Same primary key across both fragments must collapse to one record.");
            Assert.AreEqual("111", records[0].Element("movement_cost").Value,
                "With tied fragment names, the fragment listed first must win.");

            // Reversing the listed order must reverse the winner - proves the tiebreak really tracks
            // input order rather than, say, file path or an unstable sort.
            var reversed = DatabaseTableConverter.MergeTsv(
                new[] { (tsvFromSecondPack, "!!!mod"), (tsvFromFirstPack, "!!!mod") },
                "test_table", booleans, primaryKeyColumns, schemaFields, null, null);

            var reversedRecords = reversed.Root.Elements("test_table").ToList();
            Assert.AreEqual("222", reversedRecords[0].Element("movement_cost").Value);
        }

        [TestMethod]
        public void MergeTsv_WithRowFilter_KeepsOnlySelectedRowsInTheirOrder()
        {
            // The RPFM workflow keeps only one campaign map's regions; dropping the rest must not
            // reorder the ones kept, since CAIME numbers regions by their order in the table.
            var schemaPath = Path.Combine(_dir, "TWaD_test_table.xml");
            var tsvPath    = Path.Combine(_dir, "rom_test.tsv");
            File.WriteAllText(schemaPath, TwadSchema);
            File.WriteAllText(tsvPath,
                "type\tmovement_cost\tcan_ambush\tis_sea\n" +
                "#test_table_tables;7;db/test_table_tables/rom_test\t\t\t\n" +
                "zz_kept\t1\ttrue\tfalse\n" +
                "dropped\t2\ttrue\tfalse\n" +
                "aa_kept\t3\tfalse\ttrue\n");

            var merged = DatabaseTableConverter.MergeTsv(
                new[] { (tsvPath, "rom_test") }, "test_table",
                DatabaseTableConverter.GetBooleanColumns(schemaPath),
                DatabaseTableConverter.GetPrimaryKeyColumns(schemaPath),
                DatabaseTableConverter.GetFields(schemaPath),
                null, null,
                new TsvRowFilter("type", new[] { "AA_KEPT", "zz_kept" }));

            var kept = merged.Root.Elements("test_table").Select(r => r.Element("type").Value).ToArray();
            CollectionAssert.AreEqual(new[] { "zz_kept", "aa_kept" }, kept,
                "Only the selected rows may be kept (matched ignoring case), in their original order.");
        }

        [TestMethod]
        public void MergeTsv_RowFilterOnAMissingColumn_Throws()
        {
            var schemaPath = Path.Combine(_dir, "TWaD_test_table.xml");
            var tsvPath    = Path.Combine(_dir, "rom_test.tsv");
            File.WriteAllText(schemaPath, TwadSchema);
            File.WriteAllText(tsvPath, Tsv);

            Assert.ThrowsException<InvalidDataException>(() => DatabaseTableConverter.MergeTsv(
                new[] { (tsvPath, "rom_test") }, "test_table",
                DatabaseTableConverter.GetBooleanColumns(schemaPath),
                DatabaseTableConverter.GetPrimaryKeyColumns(schemaPath),
                DatabaseTableConverter.GetFields(schemaPath),
                null, null,
                new TsvRowFilter("no_such_column", new[] { "x" })),
                "A filter that cannot be applied must fail rather than silently keep every row or none.");
        }

        [TestMethod]
        public void WriteAssemblyKitXml_WritesNoBom_AndCreatesTheFolder()
        {
            var xmlPath  = Path.Combine(_dir, "not_yet_created", "regions.xml");
            var document = new XDocument(new XElement("dataroot", new XElement("regions", new XElement("key", "reg_a"))));

            DatabaseTableConverter.WriteAssemblyKitXml(document, xmlPath);

            Assert.AreNotEqual(0xEF, File.ReadAllBytes(xmlPath)[0], "The Assembly Kit's reader crashes on a UTF-8 BOM.");
            Assert.AreEqual("reg_a", XDocument.Load(xmlPath).Root.Element("regions").Element("key").Value);
        }

        [TestMethod]
        public void ReadTsvRows_ReturnsDataRowsByColumnName_SkippingTheMetadataLine()
        {
            var tsvPath = Path.Combine(_dir, "campaign_map_regions.tsv");
            File.WriteAllText(tsvPath,
                "campaign_map\tregion\n" +
                "#campaign_map_regions_tables;0;db/campaign_map_regions_tables/data__\t\n" +
                "main_map\treg_a\n" +
                "other_map\treg_b\n");

            var rows = DatabaseTableConverter.ReadTsvRows(tsvPath).ToList();

            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual("main_map", rows[0]["campaign_map"]);
            Assert.AreEqual("reg_b", rows[1]["region"]);
        }

        [TestMethod]
        public void XmlToTsv_RoundTripsBackToTrueFalse()
        {
            var schemaPath = Path.Combine(_dir, "TWaD_test_table.xml");
            var tsvPath    = Path.Combine(_dir, "rom_test.tsv");
            var xmlPath    = Path.Combine(_dir, "test_table.xml");
            var roundTrip  = Path.Combine(_dir, "roundtrip.tsv");

            File.WriteAllText(schemaPath, TwadSchema);
            File.WriteAllText(tsvPath, Tsv);

            var booleans = DatabaseTableConverter.GetBooleanColumns(schemaPath);
            DatabaseTableConverter.TsvToXml(tsvPath, "test_table", booleans, xmlPath);

            var columns = new[] { "type", "movement_cost", "can_ambush", "is_sea" };
            DatabaseTableConverter.XmlToTsv(xmlPath, "test_table", columns, booleans, "test_table_tables", 7, roundTrip);

            var lines = File.ReadAllLines(roundTrip);
            Assert.AreEqual("type\tmovement_cost\tcan_ambush\tis_sea", lines[0]);
            Assert.IsTrue(lines[1].StartsWith("#test_table_tables;7;"), "Second line should be the metadata comment.");
            Assert.AreEqual("brownlands\t80\ttrue\tfalse", lines[2], "Booleans must round-trip back to true/false.");
            Assert.AreEqual("deep_sea\t1800\tfalse\ttrue", lines[3]);
        }
    }
}
