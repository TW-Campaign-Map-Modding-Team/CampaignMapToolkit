using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CAIME
{
    using FieldType = XmlSchemaFieldType;

    public enum XmlSchemaFieldType
    {
        YesNo,
        AutoNumber,
        Integer,
        Single,
        Double,
        Text
    }

    public class XmlSchemaField
    {
        public string       Name;
        public int          PrimaryKey;
        public FieldType    Type;
        public object       DefaultValue;
    }

    /// <summary>The fields of a database table, in the order its records hold them.</summary>
    public class XmlSchema
    {
        public List<XmlSchemaField> Fields { get; }

        public XmlSchema(IEnumerable<XmlSchemaField> fields)
        {
            Fields = fields.ToList();
        }

        /// <summary>
        /// A field whose default is <paramref name="defaultValue"/> as a record holds it (a yes/no
        /// field's as "1" or "0"), or its type's default when that is null or unreadable.
        /// </summary>
        public static XmlSchemaField CreateField(string name, FieldType type, bool isPrimaryKey, string defaultValue)
        {
            return new XmlSchemaField
            {
                Name            = name,
                Type            = type,
                PrimaryKey      = isPrimaryKey ? 1 : 0,
                DefaultValue    = GetDefaultValue(defaultValue, type),
            };
        }

        public int GetFieldsCount()
        {
            return Fields.Count;
        }

        public XmlSchemaField GetField(int index)
        {
            return Fields[index];
        }

        public static Type GetFieldType(XmlSchemaField field)
        {
            switch (field.Type)
            {
                case FieldType.YesNo:
                    return typeof(bool);
                case FieldType.Text:
                    return typeof(string);
                case FieldType.AutoNumber:
                case FieldType.Integer:
                    return typeof(int);
                case FieldType.Single:
                    return typeof(float);
                case FieldType.Double:
                    return typeof(double);
            }
            return null;
        }

        private static object GetDefaultValue(string value, FieldType type)
        {
            switch (type)
            {
                case FieldType.YesNo:
                    return value == "1";
                case FieldType.Text:
                    return value ?? string.Empty;
                case FieldType.Integer:
                case FieldType.AutoNumber:
                    return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue) ? intValue : 0;
                case FieldType.Single:
                    return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatValue) ? floatValue : 0.0f;
                case FieldType.Double:
                    return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double doubleValue) ? doubleValue : 0.0;
            }

            return null;
        }
    }
}
