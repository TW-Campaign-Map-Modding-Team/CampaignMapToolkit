using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace CAIME.Rpfm
{
    /// <summary>
    /// Converts RPFM's TSV export format into CAIME's in-memory database tables, laid out as the
    /// Assembly Kit's data XML. Reusable and self-contained: it takes explicit inputs (record element name, which columns are yes/no) rather
    /// than reaching into any workflow state.
    ///
    /// RPFM TSV layout:
    ///   line 1 : tab-separated column names
    ///   line 2 : a "#&lt;table&gt;_tables;&lt;version&gt;;&lt;path&gt;" metadata comment
    ///   line 3+: tab-separated data rows, booleans written as "true"/"false"
    ///
    /// Assembly Kit data XML layout:
    ///   &lt;dataroot&gt;&lt;record&gt;&lt;field&gt;value&lt;/field&gt;...&lt;/record&gt;...&lt;/dataroot&gt;
    /// with yes/no fields written as "1"/"0" (the form the existing loader expects).
    /// </summary>
    public static class DatabaseTableConverter
    {
        // Joins the parts of a composite primary key into a single lookup key. A character that cannot
        // occur inside a field value, rather than "", so that neighbouring parts can never run together
        // into the same string - ("ab","c") and ("a","bc") are different records and must not collide.
        // Spelled as an escape, not the literal control character, so it survives being round-tripped
        // through editors and diffs. Every lookup keyed this way must use it, or they silently stop
        // matching each other.
        private const string KeySeparator = "\u0001";

        /// <summary>
        /// A table's schema from the columns RPFM's schema defines for it, followed by
        /// <paramref name="extraFields"/>. A colour column becomes the r, g and b integer fields the
        /// merged records hold (see <see cref="MergeTsv"/>).
        /// </summary>
        public static XmlSchema SchemaFromRpfm(IReadOnlyList<RpfmColumn> columns, IEnumerable<XmlSchemaField> extraFields)
        {
            var fields = new List<XmlSchemaField>();

            void Add(XmlSchemaField field)
            {
                if (fields.All(existing => !string.Equals(existing.Name, field.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    fields.Add(field);
                }
            }

            foreach (var column in columns)
            {
                var type = column.FieldType?.Type == Newtonsoft.Json.Linq.JTokenType.String ? (string)column.FieldType : null;

                if (type == "ColourRGB")
                {
                    foreach (var channel in new[] { "r", "g", "b" })
                    {
                        Add(XmlSchema.CreateField(channel, XmlSchemaFieldType.Integer, false, null));
                    }

                    continue;
                }

                var fieldType   = ToFieldType(type);
                var value       = fieldType == XmlSchemaFieldType.YesNo && column.DefaultValue != null
                                ? (string.Equals(column.DefaultValue, "true", StringComparison.OrdinalIgnoreCase) ? "1" : "0")
                                : column.DefaultValue;

                Add(XmlSchema.CreateField(column.Name, fieldType, column.IsKey, value));
            }

            foreach (var field in extraFields ?? Enumerable.Empty<XmlSchemaField>())
            {
                Add(field);
            }

            return new XmlSchema(fields);
        }

        /// <summary>The yes/no columns of <paramref name="schema"/>, whose TSV true/false become 1/0.</summary>
        public static ISet<string> GetBooleanColumns(XmlSchema schema)
        {
            return new HashSet<string>(schema.Fields.Where(field => field.Type == XmlSchemaFieldType.YesNo).Select(field => field.Name),
                StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The primary key columns of <paramref name="schema"/>, which tell when rows of different
        /// fragments are the same record. A table with no primary key returns an empty list.
        /// </summary>
        public static IReadOnlyList<string> GetPrimaryKeyColumns(XmlSchema schema)
        {
            return schema.Fields.Where(field => field.PrimaryKey == 1).Select(field => field.Name).ToList();
        }

        // RPFM's integer types, by name; anything else that is no float or boolean, strings and
        // sequences among them, is text. Floats are doubles, the type CAIME reads them as (roads'
        // "threshold").
        private static XmlSchemaFieldType ToFieldType(string rpfmType)
        {
            switch (rpfmType)
            {
                case "Boolean":                                 return XmlSchemaFieldType.YesNo;
                case "F32": case "F64":                         return XmlSchemaFieldType.Double;
                case "I16": case "I32": case "I64":
                case "OptionalI16": case "OptionalI32": case "OptionalI64":
                                                                return XmlSchemaFieldType.Integer;
                default:                                        return XmlSchemaFieldType.Text;
            }
        }

        /// <summary>
        /// Reads an RPFM TSV file's data rows, each as its values by column name, skipping the
        /// metadata line. For a caller that needs a table's raw values rather than its XML.
        /// </summary>
        public static IEnumerable<IReadOnlyDictionary<string, string>> ReadTsvRows(string tsvPath)
        {
            string[] header = null;

            foreach (var line in File.ReadLines(tsvPath))
            {
                if (line.Length == 0 || line[0] == '#')
                {
                    continue;
                }

                var cells = line.Split('\t');

                if (header == null)
                {
                    header = cells;
                    continue;
                }

                var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < header.Length; ++i)
                {
                    row[header[i]] = i < cells.Length ? cells[i] : string.Empty;
                }

                yield return row;
            }
        }

        /// <summary>
        /// Merges the records of several TSV fragments of the same table into a single Assembly Kit
        /// data XML document. A table can be split across multiple fragment files both within one pack
        /// and across several packs (e.g. the game's own packs and one or more mods), exactly the way
        /// the game itself combines them.
        ///
        /// Fragments are processed in ascending fragment-name order, and when two fragments contain a
        /// row for the same primary key, the one from the earlier-sorting fragment name wins - the
        /// same rule the game uses to resolve conflicts between table fragments. This is why a plain
        /// "data"/"data__" fragment (the vanilla default) loses to almost any distinctly-named mod
        /// fragment sorting ahead of it. Rows with no primary-key counterpart in another fragment are
        /// all kept. If the table has no primary key (<paramref name="primaryKeyColumns"/> is empty),
        /// fragments are simply concatenated, since there is then no way to tell whether two rows refer
        /// to the same record.
        ///
        /// The sort here is by fragment name only, never by which pack a fragment came from - two
        /// fragments named identically but from different packs sort as ties, which a stable sort
        /// resolves using <paramref name="fragments"/>'s incoming order. Callers are expected to place
        /// the fragments that should win that case first (RpfmWorkflowSession puts mods before the
        /// game), since fragment name is otherwise silent on which pack wins.
        ///
        /// A fragment's TSV only has the columns its table version was saved with, which can be older
        /// than <paramref name="schemaFields"/>. A schema field missing from a row gets the field's
        /// default rather than being left out, since CAIME's table loader expects every field, in the
        /// schema's order.
        ///
        /// "is_sea", which no pack's table has, is sourced from <paramref name="regionIsSeaByKey"/>
        /// when the row's key is in it: the open map is ground truth for which regions are sea. A row
        /// whose key it does not recognise gets the field's default like any other.
        ///
        /// When <paramref name="rowFilter"/> is given, only the rows it keeps are written; every other
        /// row of every fragment is dropped before merging.
        /// </summary>
        public static XDocument MergeTsv(
            IReadOnlyList<(string TsvPath, string FragmentName)> fragments,
            string recordElementName,
            ISet<string> booleanColumns,
            IReadOnlyList<string> primaryKeyColumns,
            IReadOnlyList<XmlSchemaField> schemaFields,
            IReadOnlyDictionary<string, bool> regionIsSeaByKey,
            TsvRowFilter rowFilter = null)
        {
            var ordered = fragments.OrderBy(f => f.FragmentName, StringComparer.OrdinalIgnoreCase).ToList();

            var recordsByKey = new Dictionary<string, XElement>();
            var keyOrder = new List<string>();

            for (int fragmentIndex = 0; fragmentIndex < ordered.Count; ++fragmentIndex)
            {
                var keyedRecords = TsvToKeyedRecords(
                    ordered[fragmentIndex].TsvPath, recordElementName, booleanColumns, primaryKeyColumns, schemaFields, regionIsSeaByKey, rowFilter, fragmentIndex);

                foreach (var (key, record) in keyedRecords)
                {
                    // The earliest-sorting fragment to define a key wins; a later fragment's row for
                    // the same key is discarded rather than overwriting it.
                    if (!recordsByKey.ContainsKey(key))
                    {
                        keyOrder.Add(key);
                        recordsByKey[key] = record;
                    }
                }
            }

            var records = keyOrder.Select(k => recordsByKey[k]).ToList();

            var root = new XElement("dataroot", records);
            return new XDocument(new XDeclaration("1.0", "UTF-8", null), root);
        }

        private static List<(string Key, XElement Record)> TsvToKeyedRecords(
            string tsvPath, string recordElementName, ISet<string> booleanColumns,
            IReadOnlyList<string> primaryKeyColumns, IReadOnlyList<XmlSchemaField> schemaFields,
            IReadOnlyDictionary<string, bool> regionIsSeaByKey, TsvRowFilter rowFilter, int fragmentIndex)
        {
            var lines = File.ReadAllLines(tsvPath);

            string[] header = null;
            int filterColumnIndex = -1;
            var results = new List<(string, XElement)>();
            int rowIndex = 0;

            foreach (var line in lines)
            {
                if (line.Length == 0)
                {
                    continue;
                }

                // The "#<table>;<version>;..." metadata line is not data.
                if (line[0] == '#')
                {
                    continue;
                }

                var cells = line.Split('\t');

                if (header == null)
                {
                    header = cells;

                    if (rowFilter != null)
                    {
                        filterColumnIndex = Array.IndexOf(header, rowFilter.Column);
                        if (filterColumnIndex < 0)
                        {
                            throw new InvalidDataException($"'{tsvPath}' has no {rowFilter.Column} column to select rows by.");
                        }
                    }

                    continue;
                }

                if (rowFilter != null && !rowFilter.Keeps(filterColumnIndex < cells.Length ? cells[filterColumnIndex] : string.Empty))
                {
                    continue;
                }

                string key;
                if (primaryKeyColumns != null && primaryKeyColumns.Count > 0)
                {
                    var keyValues = new List<string>(primaryKeyColumns.Count);
                    foreach (var pkColumn in primaryKeyColumns)
                    {
                        var idx = Array.IndexOf(header, pkColumn);
                        keyValues.Add(idx >= 0 && idx < cells.Length ? cells[idx] : string.Empty);
                    }
                    key = string.Join(KeySeparator, keyValues);
                }
                else
                {
                    // No primary key to de-duplicate by - every row is treated as unique so fragments
                    // are effectively just concatenated, in the same order as before.
                    key = $"__row_{fragmentIndex}_{rowIndex}";
                }

                var record = new XElement(recordElementName);

                if (schemaFields != null && schemaFields.Count > 0)
                {
                    // Collect this row's raw TSV values by column name first, expanding the packed
                    // colour column into synthetic "r"/"g"/"b" entries so it can be pulled out like any
                    // other schema field below.
                    var rawValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < header.Length; ++i)
                    {
                        var columnName = header[i];
                        if (string.IsNullOrEmpty(columnName))
                        {
                            continue;
                        }

                        var value = i < cells.Length ? cells[i] : string.Empty;

                        if (IsColourColumn(columnName) && TryExpandColour(value, out int r, out int g, out int b))
                        {
                            // Invariant, not the current culture: these are data values, not text shown
                            // to the user.
                            rawValues["r"] = r.ToString(CultureInfo.InvariantCulture);
                            rawValues["g"] = g.ToString(CultureInfo.InvariantCulture);
                            rawValues["b"] = b.ToString(CultureInfo.InvariantCulture);
                            continue;
                        }

                        rawValues[columnName] = value;
                    }

                    // Emit fields in the schema's own declared order, so a fragment's TSV column order
                    // - which can differ from the schema, especially for an older table version - does
                    // not leak into the output. A field missing from this row gets its default.
                    foreach (var field in schemaFields)
                    {
                        if (rawValues.TryGetValue(field.Name, out var value))
                        {
                            var isBoolean = booleanColumns != null && booleanColumns.Contains(field.Name);
                            record.Add(new XElement(field.Name, isBoolean ? ToXmlBoolean(value) : value));
                        }
                        else if (string.Equals(field.Name, "is_sea", StringComparison.OrdinalIgnoreCase)
                            && regionIsSeaByKey != null && regionIsSeaByKey.TryGetValue(key, out var isSea))
                        {
                            record.Add(new XElement(field.Name, isSea ? "1" : "0"));
                        }
                        else
                        {
                            record.Add(new XElement(field.Name, FormatDefaultValue(field)));
                        }
                    }
                }
                else
                {
                    // No schema available (a table with no schema fields given) - fall back to TSV column
                    // order.
                    for (int i = 0; i < header.Length; ++i)
                    {
                        var columnName = header[i];
                        if (string.IsNullOrEmpty(columnName))
                        {
                            continue;
                        }

                        var value = i < cells.Length ? cells[i] : string.Empty;

                        if (booleanColumns != null && booleanColumns.Contains(columnName))
                        {
                            record.Add(new XElement(columnName, ToXmlBoolean(value)));
                            continue;
                        }

                        // RPFM packs an RGB colour into a single hex column (e.g. "B66216") under a
                        // name that is no XML name ("unnamed colour group_1"). CAIME reads separate
                        // decimal r/g/b integer fields, so expand it.
                        if (IsColourColumn(columnName) && TryExpandColour(value, out int r, out int g, out int b))
                        {
                            record.Add(new XElement("r", r));
                            record.Add(new XElement("g", g));
                            record.Add(new XElement("b", b));
                            continue;
                        }

                        // Skip columns whose names are not valid XML element names and that we cannot
                        // map. CAIME's loader reads fields by name, so pack columns it does not know
                        // about are irrelevant to it.
                        if (!IsValidXmlName(columnName))
                        {
                            continue;
                        }

                        record.Add(new XElement(columnName, value));
                    }
                }

                results.Add((key, record));
                ++rowIndex;
            }

            return results;
        }

        // XML element names must be NCNames: start with a letter/underscore, no spaces. RPFM column
        // names are ASCII identifiers, so this simple check is sufficient to spot the odd ones out
        // (such as "unnamed colour group_1").
        private static readonly Regex ValidXmlName = new Regex(@"^[A-Za-z_][A-Za-z0-9_.\-]*$", RegexOptions.Compiled);

        private static bool IsValidXmlName(string name) => ValidXmlName.IsMatch(name);

        private static bool IsColourColumn(string name)
        {
            return !IsValidXmlName(name)
                || name.IndexOf("colour", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("color", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // Parses an RPFM "RRGGBB" hex colour into its decimal r/g/b components. Returns false for
        // empty or non-hex values (e.g. a region with no colour set), leaving r/g/b at the Assembly
        // Kit schema default of 0.
        private static bool TryExpandColour(string value, out int r, out int g, out int b)
        {
            r = g = b = 0;

            if (value == null || value.Length != 6)
            {
                return false;
            }

            for (int i = 0; i < 6; ++i)
            {
                if (!Uri.IsHexDigit(value[i]))
                {
                    return false;
                }
            }

            r = Convert.ToInt32(value.Substring(0, 2), 16);
            g = Convert.ToInt32(value.Substring(2, 2), 16);
            b = Convert.ToInt32(value.Substring(4, 2), 16);
            return true;
        }

        // RPFM writes "true"/"false"; the data XML stores "1"/"0".
        private static string ToXmlBoolean(string value)
        {
            return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ? "1" : "0";
        }

        // XmlSchemaField.DefaultValue is already typed (bool/int/float/double/string) by XmlSchema's
        // own parsing - just needs formatting to match how MergeTsv writes each type to XML.
        // Formatted invariantly: a float default written on a machine whose culture uses a comma
        // decimal separator would otherwise land in the file as "0,5" and be misread downstream.
        private static string FormatDefaultValue(XmlSchemaField field)
        {
            if (field.Type == XmlSchemaFieldType.YesNo)
            {
                return (field.DefaultValue is bool b && b) ? "1" : "0";
            }

            return field.DefaultValue is IFormattable formattable
                ? formattable.ToString(null, CultureInfo.InvariantCulture)
                : field.DefaultValue?.ToString() ?? string.Empty;
        }
    }

    /// <summary>
    /// Selects the rows of a table to convert: those whose <see cref="Column"/> holds one of the
    /// given values, compared ignoring case.
    /// </summary>
    public sealed class TsvRowFilter
    {
        private readonly HashSet<string> _values;

        public TsvRowFilter(string column, IEnumerable<string> values)
        {
            Column = column;
            _values = new HashSet<string>(values, StringComparer.OrdinalIgnoreCase);
        }

        public string Column { get; }

        public bool Keeps(string value) => _values.Contains(value);
    }
}
