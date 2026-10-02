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
    /// Converts between RPFM's TSV export format and the Assembly Kit's data XML format. Reusable and
    /// self-contained: it takes explicit inputs (record element name, which columns are yes/no) rather
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
        /// Parses a TWaD_*.xml Assembly Kit schema. A caller needing more than one of the Get*
        /// helpers below should load the schema once and pass it in, rather than reparsing the file
        /// for each of them.
        /// </summary>
        public static XmlSchema LoadSchema(string twadSchemaPath)
        {
            var schema = new XmlSchema();
            schema.Load(XDocument.Load(twadSchemaPath));
            return schema;
        }

        /// <summary>
        /// Derives the set of yes/no column names from a TWaD_*.xml Assembly Kit schema. Used so the
        /// converter knows which TSV columns to translate from true/false to 1/0.
        /// </summary>
        public static ISet<string> GetBooleanColumns(string twadSchemaPath)
        {
            return GetBooleanColumns(LoadSchema(twadSchemaPath));
        }

        /// <inheritdoc cref="GetBooleanColumns(string)"/>
        public static ISet<string> GetBooleanColumns(XmlSchema schema)
        {
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var field in schema.Fields)
            {
                if (field.Type == XmlSchemaFieldType.YesNo)
                {
                    columns.Add(field.Name);
                }
            }

            return columns;
        }

        /// <summary>
        /// Derives the primary key column name(s) from a TWaD_*.xml Assembly Kit schema. Used to
        /// detect which rows from different table fragments actually refer to the same record when
        /// merging fragments from multiple packs. A table with no primary key returns an empty list.
        /// </summary>
        public static IReadOnlyList<string> GetPrimaryKeyColumns(string twadSchemaPath)
        {
            return GetPrimaryKeyColumns(LoadSchema(twadSchemaPath));
        }

        /// <inheritdoc cref="GetPrimaryKeyColumns(string)"/>
        public static IReadOnlyList<string> GetPrimaryKeyColumns(XmlSchema schema)
        {
            var columns = new List<string>();

            foreach (var field in schema.Fields)
            {
                if (field.PrimaryKey == 1)
                {
                    columns.Add(field.Name);
                }
            }

            return columns;
        }

        /// <summary>
        /// Derives the full field list from a TWaD_*.xml Assembly Kit schema, in schema order. Used to
        /// backfill columns a fragment's TSV doesn't have (see <see cref="MergeTsv"/>).
        /// </summary>
        public static IReadOnlyList<XmlSchemaField> GetFields(string twadSchemaPath)
        {
            return LoadSchema(twadSchemaPath).Fields;
        }

        /// <summary>
        /// Reads an existing Assembly Kit data XML file (the Assembly Kit's own copy of a table) into a
        /// lookup by primary key. Used to source fields RPFM has no way to supply at all - some
        /// Assembly Kit fields (e.g. "regions.is_sea") are computed/maintained by the Assembly Kit
        /// itself and never appear in any pack's raw table data, at any version - so a schema type
        /// default (e.g. "false") is often simply wrong for an existing record, whereas the Assembly
        /// Kit's own last-known value for it is correct by construction.
        /// Returns an empty lookup if the file does not exist or fails to parse (e.g. a table that is
        /// new to this Assembly Kit installation) rather than throwing - this is a best-effort source,
        /// not a required one.
        /// </summary>
        public static IReadOnlyDictionary<string, XElement> LoadExistingRecords(
            string existingXmlPath, string recordElementName, IReadOnlyList<string> primaryKeyColumns)
        {
            var result = new Dictionary<string, XElement>();

            if (string.IsNullOrEmpty(existingXmlPath) || !File.Exists(existingXmlPath)
                || primaryKeyColumns == null || primaryKeyColumns.Count == 0)
            {
                return result;
            }

            try
            {
                var document = XDocument.Load(existingXmlPath);
                foreach (var record in document.Root.Elements(recordElementName))
                {
                    var keyValues = new List<string>(primaryKeyColumns.Count);
                    foreach (var pkColumn in primaryKeyColumns)
                    {
                        keyValues.Add(record.Element(pkColumn)?.Value ?? string.Empty);
                    }

                    // An earlier record with the same key (should not normally happen) keeps its slot -
                    // consistent with how fragment merging resolves key collisions.
                    var key = string.Join(KeySeparator, keyValues);
                    if (!result.ContainsKey(key))
                    {
                        result[key] = record;
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerViewModel.Log($"Could not read existing Assembly Kit data for backfilling: {ex.Message}", LogLevel.Warning);
            }

            return result;
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
        /// Converts one RPFM TSV file into an Assembly Kit data XML file.
        /// </summary>
        /// <param name="tsvPath">Source .tsv file.</param>
        /// <param name="recordElementName">Element name for each record (the Assembly Kit table name, e.g. "campaign_ground_types").</param>
        /// <param name="booleanColumns">Columns to translate from true/false to 1/0.</param>
        /// <param name="outputXmlPath">Destination .xml file.</param>
        public static void TsvToXml(string tsvPath, string recordElementName, ISet<string> booleanColumns, string outputXmlPath)
        {
            var document = MergeTsv(new[] { (tsvPath, string.Empty) }, recordElementName, booleanColumns, Array.Empty<string>(), Array.Empty<XmlSchemaField>(), null, null);
            WriteAssemblyKitXml(document, outputXmlPath);
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
        /// than <paramref name="schemaFields"/> (RPFM's schema lags newer game patches). A schema field
        /// missing from a row is backfilled - preferably from <paramref name="existingRecords"/> (the
        /// Assembly Kit's own last-known value for that primary key, when there is one), falling back to
        /// the field's schema type default only for a key that has no existing record at all - rather
        /// than left out of the record entirely. CAIME's table loader expects every field to be present
        /// (e.g. "regions.is_sea", which tells land regions from sea) in the schema's exact declared
        /// order, and some such fields (again "is_sea") are never present in any pack's raw table data
        /// at all, at any version, because the Assembly Kit computes/maintains them itself - so a blind
        /// type default (e.g. "false") is often simply wrong where an existing record exists.
        ///
        /// "is_sea" specifically is instead sourced from <paramref name="regionIsSeaByKey"/> when the
        /// row's key is in it - the open map itself is ground truth for which regions are sea (it is
        /// what the game actually builds passable edges from), ranked above even
        /// <paramref name="existingRecords"/> since that may be stale relative to the currently open map.
        /// A row whose key <paramref name="regionIsSeaByKey"/> does not recognise falls through to
        /// <paramref name="existingRecords"/>/the schema default same as any other field.
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
            IReadOnlyDictionary<string, XElement> existingRecords,
            IReadOnlyDictionary<string, bool> regionIsSeaByKey,
            TsvRowFilter rowFilter = null)
        {
            var ordered = fragments.OrderBy(f => f.FragmentName, StringComparer.OrdinalIgnoreCase).ToList();

            var recordsByKey = new Dictionary<string, XElement>();
            var keyOrder = new List<string>();

            for (int fragmentIndex = 0; fragmentIndex < ordered.Count; ++fragmentIndex)
            {
                var keyedRecords = TsvToKeyedRecords(
                    ordered[fragmentIndex].TsvPath, recordElementName, booleanColumns, primaryKeyColumns, schemaFields, existingRecords, regionIsSeaByKey, rowFilter, fragmentIndex);

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

        /// <summary>
        /// Writes an Assembly Kit data XML file the way the Assembly Kit's own tools can read it: UTF-8
        /// without a byte order mark. Their XML reader does not skip one - it faults on the leading
        /// bytes and takes the whole ToolDataBuilder DLL down with an access violation, which surfaces
        /// as MapDataBuilder.exe crashing rather than as a parse error. Every XML file the Assembly Kit
        /// ships is BOM-less, and XDocument.Save(path) writes one, so the writer's encoding has to be
        /// set explicitly (same reason XmlToTsv uses a BOM-less UTF8Encoding).
        /// </summary>
        public static void WriteAssemblyKitXml(XDocument document, string path)
        {
            var writerSettings = new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                Indent   = true,
            };

            Directory.CreateDirectory(Path.GetDirectoryName(path));

            using (var writer = XmlWriter.Create(path, writerSettings))
            {
                document.Save(writer);
            }
        }

        private static List<(string Key, XElement Record)> TsvToKeyedRecords(
            string tsvPath, string recordElementName, ISet<string> booleanColumns,
            IReadOnlyList<string> primaryKeyColumns, IReadOnlyList<XmlSchemaField> schemaFields,
            IReadOnlyDictionary<string, XElement> existingRecords,
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
                            // Invariant, not the current culture: these are data values read back by
                            // the Assembly Kit, not text shown to the user.
                            rawValues["r"] = r.ToString(CultureInfo.InvariantCulture);
                            rawValues["g"] = g.ToString(CultureInfo.InvariantCulture);
                            rawValues["b"] = b.ToString(CultureInfo.InvariantCulture);
                            continue;
                        }

                        rawValues[columnName] = value;
                    }

                    XElement existingRecord = null;
                    existingRecords?.TryGetValue(key, out existingRecord);

                    // Emit fields in the schema's own declared order. The Assembly Kit loader reads a
                    // fixed field sequence per record rather than by name, so a fragment's TSV column
                    // order - which can differ from the schema, especially for an older table version -
                    // must not leak into the output. A field missing from this row (the fragment's
                    // table version predates it - e.g. added in a later game patch, or the field is
                    // Assembly-Kit-maintained and never appears in any pack at all, like "is_sea") is
                    // backfilled from the Assembly Kit's own existing record for this key when there is
                    // one - its last-known value is correct by construction, unlike a blind type default
                    // - falling back to the schema default only when the key has no existing record.
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
                        else if (existingRecord?.Element(field.Name) is XElement existingField)
                        {
                            record.Add(new XElement(field.Name, existingField.Value));
                        }
                        else
                        {
                            record.Add(new XElement(field.Name, FormatDefaultValue(field)));
                        }
                    }
                }
                else
                {
                    // No schema available (the single-file TsvToXml path) - fall back to TSV column
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
                        // name the Assembly Kit does not use ("unnamed colour group_1"). The Assembly
                        // Kit expects separate decimal r/g/b integer fields, so expand it.
                        if (IsColourColumn(columnName) && TryExpandColour(value, out int r, out int g, out int b))
                        {
                            record.Add(new XElement("r", r));
                            record.Add(new XElement("g", g));
                            record.Add(new XElement("b", b));
                            continue;
                        }

                        // Skip columns whose names are not valid XML element names and that we cannot
                        // map. The Assembly Kit loader reads fields by name from its own schema, so any
                        // extra pack columns it does not know about are irrelevant to it.
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

        /// <summary>
        /// Converts an Assembly Kit data XML file back into an RPFM TSV file. Provided for
        /// completeness and reuse; the RPFM preparation pipeline itself only needs TSV -&gt; XML.
        /// </summary>
        public static void XmlToTsv(
            string xmlPath,
            string recordElementName,
            IReadOnlyList<string> columnOrder,
            ISet<string> booleanColumns,
            string tableFolderName,
            int version,
            string outputTsvPath)
        {
            var document = XDocument.Load(xmlPath);

            var sb = new StringBuilder();
            sb.Append(string.Join("\t", columnOrder)).Append('\n');
            sb.Append('#').Append(tableFolderName).Append(';').Append(version).Append(';')
              .Append("db/").Append(tableFolderName).Append('/').Append(recordElementName)
              .Append(new string('\t', Math.Max(0, columnOrder.Count - 1))).Append('\n');

            foreach (var record in document.Root.Elements(recordElementName))
            {
                var values = new List<string>(columnOrder.Count);
                foreach (var column in columnOrder)
                {
                    var element = record.Element(column);
                    var value   = element?.Value ?? string.Empty;

                    if (booleanColumns != null && booleanColumns.Contains(column))
                    {
                        value = ToTsvBoolean(value);
                    }

                    values.Add(EscapeTsv(value));
                }

                sb.Append(string.Join("\t", values)).Append('\n');
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputTsvPath));
            File.WriteAllText(outputTsvPath, sb.ToString(), new UTF8Encoding(false));
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

        // RPFM writes "true"/"false"; the Assembly Kit XML stores "1"/"0".
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

        private static string ToTsvBoolean(string value)
        {
            return value == "1" ? "true" : "false";
        }

        private static string EscapeTsv(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");
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
