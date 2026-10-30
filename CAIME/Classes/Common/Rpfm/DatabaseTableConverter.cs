using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace CAIME.Rpfm
{
    /// <summary>
    /// Converts the table fragments RPFM reads (<see cref="TableFragment"/>) into CAIME's in-memory
    /// database tables, laid out as the Assembly Kit's data XML. Reusable and self-contained: it takes
    /// explicit inputs (record element name, which columns are yes/no) rather than reaching into any
    /// workflow state.
    ///
    /// Assembly Kit data XML layout:
    ///   &lt;dataroot&gt;&lt;record&gt;&lt;field&gt;value&lt;/field&gt;...&lt;/record&gt;...&lt;/dataroot&gt;
    /// with yes/no fields written as "1"/"0" (the form the existing loader expects).
    /// </summary>
    public static class DatabaseTableConverter
    {
        // Joins the parts of a composite primary key. A character that cannot occur in a field value, so
        // ("ab","c") and ("a","bc") stay different records. Every lookup keyed this way must use it.
        private const string KeySeparator = "\u0001";

        /// <summary>
        /// A table's schema from the columns RPFM's schema defines for it, followed by
        /// <paramref name="extraFields"/>. A colour column becomes the r, g and b integer fields the
        /// merged records hold (see <see cref="MergeFragments"/>).
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
                var type = RpfmType(column);

                if (type == ColourType)
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

        /// <summary>
        /// The columns RPFM's schema gives as colours, each packed into one RRGGBB column, which
        /// <see cref="MergeFragments"/> expands into the r, g and b fields.
        /// </summary>
        public static ISet<string> GetColourColumns(IReadOnlyList<RpfmColumn> columns)
        {
            return new HashSet<string>(columns.Where(column => RpfmType(column) == ColourType).Select(column => column.Name),
                StringComparer.OrdinalIgnoreCase);
        }

        private const string ColourType = "ColourRGB";

        // A column's type name, like "I32"; null for a sequence, whose type is an object.
        private static string RpfmType(RpfmColumn column)
        {
            return column.FieldType?.Type == Newtonsoft.Json.Linq.JTokenType.String ? (string)column.FieldType : null;
        }

        /// <summary>The yes/no columns of <paramref name="schema"/>, whose true/false become 1/0.</summary>
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
        /// A fragment's rows, each as its values by column name. For a caller that needs a table's raw
        /// values rather than its XML.
        /// </summary>
        public static IEnumerable<IReadOnlyDictionary<string, string>> ReadRows(TableFragment fragment)
        {
            foreach (var cells in fragment.Rows)
            {
                var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < fragment.Columns.Count; ++i)
                {
                    row[fragment.Columns[i]] = i < cells.Count ? cells[i] : string.Empty;
                }

                yield return row;
            }
        }

        /// <summary>
        /// Merges every fragment of one table, from any pack, into one XML document, as the game does:
        /// for rows with the same primary key the fragment whose name sorts first wins, and all other
        /// rows are kept. Without a primary key the fragments are concatenated. Fragments with the
        /// same name keep <paramref name="fragments"/>'s order, so callers put the winner first
        /// (RpfmWorkflowSession puts mods before the game).
        ///
        /// Every row gets every field of <paramref name="schemaFields"/>, in order: a field an older
        /// fragment lacks gets its default, and "is_sea", which no pack has, comes from
        /// <paramref name="regionIsSeaByKey"/>. Each of <paramref name="colourColumns"/> (see
        /// <see cref="GetColourColumns"/>) fills the r, g and b fields. <paramref name="rowFilter"/>,
        /// when given, drops rows before merging.
        /// </summary>
        public static XDocument MergeFragments(
            IReadOnlyList<TableFragment> fragments,
            string recordElementName,
            ISet<string> booleanColumns,
            ISet<string> colourColumns,
            IReadOnlyList<string> primaryKeyColumns,
            IReadOnlyList<XmlSchemaField> schemaFields,
            IReadOnlyDictionary<string, bool> regionIsSeaByKey,
            RowFilter rowFilter = null)
        {
            var ordered = fragments.OrderBy(f => f.FragmentName, StringComparer.OrdinalIgnoreCase).ToList();

            var recordsByKey = new Dictionary<string, XElement>();
            var keyOrder = new List<string>();

            for (int fragmentIndex = 0; fragmentIndex < ordered.Count; ++fragmentIndex)
            {
                var keyedRecords = ToKeyedRecords(
                    ordered[fragmentIndex], recordElementName, booleanColumns, colourColumns, primaryKeyColumns, schemaFields, regionIsSeaByKey, rowFilter, fragmentIndex);

                foreach (var (key, record) in keyedRecords)
                {
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

        private static List<(string Key, XElement Record)> ToKeyedRecords(
            TableFragment fragment, string recordElementName, ISet<string> booleanColumns, ISet<string> colourColumns,
            IReadOnlyList<string> primaryKeyColumns, IReadOnlyList<XmlSchemaField> schemaFields,
            IReadOnlyDictionary<string, bool> regionIsSeaByKey, RowFilter rowFilter, int fragmentIndex)
        {
            var header = fragment.Columns.ToArray();
            int filterColumnIndex = -1;
            var results = new List<(string, XElement)>();
            int rowIndex = 0;

            if (rowFilter != null)
            {
                filterColumnIndex = Array.IndexOf(header, rowFilter.Column);
                if (filterColumnIndex < 0)
                {
                    throw new InvalidDataException(
                        $"The {fragment.Table} fragment {fragment.FragmentName} has no {rowFilter.Column} column to select rows by.");
                }
            }

            foreach (var cells in fragment.Rows)
            {
                if (rowFilter != null && !rowFilter.Keeps(filterColumnIndex < cells.Count ? cells[filterColumnIndex] : string.Empty))
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
                        keyValues.Add(idx >= 0 && idx < cells.Count ? cells[idx] : string.Empty);
                    }
                    key = string.Join(KeySeparator, keyValues);
                }
                else
                {
                    // Without a primary key every row is unique, so fragments are concatenated.
                    key = $"__row_{fragmentIndex}_{rowIndex}";
                }

                var record = new XElement(recordElementName);

                // A colour column is expanded into "r"/"g"/"b" entries so it reads like any other field.
                var rawValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < header.Length; ++i)
                {
                    var columnName = header[i];
                    var value      = i < cells.Count ? cells[i] : string.Empty;

                    if (colourColumns != null && colourColumns.Contains(columnName))
                    {
                        if (TryExpandColour(value, out int r, out int g, out int b))
                        {
                            rawValues["r"] = r.ToString(CultureInfo.InvariantCulture);
                            rawValues["g"] = g.ToString(CultureInfo.InvariantCulture);
                            rawValues["b"] = b.ToString(CultureInfo.InvariantCulture);
                        }

                        continue;
                    }

                    rawValues[columnName] = value;
                }

                // Schema order, so an older table version's column order does not leak into the output.
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

                results.Add((key, record));
                ++rowIndex;
            }

            return results;
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
        // own parsing - just needs formatting to match how MergeFragments writes each type to XML.
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
    public sealed class RowFilter
    {
        private readonly HashSet<string> _values;

        public RowFilter(string column, IEnumerable<string> values)
        {
            Column = column;
            _values = new HashSet<string>(values, StringComparer.OrdinalIgnoreCase);
        }

        public string Column { get; }

        public bool Keeps(string value) => _values.Contains(value);
    }
}
