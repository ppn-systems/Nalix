using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System;

namespace Notio.Shared.Formatters;

public static class Json
{
    private class Converter
    {
        private static readonly ConcurrentDictionary<MemberInfo, string> MemberInfoNameCache = new ConcurrentDictionary<MemberInfo, string>();

        private static readonly ConcurrentDictionary<Type, Type?> ListAddMethodCache = new ConcurrentDictionary<Type, Type>();

        private readonly object? _target;

        private readonly Type _targetType;

        private readonly bool _includeNonPublic;

        private readonly JsonSerializerCase _jsonSerializerCase;

        private Converter(object? source, Type targetType, ref object? targetInstance, bool includeNonPublic, JsonSerializerCase jsonSerializerCase)
        {
            _targetType = ((targetInstance != null) ? targetInstance.GetType() : targetType);
            _includeNonPublic = includeNonPublic;
            _jsonSerializerCase = jsonSerializerCase;
            if (source != null)
            {
                Type type = source.GetType();
                if (_targetType == null || _targetType == typeof(object))
                {
                    _targetType = type;
                }

                if (type == _targetType)
                {
                    _target = source;
                }
                else if (TrySetInstance(targetInstance, source, ref _target))
                {
                    ResolveObject(source, ref _target);
                }
            }
        }

        internal static object? FromJsonResult(object? source, JsonSerializerCase jsonSerializerCase, Type? targetType = null, bool includeNonPublic = false)
        {
            object targetInstance = null;
            return new Converter(source, targetType ?? typeof(object), ref targetInstance, includeNonPublic, jsonSerializerCase).GetResult();
        }

        private static object? FromJsonResult(object source, Type targetType, ref object? targetInstance, bool includeNonPublic)
        {
            return new Converter(source, targetType, ref targetInstance, includeNonPublic, JsonSerializerCase.None).GetResult();
        }

        private static Type? GetAddMethodParameterType(Type targetType)
        {
            return ListAddMethodCache.GetOrAdd(targetType, delegate (Type x)
            {
                MethodInfo? methodInfo = x.GetMethods().FirstOrDefault((MethodInfo m) => m.Name == "Add" && m.IsPublic && m.GetParameters().Length == 1);
                return ((object)methodInfo == null) ? null : methodInfo.GetParameters()[0].ParameterType;
            });
        }

        private static void GetByteArray(string sourceString, ref object? target)
        {
            try
            {
                target = Convert.FromBase64String(sourceString);
            }
            catch (FormatException)
            {
                target = Encoding.UTF8.GetBytes(sourceString);
            }
        }

        private object? GetSourcePropertyValue(IDictionary<string, object> sourceProperties, MemberInfo targetProperty)
        {
            string orAdd = MemberInfoNameCache.GetOrAdd(targetProperty, (MemberInfo x) => AttributeCache.DefaultCache.Value.RetrieveOne<JsonPropertyAttribute>(x)?.PropertyName ?? x.Name.GetNameWithCase(_jsonSerializerCase));
            return sourceProperties.GetValueOrDefault(orAdd);
        }

        private bool TrySetInstance(object? targetInstance, object source, ref object? target)
        {
            if (targetInstance == null)
            {
                try
                {
                    source.CreateTarget(_targetType, _includeNonPublic, ref target);
                }
                catch
                {
                    return false;
                }
            }
            else
            {
                target = targetInstance;
            }

            return true;
        }

        private object? GetResult()
        {
            return _target ?? _targetType.GetDefault();
        }

        private void ResolveObject(object source, ref object? target)
        {
            if (!(source is string sourceString))
            {
                if (source is Dictionary<string, object> dictionary)
                {
                    if (target is IDictionary targetDictionary)
                    {
                        PopulateDictionary(dictionary, targetDictionary);
                        return;
                    }

                    Dictionary<string, object> sourceProperties = dictionary;
                    PopulateObject(sourceProperties);
                    return;
                }

                if (source is List<object> list)
                {
                    if (target is Array array)
                    {
                        PopulateArray(list, array);
                        return;
                    }

                    List<object> objects = list;
                    if (target is IList list2)
                    {
                        PopulateIList(objects, list2);
                        return;
                    }
                }
            }
            else if (_targetType == typeof(byte[]))
            {
                GetByteArray(sourceString, ref target);
                return;
            }

            string text = source.ToStringInvariant();
            if (!_targetType.TryParseBasicType(text, out target))
            {
                GetEnumValue(text, ref target);
            }
        }

        private void PopulateIList(IEnumerable<object> objects, IList list)
        {
            Type addMethodParameterType = GetAddMethodParameterType(_targetType);
            if (addMethodParameterType == null)
            {
                return;
            }

            foreach (object @object in objects)
            {
                try
                {
                    list.Add(FromJsonResult(@object, _jsonSerializerCase, addMethodParameterType, _includeNonPublic));
                }
                catch
                {
                }
            }
        }

        private void PopulateArray(IList<object> objects, Array array)
        {
            Type elementType = _targetType.GetElementType();
            for (int i = 0; i < objects.Count; i++)
            {
                try
                {
                    object value = FromJsonResult(objects[i], _jsonSerializerCase, elementType, _includeNonPublic);
                    array.SetValue(value, i);
                }
                catch
                {
                }
            }
        }

        private void GetEnumValue(string sourceStringValue, ref object? target)
        {
            Type type = Nullable.GetUnderlyingType(_targetType);
            if (type == null && _targetType.IsEnum)
            {
                type = _targetType;
            }

            if (type == null)
            {
                return;
            }

            try
            {
                target = Enum.Parse(type, sourceStringValue);
            }
            catch
            {
            }
        }

        private void PopulateDictionary(IDictionary<string, object> sourceProperties, IDictionary targetDictionary)
        {
            MethodInfo methodInfo = _targetType.GetMethods().FirstOrDefault((MethodInfo m) => m.Name == "Add" && m.IsPublic && m.GetParameters().Length == 2);
            if (methodInfo == null)
            {
                return;
            }

            ParameterInfo[] parameters = methodInfo.GetParameters();
            if (parameters[0].ParameterType != typeof(string))
            {
                return;
            }

            Type parameterType = parameters[1].ParameterType;
            foreach (KeyValuePair<string, object> sourceProperty in sourceProperties)
            {
                try
                {
                    object value = FromJsonResult(sourceProperty.Value, _jsonSerializerCase, parameterType, _includeNonPublic);
                    targetDictionary.Add(sourceProperty.Key, value);
                }
                catch
                {
                }
            }
        }

        private void PopulateObject(IDictionary<string, object> sourceProperties)
        {
            if (sourceProperties != null)
            {
                if (_targetType.IsValueType)
                {
                    PopulateFields(sourceProperties);
                }

                PopulateProperties(sourceProperties);
            }
        }

        private void PopulateProperties(IDictionary<string, object> sourceProperties)
        {
            foreach (PropertyInfo item in PropertyTypeCache.DefaultCache.Value.RetrieveFilteredProperties(_targetType, onlyPublic: false, (PropertyInfo p) => p.CanWrite))
            {
                object sourcePropertyValue = GetSourcePropertyValue(sourceProperties, item);
                if (sourcePropertyValue != null)
                {
                    try
                    {
                        object targetInstance = ((!item.PropertyType.IsArray) ? _target.ReadProperty(item.Name) : null);
                        object value = FromJsonResult(sourcePropertyValue, item.PropertyType, ref targetInstance, _includeNonPublic);
                        _target.WriteProperty(item.Name, value);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private void PopulateFields(IDictionary<string, object> sourceProperties)
        {
            foreach (FieldInfo item in FieldTypeCache.DefaultCache.Value.RetrieveAllFields(_targetType))
            {
                object sourcePropertyValue = GetSourcePropertyValue(sourceProperties, item);
                if (sourcePropertyValue != null)
                {
                    object value = FromJsonResult(sourcePropertyValue, _jsonSerializerCase, item.FieldType, _includeNonPublic);
                    try
                    {
                        item.SetValue(_target, value);
                    }
                    catch
                    {
                    }
                }
            }
        }
    }

    //
    // Summary:
    //     A simple JSON Deserializer.
    private class Deserializer
    {
        //
        // Summary:
        //     Defines the different JSON read states.
        private enum ReadState
        {
            WaitingForRootOpen,
            WaitingForField,
            WaitingForColon,
            WaitingForValue,
            WaitingForNextOrRootClose
        }

        private readonly object? _result;

        private readonly string _json;

        private Dictionary<string, object?> _resultObject;

        private List<object?> _resultArray;

        private ReadState _state;

        private string? _currentFieldName;

        private int _index;

        private Deserializer(string json, int startIndex)
        {
            _json = json;
            for (_index = startIndex; _index < _json.Length; _index++)
            {
                switch (_state)
                {
                    case ReadState.WaitingForRootOpen:
                        WaitForRootOpen();
                        break;

                    case ReadState.WaitingForField:
                        if (!char.IsWhiteSpace(_json, _index))
                        {
                            if ((_resultObject != null && _json[_index] == '}') || (_resultArray != null && _json[_index] == ']'))
                            {
                                _result = ((object)_resultObject) ?? ((object)_resultArray);
                                return;
                            }

                            if (_json[_index] != '"')
                            {
                                throw CreateParserException($"'{'"'}'");
                            }

                            int fieldNameCount = GetFieldNameCount();
                            _currentFieldName = Unescape(_json.SliceLength(_index + 1, fieldNameCount));
                            _index += fieldNameCount + 1;
                            _state = ReadState.WaitingForColon;
                        }

                        break;

                    case ReadState.WaitingForColon:
                        if (!char.IsWhiteSpace(_json, _index))
                        {
                            if (_json[_index] != ':')
                            {
                                throw CreateParserException($"'{':'}'");
                            }

                            _state = ReadState.WaitingForValue;
                        }

                        break;

                    case ReadState.WaitingForValue:
                        if (!char.IsWhiteSpace(_json, _index))
                        {
                            if ((_resultObject != null && _json[_index] == '}') || (_resultArray != null && _json[_index] == ']'))
                            {
                                _result = ((object)_resultObject) ?? ((object)_resultArray);
                                return;
                            }

                            ExtractValue();
                        }

                        break;

                    default:
                        if (_state != ReadState.WaitingForNextOrRootClose || char.IsWhiteSpace(_json, _index))
                        {
                            break;
                        }

                        if (_json[_index] == ',')
                        {
                            if (_resultObject != null)
                            {
                                _state = ReadState.WaitingForField;
                                _currentFieldName = null;
                            }
                            else
                            {
                                _state = ReadState.WaitingForValue;
                            }

                            break;
                        }

                        if ((_resultObject == null || _json[_index] != '}') && (_resultArray == null || _json[_index] != ']'))
                        {
                            throw CreateParserException($"'{','}' '{'}'}' or '{']'}'");
                        }

                        _result = ((object)_resultObject) ?? ((object)_resultArray);
                        return;
                }
            }
        }

        internal static object? DeserializeInternal(string json)
        {
            return new Deserializer(json, 0)._result;
        }

        private void WaitForRootOpen()
        {
            if (!char.IsWhiteSpace(_json, _index))
            {
                switch (_json[_index])
                {
                    case '{':
                        _resultObject = new Dictionary<string, object>();
                        _state = ReadState.WaitingForField;
                        break;

                    case '[':
                        _resultArray = new List<object>();
                        _state = ReadState.WaitingForValue;
                        break;

                    default:
                        throw CreateParserException($"'{'{'}' or '{'['}'");
                }
            }
        }

        private void ExtractValue()
        {
            switch (_json[_index])
            {
                case '"':
                    ExtractStringQuoted();
                    break;

                case '[':
                case '{':
                    ExtractObject();
                    break;

                case 't':
                    ExtractConstant("true", true);
                    break;

                case 'f':
                    ExtractConstant("false", false);
                    break;

                case 'n':
                    ExtractConstant("null");
                    break;

                default:
                    ExtractNumber();
                    break;
            }

            _currentFieldName = null;
            _state = ReadState.WaitingForNextOrRootClose;
        }

        private static string Unescape(string str)
        {
            if (str.IndexOf('\\') < 0)
            {
                return str;
            }

            StringBuilder stringBuilder = new StringBuilder(str.Length);
            for (int i = 0; i < str.Length; i++)
            {
                if (str[i] != '\\')
                {
                    stringBuilder.Append(str[i]);
                    continue;
                }

                if (i + 1 > str.Length - 1)
                {
                    break;
                }

                switch (str[i + 1])
                {
                    case 'u':
                        i = ExtractEscapeSequence(str, i, stringBuilder);
                        break;

                    case 'b':
                        stringBuilder.Append('\b');
                        i++;
                        break;

                    case 't':
                        stringBuilder.Append('\t');
                        i++;
                        break;

                    case 'n':
                        stringBuilder.Append('\n');
                        i++;
                        break;

                    case 'f':
                        stringBuilder.Append('\f');
                        i++;
                        break;

                    case 'r':
                        stringBuilder.Append('\r');
                        i++;
                        break;

                    default:
                        stringBuilder.Append(str[i + 1]);
                        i++;
                        break;
                }
            }

            return stringBuilder.ToString();
        }

        private static int ExtractEscapeSequence(string str, int i, StringBuilder builder)
        {
            int startIndex = i + 2;
            int num = i + 5;
            if (num > str.Length - 1)
            {
                builder.Append(str[i + 1]);
                i++;
                return i;
            }

            byte[] bytes = str.Slice(startIndex, num).ConvertHexadecimalToBytes();
            builder.Append(Encoding.BigEndianUnicode.GetChars(bytes));
            i += 5;
            return i;
        }

        private int GetFieldNameCount()
        {
            int num = 0;
            for (int i = _index + 1; i < _json.Length && (_json[i] != '"' || _json[i - 1] == '\\'); i++)
            {
                num++;
            }

            return num;
        }

        private void ExtractObject()
        {
            Deserializer deserializer = new Deserializer(_json, _index);
            if (_currentFieldName != null)
            {
                _resultObject[_currentFieldName] = deserializer._result;
            }
            else
            {
                _resultArray.Add(deserializer._result);
            }

            _index = deserializer._index;
        }

        private void ExtractNumber()
        {
            int num = 0;
            for (int i = _index; i < _json.Length && !char.IsWhiteSpace(_json[i]) && _json[i] != ',' && (_resultObject == null || _json[i] != '}') && (_resultArray == null || _json[i] != ']'); i++)
            {
                num++;
            }

            if (!decimal.TryParse(_json.SliceLength(_index, num), NumberStyles.Number, CultureInfo.InvariantCulture, out var result))
            {
                throw CreateParserException("[number]");
            }

            if (_currentFieldName != null)
            {
                _resultObject[_currentFieldName] = result;
            }
            else
            {
                _resultArray.Add(result);
            }

            _index += num - 1;
        }

        private void ExtractConstant(string boolValue, bool? value = null)
        {
            if (_json.SliceLength(_index, boolValue.Length) != boolValue)
            {
                throw CreateParserException($"'{':'}'");
            }

            if (_currentFieldName != null)
            {
                _resultObject[_currentFieldName] = value;
            }
            else
            {
                _resultArray.Add(value);
            }

            _index += boolValue.Length - 1;
        }

        private void ExtractStringQuoted()
        {
            int num = 0;
            bool flag = false;
            for (int i = _index + 1; i < _json.Length && (_json[i] != '"' || flag); i++)
            {
                flag = _json[i] == '\\' && !flag;
                num++;
            }

            string text = Unescape(_json.SliceLength(_index + 1, num));
            if (_currentFieldName != null)
            {
                _resultObject[_currentFieldName] = text;
            }
            else
            {
                _resultArray.Add(text);
            }

            _index += num + 1;
        }

        private FormatException CreateParserException(string expected)
        {
            var (num3, num4) = _json.TextPositionAt(_index);
            return new FormatException($"Parser error (Line {num3}, Col {num4}, State {_state}): Expected {expected} but got '{_json[_index]}'.");
        }
    }

    //
    // Summary:
    //     A simple JSON serializer.
    private class Serializer
    {
        private static readonly Dictionary<int, string> IndentStrings = new Dictionary<int, string>();

        private readonly SerializerOptions _options;

        private readonly string _result;

        private readonly StringBuilder _builder;

        private readonly string _lastCommaSearch;

        private readonly string[]? _excludedNames;

        //
        // Summary:
        //     Initializes a new instance of the Swan.Formatters.Json.Serializer class.
        //
        // Parameters:
        //   obj:
        //     The object.
        //
        //   depth:
        //     The depth.
        //
        //   options:
        //     The options.
        private Serializer(object? obj, int depth, SerializerOptions options, string[]? excludedNames = null)
        {
            if (depth > 20)
            {
                throw new InvalidOperationException("The max depth (20) has been reached. Serializer can not continue.");
            }

            _result = ResolveBasicType(obj);
            if (!string.IsNullOrWhiteSpace(_result))
            {
                return;
            }

            _options = options;
            if (_excludedNames == null)
            {
                _excludedNames = excludedNames;
            }

            _options.ExcludeProperties = GetExcludedNames(obj?.GetType(), _excludedNames);
            if (options.IsObjectPresent(obj))
            {
                _result = "{ \"$circref\": \"" + Escape(obj.GetHashCode().ToStringInvariant(), quoted: false) + "\" }";
                return;
            }

            _lastCommaSearch = "," + (_options.Format ? Environment.NewLine : string.Empty);
            _builder = new StringBuilder();
            string result;
            if (!(obj is IDictionary dictionary))
            {
                if (obj is IEnumerable enumerable)
                {
                    if (!enumerable.Cast<object>().Any())
                    {
                        result = "[ ]";
                    }
                    else if (enumerable is byte[] bytes)
                    {
                        result = Serialize(bytes.ToBase64(), depth, _options, _excludedNames);
                    }
                    else
                    {
                        IEnumerable target = enumerable;
                        result = ResolveEnumerable(target, depth);
                    }
                }
                else
                {
                    result = ResolveObject(obj, depth);
                }
            }
            else if (dictionary.Count == 0)
            {
                result = "{ }";
            }
            else
            {
                IDictionary items = dictionary;
                result = ResolveDictionary(items, depth);
            }

            _result = result;
        }

        internal static string Serialize(object? obj, int depth, SerializerOptions options, string[]? excludedNames = null)
        {
            return new Serializer(obj, depth, options, excludedNames)._result;
        }

        internal static string[]? GetExcludedNames(Type? type, string[]? excludedNames)
        {
            if (type == null)
            {
                return excludedNames;
            }

            IEnumerable<string> enumerable = IgnoredPropertiesCache.Retrieve(type, (Type t) => from x in t.GetProperties()
                                                                                               where AttributeCache.DefaultCache.Value.RetrieveOne<JsonPropertyAttribute>(x)?.Ignored ?? false
                                                                                               select x.Name);
            if (enumerable == null || !enumerable.Any())
            {
                return excludedNames;
            }

            if (excludedNames == null || !excludedNames.Any(string.IsNullOrWhiteSpace))
            {
                return enumerable.ToArray();
            }

            return enumerable.Intersect(excludedNames.Where((string y) => !string.IsNullOrWhiteSpace(y))).ToArray();
        }

        private static string ResolveBasicType(object? obj)
        {
            if (obj != null)
            {
                if (!(obj is string str))
                {
                    if (!(obj is bool))
                    {
                        if (!(obj is Type) && !(obj is Assembly) && !(obj is MethodInfo) && !(obj is PropertyInfo) && !(obj is EventInfo))
                        {
                            if (obj is DateTime dateTime)
                            {
                                return $"{'"'}{dateTime:s}{'"'}";
                            }

                            Type type = obj.GetType();
                            if (!Definitions.BasicTypesInfo.Value.ContainsKey(type))
                            {
                                return string.Empty;
                            }

                            string text = Escape(Definitions.BasicTypesInfo.Value[type].ToStringInvariant(obj), quoted: false);
                            if (!decimal.TryParse(text, out var _))
                            {
                                return $"{'"'}{text}{'"'}";
                            }

                            return text ?? "";
                        }

                        return Escape(obj.ToString(), quoted: true);
                    }

                    if (!(bool)obj)
                    {
                        return "false";
                    }

                    return "true";
                }

                return Escape(str, quoted: true);
            }

            return "null";
        }

        private static bool IsNonEmptyJsonArrayOrObject(string serialized)
        {
            if (serialized == "{ }" || serialized == "[ ]")
            {
                return false;
            }

            return (from c in serialized
                    where c != ' '
                    select c == '{' || c == '[').FirstOrDefault();
        }

        private static string Escape(string str, bool quoted)
        {
            if (str == null)
            {
                return string.Empty;
            }

            StringBuilder stringBuilder = new StringBuilder(str.Length * 2);
            if (quoted)
            {
                stringBuilder.Append('"');
            }

            Escape(str, stringBuilder);
            if (quoted)
            {
                stringBuilder.Append('"');
            }

            return stringBuilder.ToString();
        }

        private static void Escape(string str, StringBuilder builder)
        {
            foreach (char c in str)
            {
                switch (c)
                {
                    case '"':
                    case '/':
                    case '\\':
                        builder.Append('\\').Append(c);
                        continue;
                    case '\b':
                        builder.Append("\\b");
                        continue;
                    case '\t':
                        builder.Append("\\t");
                        continue;
                    case '\n':
                        builder.Append("\\n");
                        continue;
                    case '\f':
                        builder.Append("\\f");
                        continue;
                    case '\r':
                        builder.Append("\\r");
                        continue;
                }

                if (c < ' ')
                {
                    byte[] bytes = BitConverter.GetBytes((ushort)c);
                    if (!BitConverter.IsLittleEndian)
                    {
                        Array.Reverse((Array)bytes);
                    }

                    builder.Append("\\u").Append(bytes[1].ToString("X", CultureInfo.InvariantCulture).PadLeft(2, '0')).Append(bytes[0].ToString("X", CultureInfo.InvariantCulture).PadLeft(2, '0'));
                }
                else
                {
                    builder.Append(c);
                }
            }
        }

        private Dictionary<string, object?> CreateDictionary(Dictionary<string, MemberInfo> fields, string targetType, object target)
        {
            Dictionary<string, object> dictionary = new Dictionary<string, object>();
            if (!string.IsNullOrWhiteSpace(_options.TypeSpecifier))
            {
                dictionary[_options.TypeSpecifier] = targetType;
            }

            foreach (KeyValuePair<string, MemberInfo> field in fields)
            {
                try
                {
                    dictionary[field.Key] = ((field.Value is PropertyInfo propertyInfo) ? target.ReadProperty(propertyInfo.Name) : (field.Value as FieldInfo)?.GetValue(target));
                }
                catch
                {
                }
            }

            return dictionary;
        }

        private string ResolveDictionary(IDictionary items, int depth)
        {
            Append('{', depth);
            AppendLine();
            int num = 0;
            foreach (object key in items.Keys)
            {
                Append('"', depth + 1);
                Escape(key.ToString(), _builder);
                _builder.Append('"').Append(':').Append(" ");
                string text = Serialize(items[key], depth + 1, _options, _excludedNames);
                if (IsNonEmptyJsonArrayOrObject(text))
                {
                    AppendLine();
                }

                Append(text, 0);
                Append(',', 0);
                AppendLine();
                num++;
            }

            RemoveLastComma();
            Append('}', (num > 0) ? depth : 0);
            return _builder.ToString();
        }

        private string ResolveObject(object target, int depth)
        {
            Type type = target.GetType();
            if (type.IsEnum)
            {
                return Convert.ToInt64(target, CultureInfo.InvariantCulture).ToStringInvariant();
            }

            Dictionary<string, MemberInfo> properties = _options.GetProperties(type);
            if (properties.Count == 0 && string.IsNullOrWhiteSpace(_options.TypeSpecifier))
            {
                return "{ }";
            }

            return Serialize(CreateDictionary(properties, type.ToString(), target), depth, _options, _excludedNames);
        }

        private string ResolveEnumerable(IEnumerable target, int depth)
        {
            IEnumerable<object> enumerable = target.Cast<object>();
            Append('[', depth);
            AppendLine();
            int num = 0;
            foreach (object item in enumerable)
            {
                string text = Serialize(item, depth + 1, _options, _excludedNames);
                if (IsNonEmptyJsonArrayOrObject(text))
                {
                    Append(text, 0);
                }
                else
                {
                    Append(text, depth + 1);
                }

                Append(',', 0);
                AppendLine();
                num++;
            }

            RemoveLastComma();
            Append(']', (num > 0) ? depth : 0);
            return _builder.ToString();
        }

        private void SetIndent(int depth)
        {
            if (_options.Format && depth > 0)
            {
                _builder.Append(IndentStrings.GetOrAdd(depth, (int x) => new string(' ', x * 4)));
            }
        }

        //
        // Summary:
        //     Removes the last comma in the current string builder.
        private void RemoveLastComma()
        {
            if (_builder.Length >= _lastCommaSearch.Length && !_lastCommaSearch.Where((char t, int i) => _builder[_builder.Length - _lastCommaSearch.Length + i] != t).Any())
            {
                _builder.Remove(_builder.Length - _lastCommaSearch.Length, 1);
            }
        }

        private void Append(string text, int depth)
        {
            SetIndent(depth);
            _builder.Append(text);
        }

        private void Append(char text, int depth)
        {
            SetIndent(depth);
            _builder.Append(text);
        }

        private void AppendLine()
        {
            if (_options.Format)
            {
                _builder.Append(Environment.NewLine);
            }
        }
    }

    internal const string AddMethodName = "Add";

    private const char OpenObjectChar = '{';

    private const char CloseObjectChar = '}';

    private const char OpenArrayChar = '[';

    private const char CloseArrayChar = ']';

    private const char FieldSeparatorChar = ',';

    private const char ValueSeparatorChar = ':';

    private const char StringEscapeChar = '\\';

    private const char StringQuotedChar = '"';

    private const string EmptyObjectLiteral = "{ }";

    private const string EmptyArrayLiteral = "[ ]";

    private const string TrueLiteral = "true";

    private const string FalseLiteral = "false";

    private const string NullLiteral = "null";

    private static readonly CollectionCacheRepository<string> IgnoredPropertiesCache = new CollectionCacheRepository<string>();

    //
    // Summary:
    //     Serializes the specified object into a JSON string.
    //
    // Parameters:
    //   obj:
    //     The object.
    //
    //   format:
    //     if set to true it formats and indents the output.
    //
    //   typeSpecifier:
    //     The type specifier. Leave null or empty to avoid setting.
    //
    //   includeNonPublic:
    //     if set to true non-public getters will be also read.
    //
    //   includedNames:
    //     The included property names.
    //
    //   excludedNames:
    //     The excluded property names.
    //
    // Returns:
    //     A System.String that represents the current object.
    public static string Serialize(object? obj, bool format = false, string? typeSpecifier = null, bool includeNonPublic = false, string[]? includedNames = null, params string[] excludedNames)
    {
        return Serialize(obj, format, typeSpecifier, includeNonPublic, includedNames, excludedNames, null, JsonSerializerCase.None);
    }

    //
    // Summary:
    //     Serializes the specified object into a JSON string.
    //
    // Parameters:
    //   obj:
    //     The object.
    //
    //   jsonSerializerCase:
    //     The json serializer case.
    //
    //   format:
    //     if set to true [format].
    //
    //   typeSpecifier:
    //     The type specifier.
    //
    // Returns:
    //     A System.String that represents the current object.
    public static string Serialize(object? obj, JsonSerializerCase jsonSerializerCase, bool format = false, string? typeSpecifier = null)
    {
        return Serialize(obj, format, typeSpecifier, includeNonPublic: false, null, null, null, jsonSerializerCase);
    }

    //
    // Summary:
    //     Serializes the specified object into a JSON string.
    //
    // Parameters:
    //   obj:
    //     The object.
    //
    //   format:
    //     if set to true it formats and indents the output.
    //
    //   typeSpecifier:
    //     The type specifier. Leave null or empty to avoid setting.
    //
    //   includeNonPublic:
    //     if set to true non-public getters will be also read.
    //
    //   includedNames:
    //     The included property names.
    //
    //   excludedNames:
    //     The excluded property names.
    //
    //   parentReferences:
    //     The parent references.
    //
    //   jsonSerializerCase:
    //     The json serializer case.
    //
    // Returns:
    //     A System.String that represents the current object.
    public static string Serialize(object? obj, bool format, string? typeSpecifier, bool includeNonPublic, string[]? includedNames, string[]? excludedNames, List<WeakReference>? parentReferences, JsonSerializerCase jsonSerializerCase)
    {
        if (obj != null && (obj is string || Definitions.AllBasicValueTypes.Contains(obj.GetType())))
        {
            return SerializePrimitiveValue(obj);
        }

        SerializerOptions options = new SerializerOptions(format, typeSpecifier, includedNames, Serializer.GetExcludedNames(obj?.GetType(), excludedNames), includeNonPublic, parentReferences, jsonSerializerCase);
        return Serializer.Serialize(obj, 0, options, excludedNames);
    }

    //
    // Summary:
    //     Serializes the specified object using the SerializerOptions provided.
    //
    // Parameters:
    //   obj:
    //     The object.
    //
    //   options:
    //     The options.
    //
    // Returns:
    //     A System.String that represents the current object.
    public static string Serialize(object? obj, SerializerOptions options)
    {
        return Serializer.Serialize(obj, 0, options);
    }

    //
    // Summary:
    //     Serializes the specified object only including the specified property names.
    //
    //
    // Parameters:
    //   obj:
    //     The object.
    //
    //   format:
    //     if set to true it formats and indents the output.
    //
    //   includeNames:
    //     The include names.
    //
    // Returns:
    //     A System.String that represents the current object.
    public static string SerializeOnly(object? obj, bool format, params string[] includeNames)
    {
        return Serialize(obj, new SerializerOptions(format, null, includeNames));
    }

    //
    // Summary:
    //     Serializes the specified object excluding the specified property names.
    //
    // Parameters:
    //   obj:
    //     The object.
    //
    //   format:
    //     if set to true it formats and indents the output.
    //
    //   excludeNames:
    //     The exclude names.
    //
    // Returns:
    //     A System.String that represents the current object.
    public static string SerializeExcluding(object? obj, bool format, params string[] excludeNames)
    {
        return Serializer.Serialize(obj, 0, new SerializerOptions(format, null, null), excludeNames);
    }

    //
    // Summary:
    //     Deserializes the specified json string as either a Dictionary[string, object]
    //     or as a List[object] depending on the syntax of the JSON string.
    //
    // Parameters:
    //   json:
    //     The JSON string.
    //
    //   jsonSerializerCase:
    //     The json serializer case.
    //
    // Returns:
    //     Type of the current deserializes.
    public static object? Deserialize(string json, JsonSerializerCase jsonSerializerCase)
    {
        if (json != null)
        {
            return Converter.FromJsonResult(Deserializer.DeserializeInternal(json), jsonSerializerCase);
        }

        throw new ArgumentNullException("json");
    }

    //
    // Summary:
    //     Deserializes the specified json string as either a Dictionary[string, object]
    //     or as a List[object] depending on the syntax of the JSON string.
    //
    // Parameters:
    //   json:
    //     The JSON string.
    //
    // Returns:
    //     Type of the current deserializes.
    public static object? Deserialize(string json)
    {
        if (json != null)
        {
            return Deserialize(json, JsonSerializerCase.None);
        }

        throw new ArgumentNullException("json");
    }

    //
    // Summary:
    //     Deserializes the specified JSON string and converts it to the specified object
    //     type. Non-public constructors and property setters are ignored.
    //
    // Parameters:
    //   json:
    //     The JSON string.
    //
    //   jsonSerializerCase:
    //     The JSON serializer case.
    //
    // Type parameters:
    //   T:
    //     The type of object to deserialize.
    //
    // Returns:
    //     The deserialized specified type object.
    public static T Deserialize<T>(string json, JsonSerializerCase jsonSerializerCase = JsonSerializerCase.None)
    {
        if (json != null)
        {
            return (T)Deserialize(json, typeof(T), includeNonPublic: false, jsonSerializerCase);
        }

        throw new ArgumentNullException("json");
    }

    //
    // Summary:
    //     Deserializes the specified JSON string and converts it to the specified object
    //     type.
    //
    // Parameters:
    //   json:
    //     The JSON string.
    //
    //   includeNonPublic:
    //     if set to true, it also uses the non-public constructors and property setters.
    //
    //
    // Type parameters:
    //   T:
    //     The type of object to deserialize.
    //
    // Returns:
    //     The deserialized specified type object.
    public static T Deserialize<T>(string json, bool includeNonPublic)
    {
        if (json != null)
        {
            return (T)Deserialize(json, typeof(T), includeNonPublic);
        }

        throw new ArgumentNullException("json");
    }

    //
    // Summary:
    //     Deserializes the specified JSON string and converts it to the specified object
    //     type.
    //
    // Parameters:
    //   json:
    //     The JSON string.
    //
    //   resultType:
    //     Type of the result.
    //
    //   includeNonPublic:
    //     if set to true, it also uses the non-public constructors and property setters.
    //
    //
    //   jsonSerializerCase:
    //     The json serializer case.
    //
    // Returns:
    //     Type of the current conversion from json result.
    public static object? Deserialize(string json, Type resultType, bool includeNonPublic = false, JsonSerializerCase jsonSerializerCase = JsonSerializerCase.None)
    {
        if (json != null)
        {
            return Converter.FromJsonResult(Deserializer.DeserializeInternal(json), jsonSerializerCase, resultType, includeNonPublic);
        }

        throw new ArgumentNullException("json");
    }

    private static string SerializePrimitiveValue(object obj)
    {
        if (!(obj is string text))
        {
            if (obj is bool)
            {
                return ((bool)obj) ? "true" : "false";
            }

            return obj.ToString();
        }

        return "\"" + text + "\"";
    }
}