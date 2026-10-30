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
    /// Unit tests for <see cref="DatabaseTableConverter"/>. The inline fragments hold values as
    /// RPFM's table.rows sends them, made text by <see cref="RpfmService"/> (true/false booleans, a
    /// colour as one RRGGBB column), and the inline schema mirrors the columns RPFM's
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

        private static readonly string[] TestColumns = { "type", "movement_cost", "can_ambush", "is_sea" };

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
        public void MergeFragments_WritesBooleansAsOneAndZero()
        {
            var fragment = Fragment("rom_test", TestColumns,
                new[] { "brownlands", "80", "true", "false" },
                new[] { "deep_sea", "1800", "false", "true" });

            var doc     = Merge(new[] { fragment }, null);
            var records = doc.Root.Elements("test_table").ToList();

            Assert.AreEqual("dataroot", doc.Root.Name.LocalName, "Root element must be dataroot.");
            Assert.AreEqual(2, records.Count);

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
        public void MergeFragments_ExpandsRpfmColourColumnIntoRgb()
        {
            // Mirrors the real RPFM "regions" table: a colour packed into a single hex column whose
            // name is not a valid XML name. Row 2 has an empty colour, so r/g/b keep their default.
            var columns  = new[] { Column("key", "StringU8", isKey: true), Column("is_sea", "Boolean"), Column("unnamed colour group_1", "ColourRGB") };
            var fragment = Fragment("rom_regions", new[] { "key", "is_sea", "unnamed colour group_1" },
                new[] { "reg_a", "false", "B66216" },
                new[] { "reg_b", "true", "" });

            var records = MergeWithRpfmColumns(fragment, "regions", columns).Root.Elements("regions").ToList();
            Assert.AreEqual(2, records.Count);

            // B66216 -> r=182 (0xB6), g=98 (0x62), b=22 (0x16).
            Assert.AreEqual("182", records[0].Element("r").Value);
            Assert.AreEqual("98", records[0].Element("g").Value);
            Assert.AreEqual("22", records[0].Element("b").Value);
            Assert.IsFalse(records[0].Elements().Any(e => e.Name.LocalName.Contains(" ")),
                "No element with an invalid (space-containing) name should be emitted.");

            Assert.AreEqual("0", records[1].Element("r").Value);
        }

        // Only RPFM's schema says which columns are colours: a text column named like one, holding
        // what looks like a hex colour, keeps its value.
        [TestMethod]
        public void MergeFragments_TextColumnNamedLikeAColour_KeepsItsValue()
        {
            var columns  = new[] { Column("key", "StringU8", isKey: true), Column("colour_name", "StringU8") };
            var fragment = Fragment("data__", new[] { "key", "colour_name" }, new[] { "reg_a", "abcdef" });

            var record = MergeWithRpfmColumns(fragment, "regions", columns).Root.Elements("regions").Single();

            Assert.AreEqual("abcdef", record.Element("colour_name").Value);
            Assert.IsNull(record.Element("r"));
        }

        [TestMethod]
        public void MergeFragments_SameFragmentNameFromTwoSources_EarlierListedFragmentWins()
        {
            // Two fragments that happen to share the exact same in-pack fragment name (e.g. two packs
            // both shipping a "!!!mod" fragment for this table) - fragment-name sorting alone cannot
            // break this tie, so MergeFragments must fall back to a stable sort and let whichever
            // fragment the caller listed first win. RpfmWorkflowSession relies on this: it lists a
            // mod's fragments before the game's, so a mod wins this tie.
            var fromFirstPack  = Fragment("!!!mod", TestColumns, new[] { "brownlands", "111", "true", "false" });
            var fromSecondPack = Fragment("!!!mod", TestColumns, new[] { "brownlands", "222", "true", "false" });

            var merged   = Merge(new[] { fromFirstPack, fromSecondPack }, null);
            var records  = merged.Root.Elements("test_table").ToList();
            Assert.AreEqual(1, records.Count, "Same primary key across both fragments must collapse to one record.");
            Assert.AreEqual("111", records[0].Element("movement_cost").Value,
                "With tied fragment names, the fragment listed first must win.");

            // Reversing the listed order must reverse the winner - proves the tiebreak really tracks
            // input order rather than an unstable sort.
            var reversed = Merge(new[] { fromSecondPack, fromFirstPack }, null);
            Assert.AreEqual("222", reversed.Root.Elements("test_table").First().Element("movement_cost").Value);
        }

        [TestMethod]
        public void MergeFragments_WithRowFilter_KeepsOnlySelectedRowsInTheirOrder()
        {
            // The RPFM workflow keeps only one campaign map's regions; dropping the rest must not
            // reorder the ones kept, since CAIME numbers regions by their order in the table.
            var fragment = Fragment("rom_test", TestColumns,
                new[] { "zz_kept", "1", "true", "false" },
                new[] { "dropped", "2", "true", "false" },
                new[] { "aa_kept", "3", "false", "true" });

            var merged = Merge(new[] { fragment }, new RowFilter("type", new[] { "AA_KEPT", "zz_kept" }));

            var kept = merged.Root.Elements("test_table").Select(r => r.Element("type").Value).ToArray();
            CollectionAssert.AreEqual(new[] { "zz_kept", "aa_kept" }, kept,
                "Only the selected rows may be kept (matched ignoring case), in their original order.");
        }

        [TestMethod]
        public void MergeFragments_RowFilterOnAMissingColumn_Throws()
        {
            var fragment = Fragment("rom_test", TestColumns, new[] { "brownlands", "80", "true", "false" });

            Assert.ThrowsException<InvalidDataException>(() => Merge(new[] { fragment }, new RowFilter("no_such_column", new[] { "x" })),
                "A filter that cannot be applied must fail rather than silently keep every row or none.");
        }

        [TestMethod]
        public void ReadRows_ReturnsRowsByColumnName()
        {
            var fragment = new TableFragment("campaign_map_regions", "data__", new[] { "campaign_map", "region" },
                new[] { new[] { "main_map", "reg_a" }, new[] { "other_map", "reg_b" } });

            var rows = DatabaseTableConverter.ReadRows(fragment).ToList();

            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual("main_map", rows[0]["campaign_map"]);
            Assert.AreEqual("reg_b", rows[1]["region"]);
        }

        private static XmlSchema TestSchema()
        {
            return DatabaseTableConverter.SchemaFromRpfm(TestTableColumns, null);
        }

        private static TableFragment Fragment(string fragmentName, string[] columns, params string[][] rows)
        {
            return new TableFragment("test_table", fragmentName, columns, rows);
        }

        private static XDocument Merge(TableFragment[] fragments, RowFilter rowFilter)
        {
            var schema = TestSchema();
            return DatabaseTableConverter.MergeFragments(fragments, "test_table",
                DatabaseTableConverter.GetBooleanColumns(schema), DatabaseTableConverter.GetColourColumns(TestTableColumns),
                DatabaseTableConverter.GetPrimaryKeyColumns(schema), schema.Fields, null, rowFilter);
        }

        // One fragment, under the schema RPFM's columns make, as RpfmWorkflowSession merges a table.
        private static XDocument MergeWithRpfmColumns(TableFragment fragment, string recordElementName, RpfmColumn[] columns)
        {
            var schema = DatabaseTableConverter.SchemaFromRpfm(columns, null);
            return DatabaseTableConverter.MergeFragments(new[] { fragment }, recordElementName,
                DatabaseTableConverter.GetBooleanColumns(schema), DatabaseTableConverter.GetColourColumns(columns),
                DatabaseTableConverter.GetPrimaryKeyColumns(schema), schema.Fields, null);
        }

        private static RpfmColumn Column(string name, string type, bool isKey = false, string defaultValue = null)
        {
            return new RpfmColumn { Name = name, FieldType = new JValue(type), IsKey = isKey, DefaultValue = defaultValue };
        }
    }
}
