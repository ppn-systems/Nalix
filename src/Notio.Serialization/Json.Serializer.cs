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

            var excludedByAttr = IgnoredPropertiesCache.Retrieve(type, t => t.GetProperties()
                .Where(x => AttributeCache.DefaultCache.Value.RetrieveOne<JsonPropertyAttribute>(x)?.Ignored == true)
                .Select(x => x.Name));

            IEnumerable<string> byAttr = excludedByAttr as string[] ?? excludedByAttr.ToArray();
            if (byAttr.Any() != true)
                return excludedNames;

            return excludedNames?.Any(name => !string.IsNullOrWhiteSpace(name)) == true
                ? byAttr.Intersect(excludedNames.Where(y => !string.IsNullOrWhiteSpace(y))).ToArray()
                : byAttr.ToArray();
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
            var targetType = obj.GetType();
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

            var sb = new StringBuilder(str.Length * 2);
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

        private Dictionary<string, object?> CreateDictionary(Dictionary<string, MemberInfo> fields, string targetType, object target)
        {
            var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(_options.TypeSpecifier))
                dict[_options.TypeSpecifier!] = targetType;

            foreach (var kvp in fields)
            {
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
            Append(OpenObjectChar, depth);
            AppendLine();

            int count = 0;
            foreach (var key in items.Keys)
            {
                if (key == null) continue; // Skip null keys to avoid null reference exception

                Append(StringQuotedChar, depth + 1);
                Append(Escape(key.ToString() ?? string.Empty, false), 0);
                Append(StringQuotedChar, 0);
                _builder.Append(ValueSeparatorChar).Append(' ');
                var serializedValue = Serialize(items[key], depth + 1, _options, _excludedNames);
                if (IsNonEmptyJsonArrayOrObject(serializedValue))
                    AppendLine();
                Append(serializedValue, 0);
                Append(FieldSeparatorChar, 0);
                AppendLine();
                count++;
            }
            RemoveLastComma();
            Append(CloseObjectChar, count > 0 ? depth : 0);
            return _builder.ToString();
        }

        private string ResolveObject(object target, int depth)
        {
            var targetType = target.GetType();
            if (targetType.IsEnum)
                return Convert.ToInt64(target, CultureInfo.InvariantCulture).ToStringInvariant();

            var fields = _options.GetProperties(targetType);
            if (fields.Count == 0 && string.IsNullOrWhiteSpace(_options.TypeSpecifier))
                return EmptyObjectLiteral;

            var dict = CreateDictionary(fields, targetType.ToString(), target);
            return Serialize(dict, depth, _options, _excludedNames);
        }

        private string ResolveEnumerable(IEnumerable target, int depth)
        {
            var items = target.Cast<object>();
            Append(OpenArrayChar, depth);
            AppendLine();
            int count = 0;
            foreach (var item in items)
            {
                var serialized = Serialize(item, depth + 1, _options, _excludedNames);
                if (IsNonEmptyJsonArrayOrObject(serialized))
                    Append(serialized, 0);
                else
                    Append(serialized, depth + 1);
                Append(FieldSeparatorChar, 0);
                AppendLine();
                count++;
            }
            RemoveLastComma();
            Append(CloseArrayChar, count > 0 ? depth : 0);
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
            SetIndent(depth);
            _builder.Append(text);
        }

        private void Append(char ch, int depth)
        {
            SetIndent(depth);
            _builder.Append(ch);
        }

        private void AppendLine()
        {
            if (_format)
                _builder.Append(_newLine);
        }
    }
}
