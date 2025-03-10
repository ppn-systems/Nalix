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

/// <summary>
/// A very simple, light-weight JSON library written by Mario
/// to teach Geo how things are done.
///
/// This helper is useful for small tasks but does not represent a full-featured
/// serializer such as Serialization.NET.
/// </summary>
public partial class Json
{
    /// <summary>
    /// A simple JSON serializer.
    /// </summary>
    private class Serializer
    {
        // Use a thread-safe dictionary for indent strings caching.
        private static readonly ConcurrentDictionary<int, string> IndentStrings = new();

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
                throw new InvalidOperationException("The max depth (20) has been reached. Serializer cannot continue.");

            _options = options;
            _format = options.Format;
            _newLine = _format ? Environment.NewLine : string.Empty;
            _builder = new StringBuilder();
            _lastCommaSearch = FieldSeparatorChar + _newLine;
            _excludedNames = excludedNames;

            // First, attempt to resolve as a basic type.
            _result = ResolveBasicType(obj);
            if (!string.IsNullOrWhiteSpace(_result))
                return;

            // Update excluded names from type attributes.
            _options.ExcludeProperties = GetExcludedNames(obj?.GetType(), _excludedNames);

            // Handle circular references.
            if (options.IsObjectPresent(obj!))
            {
                _result = $"{{ \"$circref\": \"{Escape(obj!.GetHashCode().ToStringInvariant(), false)}\" }}";
                return;
            }

            // Choose the resolution method based on the type of obj.
            _result = obj switch
            {
                IDictionary { Count: 0 } => EmptyObjectLiteral,
                IDictionary dict => ResolveDictionary(dict, depth),
                IEnumerable enumerable when !enumerable.Cast<object>().Any() => EmptyArrayLiteral,
                IEnumerable and byte[] bytes => Serialize(bytes.ToBase64(), depth, _options, _excludedNames),
                IEnumerable enumerable => ResolveEnumerable(enumerable, depth),
                _ => ResolveObject(obj!, depth)
            };
        }

        internal static string Serialize(object? obj, int depth, SerializerOptions options, string[]? excludedNames = null) =>
            new Serializer(obj, depth, options, excludedNames)._result;

        internal static string[]? GetExcludedNames(Type? type, string[]? excludedNames)
        {
            if (type == null)
                return excludedNames;

            var allExcluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var properties = type.GetProperties();

            var includedProps = properties.Where(p => Attribute
                .IsDefined(p, typeof(JsonIncludeAttribute))).Select(p => p.Name).ToList();

            if (includedProps.Count != 0)
            {
                allExcluded.UnionWith(properties.Select(p => p.Name).Except(includedProps));
            }
            else
            {
                foreach (var prop in properties)
                {
                    if (Attribute.IsDefined(prop, typeof(JsonPropertyIgnoreAttribute)))
                        allExcluded.Add(prop.Name);
                }

                foreach (var prop in IgnoredPropertiesCache.Retrieve(type, t =>
                    t.GetProperties().Where(p => AttributeCache.DefaultCache.Value
                    .RetrieveOne<JsonPropertyAttribute>(p)?.Ignored == true).Select(p => p.Name)))
                {
                    allExcluded.Add(prop);
                }
            }

            if (allExcluded.Count == 0)
                return excludedNames;

            return excludedNames?.Any(name => !string.IsNullOrWhiteSpace(name)) == true
                ? [.. allExcluded.Intersect(excludedNames.Where(y => !string.IsNullOrWhiteSpace(y)), StringComparer.OrdinalIgnoreCase)]
                : [.. allExcluded];
        }

        private static string ResolveBasicType(object? obj)
        {
            return obj switch
            {
                null => NullLiteral,
                string s => Escape(s, true),
                bool b => b ? TrueLiteral : FalseLiteral,
                DateTime d => $"{StringQuotedChar}{d:s}{StringQuotedChar}",
                Type or Assembly or MethodInfo or PropertyInfo or EventInfo => Escape(obj.ToString() ?? string.Empty, true),
                _ => ResolveComplexBasicType(obj)
            };
        }

        private static string ResolveComplexBasicType(object obj)
        {
            Type targetType = obj.GetType();
            if (!Definitions.BasicTypesInfo.Value.TryGetValue(targetType, out ExtendedTypeInfo? info))
                return string.Empty;

            var escaped = Escape(info.ToStringInvariant(obj), false);
            return decimal.TryParse(escaped, out _) ? escaped : $"{StringQuotedChar}{escaped}{StringQuotedChar}";
        }

        private static bool IsNonEmptyJsonArrayOrObject(string serialized)
        {
            if (serialized == EmptyObjectLiteral || serialized == EmptyArrayLiteral)
                return false;

            return serialized.TrimStart().FirstOrDefault() is OpenObjectChar or OpenArrayChar;
        }

        private static string Escape(string? str, bool quoted)
        {
            if (str == null)
                return string.Empty;

            StringBuilder sb = new(str.Length * 2);
            if (quoted) sb.Append(StringQuotedChar);
            foreach (var ch in str)
            {
                switch (ch)
                {
                    case '\\':
                    case '"':
                    case '/':
                        sb.Append('\\').Append(ch);
                        break;

                    case '\b':
                        sb.Append("\\b");
                        break;

                    case '\t':
                        sb.Append("\\t");
                        break;

                    case '\n':
                        sb.Append("\\n");
                        break;

                    case '\f':
                        sb.Append("\\f");
                        break;

                    case '\r':
                        sb.Append("\\r");
                        break;

                    default:
                        if (ch < ' ')
                        {
                            sb.Append("\\u")
                              .Append(((int)ch).ToString("X4", CultureInfo.InvariantCulture));
                        }
                        else
                            sb.Append(ch);
                        break;
                }
            }
            if (quoted) sb.Append(StringQuotedChar);
            return sb.ToString();
        }

        private Dictionary<string, object?> CreateDictionary(
                    Dictionary<string, MemberInfo> fields, string targetType, object target)
        {
            Dictionary<string, object?> dict = new(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(_options.TypeSpecifier))
                dict[_options.TypeSpecifier!] = targetType;

            foreach (KeyValuePair<string, MemberInfo> kvp in fields)
            {
                if (_options.ExcludeProperties?.Contains(kvp.Key) == true)
                    continue;

                try
                {
                    dict[kvp.Key] = kvp.Value is PropertyInfo prop
                        ? target.ReadProperty(prop.Name)
                        : (kvp.Value as FieldInfo)?.GetValue(target);
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
                if (key == null) continue; // Skip null keys to avoid null reference exception

                this.Append(StringQuotedChar, depth + 1);
                this.Append(Escape(key.ToString() ?? string.Empty, false), 0);
                this.Append(StringQuotedChar, 0);

                _builder.Append(ValueSeparatorChar).Append(' ');
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

            if (targetType.IsEnum)
                return Convert.ToInt64(target, CultureInfo.InvariantCulture).ToStringInvariant();

            Dictionary<string, MemberInfo> fields = _options.GetProperties(targetType);

            if (fields.Count == 0 && string.IsNullOrWhiteSpace(_options.TypeSpecifier))
                return EmptyObjectLiteral;

            Dictionary<string, object?> dict = this.CreateDictionary(fields, targetType.ToString(), target);
            return Serialize(dict, depth, _options, _excludedNames);
        }

        private string ResolveEnumerable(IEnumerable target, int depth)
        {
            IEnumerable<object> items = target.Cast<object>();

            this.Append(OpenArrayChar, depth);
            this.AppendLine();

            int count = 0;
            foreach (object item in items)
            {
                string serialized = Serialize(item, depth + 1, _options, _excludedNames);
                if (IsNonEmptyJsonArrayOrObject(serialized))
                    this.Append(serialized, 0);
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
            _builder.Append(IndentStrings.GetOrAdd(depth, d => new string(' ', d * 4)));
        }

        private void RemoveLastComma()
        {
            int len = _builder.Length;
            int searchLen = _lastCommaSearch.Length;
            if (len < searchLen) return;

            bool match = true;
            for (int i = 0; i < searchLen; i++)
            {
                if (_builder[len - searchLen + i] != _lastCommaSearch[i])
                {
                    match = false;
                    break;
                }
            }
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
