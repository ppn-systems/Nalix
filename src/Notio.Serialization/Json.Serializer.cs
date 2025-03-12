using Notio.Common.Attributes;
using Notio.Serialization.Internal;
using Notio.Serialization.Internal.Extensions;
using Notio.Serialization.Internal.Reflection;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Notio.Serialization;

public partial class Json
{
    /// <summary>
    /// A simple JSON serializer.
    /// </summary>
    private class Serializer
    {
        // Thread-safe cache for indent strings
        private static readonly ConcurrentDictionary<int, string> IndentStrings = new();

        // Constants for common string patterns
        private static readonly string NewLine = Environment.NewLine;

        private readonly SerializerOptions _options;
        private readonly StringBuilder _builder;
        private readonly bool _format;
        private readonly string _newLine;
        private readonly string _lastCommaSearch;
        private readonly string[]? _excludedNames;

        private readonly string _result;

        private Serializer(object? obj, int depth, SerializerOptions options, string[]? excludedNames = null)
        {
            if (depth > 20)
                throw new InvalidOperationException("Maximum serialization depth (20) exceeded");

            _options = options;
            _format = options.Format;
            _newLine = _format ? NewLine : string.Empty;
            _builder = new StringBuilder();
            _lastCommaSearch = FieldSeparatorChar + _newLine;
            _excludedNames = excludedNames;

            // Try to resolve as a basic type first
            _result = ResolveBasicType(obj);
            if (!string.IsNullOrEmpty(_result))
                return;

            // Update excluded names from type attributes
            _options.ExcludeProperties = GetExcludedNames(obj?.GetType(), _excludedNames);

            // Check for circular references
            if (obj != null && options.IsObjectPresent(obj))
            {
                _result = $"{{ \"$circref\": \"{Escape(obj.GetHashCode().ToStringInvariant(), false)}\" }}";
                return;
            }

            // Choose serialization strategy based on object type
            _result = obj switch
            {
                null => NullLiteral,
                IDictionary { Count: 0 } => EmptyObjectLiteral,
                IDictionary dict => ResolveDictionary(dict, depth),
                IEnumerable { } enumerable when !enumerable.Cast<object>().Any() => EmptyArrayLiteral,
                byte[] bytes => Serialize(Convert.ToBase64String(bytes), depth, _options, _excludedNames),
                IEnumerable enumerable => ResolveEnumerable(enumerable, depth),
                _ => ResolveObject(obj, depth)
            };
        }

        internal static string Serialize(object? obj, int depth, SerializerOptions options, string[]? excludedNames = null) =>
            new Serializer(obj, depth, options, excludedNames)._result;

        internal static string[]? GetExcludedNames(Type? type, string[]? excludedNames)
        {
            if (type == null)
                return excludedNames;

            HashSet<string> allExcluded = new(StringComparer.OrdinalIgnoreCase);
            PropertyInfo[] properties = type.GetProperties();

            HashSet<string> propertyNames = properties
                .Select(p => p.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            HashSet<string> includedProps = properties
                .Where(p => Attribute.IsDefined(p, typeof(JsonIncludeAttribute)))
                .Select(p => p.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (includedProps.Count != 0)
                allExcluded.UnionWith(propertyNames.Except(includedProps));

            // Gộp với danh sách bị loại trừ từ tham số truyền vào
            if (excludedNames != null)
                allExcluded.UnionWith(excludedNames);

            return allExcluded.Count != 0 ? [.. allExcluded] : excludedNames;
        }


        private static string ResolveBasicType(object? obj)
        {
            return obj switch
            {
                null => NullLiteral,
                string s => Escape(s, true),
                bool b => b ? TrueLiteral : FalseLiteral,
                DateTime d => $"{StringQuotedChar}{d:s}{StringQuotedChar}",
                Type or Assembly or MethodInfo or PropertyInfo or EventInfo =>
                    Escape(obj.ToString() ?? string.Empty, true),
                _ => ResolveNumericalType(obj)
            };
        }

        private static string ResolveNumericalType(object obj)
        {
            Type targetType = obj.GetType();
            if (!Definitions.BasicTypesInfo.Value.TryGetValue(targetType, out ExtendedTypeInfo? info))
                return string.Empty;

            var stringValue = info.ToStringInvariant(obj);
            return decimal.TryParse(stringValue, out _) ? stringValue : $"{StringQuotedChar}{Escape(stringValue, false)}{StringQuotedChar}";
        }

        private static bool IsNonEmptyJsonArrayOrObject(string serialized)
        {
            if (serialized == EmptyObjectLiteral || serialized == EmptyArrayLiteral)
                return false;

            var firstNonWhitespace = serialized.TrimStart().FirstOrDefault();
            return firstNonWhitespace == OpenObjectChar || firstNonWhitespace == OpenArrayChar;
        }

        private static string Escape(string? str, bool quoted)
        {
            if (string.IsNullOrEmpty(str))
                return quoted ? "\"\"" : string.Empty;

            // Estimate buffer size by counting special chars
            int extraSpace = 0;
            foreach (char c in str)
            {
                if (c is '\\' or '"' or '/' or '\b' or '\t' or '\n' or '\f' or '\r' or < ' ')
                    extraSpace += c < ' ' ? 6 : 2;
            }

            // Allocate buffer
            int length = str.Length + extraSpace + (quoted ? 2 : 0);
            Span<char> buffer = length <= 1024 ? stackalloc char[length] : new char[length];

            int index = 0;
            if (quoted) buffer[index++] = '"';

            // Process each character
            foreach (char c in str)
            {
                switch (c)
                {
                    case '\\': buffer[index++] = '\\'; buffer[index++] = '\\'; break;
                    case '"': buffer[index++] = '\\'; buffer[index++] = '"'; break;
                    case '/': buffer[index++] = '\\'; buffer[index++] = '/'; break;
                    case '\b': buffer[index++] = '\\'; buffer[index++] = 'b'; break;
                    case '\t': buffer[index++] = '\\'; buffer[index++] = 't'; break;
                    case '\n': buffer[index++] = '\\'; buffer[index++] = 'n'; break;
                    case '\f': buffer[index++] = '\\'; buffer[index++] = 'f'; break;
                    case '\r': buffer[index++] = '\\'; buffer[index++] = 'r'; break;
                    default:
                        if (c < ' ')
                        {
                            buffer[index++] = '\\';
                            buffer[index++] = 'u';
                            buffer[index++] = '0';
                            buffer[index++] = '0';
                            buffer[index++] = ToHex((c >> 4) & 0xF);
                            buffer[index++] = ToHex(c & 0xF);
                        }
                        else
                        {
                            buffer[index++] = c;
                        }
                        break;
                }
            }

            if (quoted) buffer[index++] = '"';

            return new string(buffer.ToArray(), 0, index);
        }

        private static char ToHex(int value) => (char)(value < 10 ? '0' + value : 'A' + (value - 10));

        private Dictionary<string, object?> CreateDictionary(
            Dictionary<string, MemberInfo> fields, string targetType, object target)
        {
            Dictionary<string, object?> dict = new(StringComparer.OrdinalIgnoreCase);

            // Add type specifier if provided
            if (!string.IsNullOrEmpty(_options.TypeSpecifier))
                dict[_options.TypeSpecifier!] = targetType;

            // Add each field/property value
            foreach (var (key, member) in fields)
            {
                if (_options.ExcludeProperties?.Contains(key) == true)
                    continue;

                try
                {
                    dict[key] = member switch
                    {
                        PropertyInfo prop => target.ReadProperty(prop.Name),
                        FieldInfo field => field.GetValue(target),
                        _ => null
                    };
                }
                catch { /* Ignore errors */ }
            }

            return dict;
        }

        private string ResolveDictionary(IDictionary items, int depth)
        {
            this.Append(OpenObjectChar, depth);
            this.AppendLine();

            int count = 0;
            foreach (object key in items.Keys)
            {
                if (key == null) continue;

                // Append key
                this.Append($"\"{Escape(key.ToString()!, false)}\"", depth + 1);
                _builder.Append(ValueSeparatorChar).Append(' ');

                // Serialize and append value
                string serializedValue = Serialize(items[key], depth + 1, _options, _excludedNames);

                if (IsNonEmptyJsonArrayOrObject(serializedValue))
                    this.AppendLine();

                this.Append(serializedValue, 0);
                this.Append(FieldSeparatorChar, 0);
                this.AppendLine();
                count++;
            }

            this.RemoveLastComma();
            this.Append(CloseObjectChar, count > 0 ? depth : 0);

            return _builder.ToString();
        }

        private string ResolveObject(object target, int depth)
        {
            Type targetType = target.GetType();

            // Handle enum values
            if (targetType.IsEnum)
                return Convert.ToInt64(target, CultureInfo.InvariantCulture).ToStringInvariant();

            // Get all fields and properties to serialize
            Dictionary<string, MemberInfo> fields = _options.GetProperties(targetType);

            // Handle empty objects
            if (fields.Count == 0 && string.IsNullOrEmpty(_options.TypeSpecifier))
                return EmptyObjectLiteral;

            // Create dictionary of property values
            Dictionary<string, object?> dict = CreateDictionary(fields, targetType.ToString(), target);

            // Serialize the dictionary
            return Serialize(dict, depth, _options, _excludedNames);
        }

        private string ResolveEnumerable(IEnumerable target, int depth)
        {
            // Get items as objects
            IEnumerable<object> items = target.Cast<object>();

            // Start array
            this.Append(OpenArrayChar, depth);
            this.AppendLine();

            int count = 0;
            foreach (object item in items)
            {
                string serialized = Serialize(item, depth + 1, _options, _excludedNames);

                // Format nested objects properly
                if (IsNonEmptyJsonArrayOrObject(serialized))
                    this.Append(serialized, depth + 1);
                else
                    this.Append(serialized, depth + 1);

                this.Append(FieldSeparatorChar, 0);
                this.AppendLine();
                count++;
            }

            this.RemoveLastComma();
            this.Append(CloseArrayChar, count > 0 ? depth : 0);
            return _builder.ToString();
        }

        private void SetIndent(int depth)
        {
            if (!_format || depth <= 0) return;

            // Get cached indent string or create new one
            string indent = IndentStrings.GetOrAdd(depth, d => new string(' ', d * 4));
            _builder.Append(indent);
        }

        private void RemoveLastComma()
        {
            int len = _builder.Length;
            int searchLen = _lastCommaSearch.Length;
            if (len < searchLen) return;

            // Check if the last characters match the comma pattern
            bool match = true;
            for (int i = 0; i < searchLen; i++)
            {
                if (_builder[len - searchLen + i] != _lastCommaSearch[i])
                {
                    match = false;
                    break;
                }
            }

            // Remove the comma if found
            if (match)
                _builder.Remove(len - searchLen, 1);
        }

        private void Append(string text, int depth)
        {
            this.SetIndent(depth);
            _builder.Append(text);
        }

        private void Append(char ch, int depth)
        {
            this.SetIndent(depth);
            _builder.Append(ch);
        }

        private void AppendLine()
        {
            if (_format)
                _builder.Append(_newLine);
        }
    }
}
