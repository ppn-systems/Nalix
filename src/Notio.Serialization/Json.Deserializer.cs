using Notio.Serialization.Internal.Extensions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Notio.Serialization;

/// <summary>
/// A very simple, light-weight JSON library written by Mario
/// to teach Geo how things are done.
///
/// This helper is useful for small tasks but does not represent a full-featured
/// serializer such as Serialization.NET.
/// </summary>
public static partial class Json
{
    /// <summary>
    /// A simple JSON Deserializer.
    /// </summary>
    private class Deserializer
    {
        private readonly string _json;
        private int _index;
        private ReadState _state = ReadState.WaitingForRootOpen;
        private string? _currentFieldName;

        // The final result – either a Dictionary for objects or a List for arrays.
        private readonly object? _result;

        // Will hold object data when parsing JSON objects.
        private Dictionary<string, object?>? _resultObject;

        // Will hold array data when parsing JSON arrays.
        private List<object?>? _resultArray;

        private Deserializer(string json, int startIndex)
        {
            _json = json;
            _index = startIndex;
            int len = _json.Length;

            _resultObject = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            _resultArray = [];

            while (_index < len)
            {
                // Switch based on the current state.
                switch (_state)
                {
                    case ReadState.WaitingForRootOpen:
                        WaitForRootOpen();
                        break;

                    case ReadState.WaitingForField:
                        {
                            // Skip white space.
                            if (char.IsWhiteSpace(_json, _index))
                            {
                                _index++;
                                continue;
                            }
                            // Check for closing object or array.
                            if ((_resultObject != null && _json[_index] == CloseObjectChar) ||
                                (_resultArray != null && _json[_index] == CloseArrayChar))
                            {
                                _result = _resultObject ?? (object?)_resultArray;
                                return;
                            }
                            // Field names must start with a quoted string.
                            if (_json[_index] != StringQuotedChar)
                                throw CreateParserException($"'{StringQuotedChar}'");
                            // Get field name.
                            int charCount = GetFieldNameCount();
                            _currentFieldName = Unescape(_json.SliceLength(_index + 1, charCount));
                            _index += charCount + 1; // Skip over the field name closing quote.
                            _state = ReadState.WaitingForColon;
                        }
                        break;

                    case ReadState.WaitingForColon:
                        {
                            if (char.IsWhiteSpace(_json, _index))
                            {
                                _index++;
                                continue;
                            }
                            if (_json[_index] != ValueSeparatorChar)
                                throw CreateParserException($"'{ValueSeparatorChar}'");
                            _state = ReadState.WaitingForValue;
                            _index++; // Consume the colon.
                        }
                        break;

                    case ReadState.WaitingForValue:
                        {
                            if (char.IsWhiteSpace(_json, _index))
                            {
                                _index++;
                                continue;
                            }
                            // Check for empty object/array.
                            if ((_resultObject != null && _json[_index] == CloseObjectChar) ||
                                (_resultArray != null && _json[_index] == CloseArrayChar))
                            {
                                _result = _resultObject ?? (object?)_resultArray;
                                return;
                            }
                            // Extract the value.
                            ExtractValue();
                            _state = ReadState.WaitingForNextOrRootClose;
                        }
                        break;

                    case ReadState.WaitingForNextOrRootClose:
                        {
                            if (char.IsWhiteSpace(_json, _index))
                            {
                                _index++;
                                continue;
                            }
                            // If we encounter a comma, move to the next field/value.
                            if (_json[_index] == FieldSeparatorChar)
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
                                _index++; // Consume the comma.
                                continue;
                            }
                            // If we see a closing brace/bracket, finish.
                            if ((_resultObject != null && _json[_index] == CloseObjectChar) ||
                                (_resultArray != null && _json[_index] == CloseArrayChar))
                            {
                                _result = _resultObject ?? (object?)_resultArray;
                                return;
                            }
                            throw CreateParserException($"'{FieldSeparatorChar}', '{CloseObjectChar}' or '{CloseArrayChar}'");
                        }
                }
            }
        }

        internal static object? DeserializeInternal(string json) =>
            new Deserializer(json, 0)._result;

        private void WaitForRootOpen()
        {
            // Skip any whitespace.
            while (_index < _json.Length && char.IsWhiteSpace(_json, _index))
                _index++;

            if (_index >= _json.Length)
                throw CreateParserException($"'{OpenObjectChar}' or '{OpenArrayChar}'");

            char ch = _json[_index];
            if (ch == OpenObjectChar)
            {
                _resultObject = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                _state = ReadState.WaitingForField;
                _index++; // Consume the opening brace.
            }
            else if (ch == OpenArrayChar)
            {
                _resultArray = [];
                _state = ReadState.WaitingForValue;
                _index++; // Consume the opening bracket.
            }
            else
            {
                throw CreateParserException($"'{OpenObjectChar}' or '{OpenArrayChar}'");
            }
        }

        private void ExtractValue()
        {
            // Determine the value type based on the current character.
            char current = _json[_index];
            if (current == StringQuotedChar)
            {
                ExtractStringQuoted();
            }
            else if (current == OpenObjectChar || current == OpenArrayChar)
            {
                ExtractObject();
            }
            else if (current == 't') // true
            {
                ExtractConstant(TrueLiteral, true);
            }
            else if (current == 'f') // false
            {
                ExtractConstant(FalseLiteral, false);
            }
            else if (current == 'n') // null
            {
                ExtractConstant(NullLiteral);
            }
            else
            {
                ExtractNumber();
            }
            _currentFieldName = null;
        }

        private static string Unescape(string str)
        {
            int escapeIndex = str.IndexOf(StringEscapeChar);
            if (escapeIndex < 0)
                return str; // Không có escape, return ngay

            var builder = new StringBuilder(str.Length);
            builder.Append(str.AsSpan(0, escapeIndex));

            for (int i = escapeIndex; i < str.Length; i++)
            {
                char c = str[i];
                if (c == StringEscapeChar && i + 1 < str.Length)
                {
                    char next = str[++i];
                    switch (next)
                    {
                        case 'b': builder.Append('\b'); break;
                        case 't': builder.Append('\t'); break;
                        case 'n': builder.Append('\n'); break;
                        case 'f': builder.Append('\f'); break;
                        case 'r': builder.Append('\r'); break;
                        case 'u': i = ExtractEscapeSequence(str, i, builder); break;
                        default: builder.Append(next); break;
                    }
                }
                else
                {
                    builder.Append(c);
                }
            }
            return builder.ToString();
        }

        private static int ExtractEscapeSequence(string str, int i, StringBuilder builder)
        {
            // Kiểm tra đủ 4 ký tự sau '\u'
            if (i + 5 >= str.Length)
                throw new FormatException($"Invalid Unicode escape sequence at index {i}");

            int code = 0;
            for (int j = i + 2; j <= i + 5; j++)
            {
                if (!Uri.IsHexDigit(str[j])) // Check ký tự có phải hex hợp lệ không
                    throw new FormatException($"Invalid hex digit '{str[j]}' in Unicode escape sequence.");
                code = code * 16 + HexValue(str[j]);
            }
            builder.Append((char)code);
            return i + 5;
        }

        private static int HexValue(char c)
        {
            if (c >= '0' && c <= '9')
                return c - '0';
            if (c >= 'a' && c <= 'f')
                return c - 'a' + 10;
            if (c >= 'A' && c <= 'F')
                return c - 'A' + 10;
            throw new FormatException($"Invalid hex digit: {c}");
        }

        private int GetFieldNameCount()
        {
            int count = 0;
            for (int j = _index + 1; j < _json.Length; j++)
            {
                if (_json[j] == StringQuotedChar && _json[j - 1] != StringEscapeChar)
                    break;
                count++;
            }
            return count;
        }

        private void ExtractObject()
        {
            // Recursively parse an object/array.
            var innerDeserializer = new Deserializer(_json, _index);
            if (_currentFieldName != null && _resultObject != null)
            {
                _resultObject[_currentFieldName] = innerDeserializer._result;
            }
            else
            {
                _resultArray?.Add(innerDeserializer._result);
            }
            _index = innerDeserializer._index;
        }

        private void ExtractNumber()
        {
            int start = _index;

            // Kiểm tra số âm
            if (_json[_index] == '-')
                _index++;

            while (_index < _json.Length &&
                  (char.IsDigit(_json[_index]) || _json[_index] == '.' || _json[_index] == 'e' || _json[_index] == 'E'))
            {
                _index++;
            }

            string numberStr = _json.SliceLength(start, _index - start);
            if (!decimal.TryParse(numberStr, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal number))
                throw CreateParserException("[number]");

            if (_currentFieldName != null && _resultObject != null)
                _resultObject[_currentFieldName] = number;
            else
                _resultArray?.Add(number);

            _index--;
        }

        private void ExtractConstant(string literal, bool? constantValue = null)
        {
            if (_index + literal.Length > _json.Length || _json.SliceLength(_index, literal.Length) != literal)
                throw CreateParserException($"'{literal}'");

            if (_currentFieldName != null && _resultObject != null)
            {
                _resultObject[_currentFieldName] = constantValue;
            }
            else
            {
                _resultArray?.Add(constantValue);
            }
            _index += literal.Length - 1;
        }

        private void ExtractStringQuoted()
        {
            int start = _index + 1;
            int count = 0;
            bool escapeFound = false;
            for (int j = start; j < _json.Length; j++)
            {
                if (_json[j] == StringQuotedChar && !escapeFound)
                    break;
                escapeFound = _json[j] == StringEscapeChar && !escapeFound;
                count++;
            }
            string value = Unescape(_json.SliceLength(start, count));
            if (_currentFieldName != null && _resultObject != null)
            {
                _resultObject[_currentFieldName] = value;
            }
            else
            {
                _resultArray?.Add(value);
            }
            _index += count + 1;
        }

        private FormatException CreateParserException(string expected)
        {
            var (line, col) = _json.TextPositionAt(_index);
            return new FormatException($"Parser error (Line {line}, Col {col}, State {_state}): Expected {expected} but got '{_json[_index]}'.");
        }

        private enum ReadState
        {
            WaitingForRootOpen,
            WaitingForField,
            WaitingForColon,
            WaitingForValue,
            WaitingForNextOrRootClose,
        }

        // Constants (assumed defined elsewhere or can be defined here)
        private const char OpenObjectChar = '{';

        private const char CloseObjectChar = '}';
        private const char OpenArrayChar = '[';
        private const char CloseArrayChar = ']';
        private const char StringQuotedChar = '"';
        private const char StringEscapeChar = '\\';
        private const char FieldSeparatorChar = ',';
        private const char ValueSeparatorChar = ':';
        private const string TrueLiteral = "true";
        private const string FalseLiteral = "false";
        private const string NullLiteral = "null";
    }
}
