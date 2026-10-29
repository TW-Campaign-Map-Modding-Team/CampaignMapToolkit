using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using CAIME.Rpfm;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace CAIME.Tests.Unit
{
    /// <summary>
    /// Unit tests for <see cref="DatabaseTableConverter"/>. The inline TSV mirrors the exact format
    /// RPFM's TSV export produces (column-names line, then a "#&lt;table&gt;;&lt;version&gt;;..." metadata
    /// line, then true/false booleans) and the inline schema mirrors the columns RPFM's
    /// schema.definition returns. The key behaviour verified is that yes/no columns become "1"/"0" -
    /// the form the database loader expects.
    /// </summary>
    [TestClass]
    public class RpfmDatabaseTableConverterTests
    {
        private static readonly RpfmColumn[] TestTableColumns =
        {
            Column("type",          "StringU8", isKey: true),
            Column("movement_cost", "I32",      defaultValue: "0"),
            Column("can_ambush",    "Boolean"),
            Column("is_sea",        "Boolean",  defaultValue: "false"),
        };

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
            var booleans = DatabaseTableConverter.GetBooleanColumns(TestSchema());

            CollectionAssert.AreEquivalent(new[] { "can_ambush", "is_sea" }, booleans.ToArray());
        }

        [TestMethod]
        public void SchemaFromRpfm_MapsTypesKeysAndDefaults()
        {
            var schema = TestSchema();

            CollectionAssert.AreEqual(new[] { "type", "movement_cost", "can_ambush", "is_sea" }, schema.Fields.Select(f => f.Name).ToArray());
            CollectionAssert.AreEqual(new[] { "type" }, DatabaseTableConverter.GetPrimaryKeyColumns(schema).ToArray());
            Assert.AreEqual(XmlSchemaFieldType.Text,    schema.Fields[0].Type);
            Assert.AreEqual(XmlSchemaFieldType.Integer, schema.Fields[1].Type);
            Assert.AreEqual(XmlSchemaFieldType.YesNo,   schema.Fields[2].Type);
            Assert.AreEqual(false, schema.Fields[3].DefaultValue, "RPFM's \"false\" default must read as false.");
        }

        [TestMethod]
        public void SchemaFromRpfm_ColourColumnBecomesRgb_ThenTheExtraFields()
        {
            var schema = DatabaseTableConverter.SchemaFromRpfm(
                new[] { Column("key", "StringU8", isKey: true), Column("unnamed colour group_1", "ColourRGB") },
                new[] { XmlSchema.CreateField("is_sea", XmlSchemaFieldType.YesNo, false, "0") });

            CollectionAssert.AreEqual(new[] { "key", "r", "g", "b", "is_sea" }, schema.Fields.Select(f => f.Name).ToArray());
            Assert.IsTrue(schema.Fields.Skip(1).Take(3).All(f => f.Type == XmlSchemaFieldType.Integer));
        }

        [TestMethod]
        public void MergeTsv_WritesBooleansAsOneAndZero()
        {
            var tsvPath = Path.Combine(_dir, "rom_test.tsv");
            File.WriteAllText(tsvPath, Tsv);

            var doc     = MergeWithoutSchemaFields(tsvPath, "test_table", TestSchema());
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
        public void MergeTsv_ExpandsRpfmColourColumnIntoRgb_AndSkipsUnmappableNames()
        {
            // Mirrors the real RPFM "regions" table: a colour packed into a single hex column whose
            // header is not a valid XML name. Row 2 has an empty colour (must not emit r/g/b).
            var tsv =
"key\tis_sea\tunnamed colour group_1\n" +
"#regions_tables;3;db/regions_tables/rom_regions\t\t\n" +
"reg_a\tfalse\tB66216\n" +
"reg_b\ttrue\t\n";

            var tsvPath = Path.Combine(_dir, "rom_regions.tsv");
            File.WriteAllText(tsvPath, tsv);

            var schema  = DatabaseTableConverter.SchemaFromRpfm(new[] { Column("key", "StringU8", isKey: true), Column("is_sea", "Boolean") }, null);
            var records = MergeWithoutSchemaFields(tsvPath, "regions", schema).Root.Elements("regions").ToList();
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

            var merged   = Merge(new[] { (tsvFromFirstPack, "!!!mod"), (tsvFromSecondPack, "!!!mod") }, null);
            var records  = merged.Root.Elements("test_table").ToList();
            Assert.AreEqual(1, records.Count, "Same primary key across both fragments must collapse to one record.");
            Assert.AreEqual("111", records[0].Element("movement_cost").Value,
                "With tied fragment names, the fragment listed first must win.");

            // Reversing the listed order must reverse the winner - proves the tiebreak really tracks
            // input order rather than, say, file path or an unstable sort.
            var reversed = Merge(new[] { (tsvFromSecondPack, "!!!mod"), (tsvFromFirstPack, "!!!mod") }, null);
            Assert.AreEqual("222", reversed.Root.Elements("test_table").First().Element("movement_cost").Value);
        }

        [TestMethod]
        public void MergeTsv_WithRowFilter_KeepsOnlySelectedRowsInTheirOrder()
        {
            // The RPFM workflow keeps only one campaign map's regions; dropping the rest must not
            // reorder the ones kept, since CAIME numbers regions by their order in the table.
            var tsvPath = Path.Combine(_dir, "rom_test.tsv");
            File.WriteAllText(tsvPath,
                "type\tmovement_cost\tcan_ambush\tis_sea\n" +
                "#test_table_tables;7;db/test_table_tables/rom_test\t\t\t\n" +
                "zz_kept\t1\ttrue\tfalse\n" +
                "dropped\t2\ttrue\tfalse\n" +
                "aa_kept\t3\tfalse\ttrue\n");

            var merged = Merge(new[] { (tsvPath, "rom_test") }, new TsvRowFilter("type", new[] { "AA_KEPT", "zz_kept" }));

            var kept = merged.Root.Elements("test_table").Select(r => r.Element("type").Value).ToArray();
            CollectionAssert.AreEqual(new[] { "zz_kept", "aa_kept" }, kept,
                "Only the selected rows may be kept (matched ignoring case), in their original order.");
        }

        [TestMethod]
        public void MergeTsv_RowFilterOnAMissingColumn_Throws()
        {
            var tsvPath = Path.Combine(_dir, "rom_test.tsv");
            File.WriteAllText(tsvPath, Tsv);

            Assert.ThrowsException<InvalidDataException>(() => Merge(new[] { (tsvPath, "rom_test") }, new TsvRowFilter("no_such_column", new[] { "x" })),
                "A filter that cannot be applied must fail rather than silently keep every row or none.");
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

        private static XmlSchema TestSchema()
        {
            return DatabaseTableConverter.SchemaFromRpfm(TestTableColumns, null);
        }

        private static XDocument Merge((string, string)[] fragments, TsvRowFilter rowFilter)
        {
            var schema = TestSchema();
            return DatabaseTableConverter.MergeTsv(fragments, "test_table",
                DatabaseTableConverter.GetBooleanColumns(schema), DatabaseTableConverter.GetPrimaryKeyColumns(schema), schema.Fields, null, rowFilter);
        }

        // One fragment, in its TSV's own column order, with the schema giving only its yes/no columns.
        private static XDocument MergeWithoutSchemaFields(string tsvPath, string recordElementName, XmlSchema schema)
        {
            return DatabaseTableConverter.MergeTsv(new[] { (tsvPath, string.Empty) }, recordElementName,
                DatabaseTableConverter.GetBooleanColumns(schema), Array.Empty<string>(), Array.Empty<XmlSchemaField>(), null);
        }

        private static RpfmColumn Column(string name, string type, bool isKey = false, string defaultValue = null)
        {
            return new RpfmColumn { Name = name, FieldType = new JValue(type), IsKey = isKey, DefaultValue = defaultValue };
        }
    }
}
