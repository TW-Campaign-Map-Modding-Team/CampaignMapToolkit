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
        public void MergeTsvToXml_SameFragmentNameFromTwoSources_EarlierListedFragmentWins()
        {
            // Two fragments that happen to share the exact same in-pack fragment name (e.g. two packs
            // both shipping a "!!!mod" fragment for this table) - fragment-name sorting alone cannot
            // break this tie, so MergeTsvToXml must fall back to a stable sort and let whichever
            // fragment the caller listed first win. RpfmWorkflowSession relies on this: it lists
            // fragments in pack-file-name order specifically so this tie resolves alphabetically by
            // pack name.
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

            var outputXmlPath = Path.Combine(_dir, "first_pack_wins.xml");
            DatabaseTableConverter.MergeTsvToXml(
                new[] { (tsvFromFirstPack, "!!!mod"), (tsvFromSecondPack, "!!!mod") },
                "test_table", booleans, primaryKeyColumns, schemaFields, null, null, outputXmlPath);

            var records = XDocument.Load(outputXmlPath).Root.Elements("test_table").ToList();
            Assert.AreEqual(1, records.Count, "Same primary key across both fragments must collapse to one record.");
            Assert.AreEqual("111", records[0].Element("movement_cost").Value,
                "With tied fragment names, the fragment listed first must win.");

            // Reversing the listed order must reverse the winner - proves the tiebreak really tracks
            // input order rather than, say, file path or an unstable sort.
            var reversedOutputXmlPath = Path.Combine(_dir, "second_pack_wins.xml");
            DatabaseTableConverter.MergeTsvToXml(
                new[] { (tsvFromSecondPack, "!!!mod"), (tsvFromFirstPack, "!!!mod") },
                "test_table", booleans, primaryKeyColumns, schemaFields, null, null, reversedOutputXmlPath);

            var reversedRecords = XDocument.Load(reversedOutputXmlPath).Root.Elements("test_table").ToList();
            Assert.AreEqual("222", reversedRecords[0].Element("movement_cost").Value);
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
