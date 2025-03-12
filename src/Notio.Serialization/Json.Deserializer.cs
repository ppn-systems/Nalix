using Notio.Serialization.Internal.Extensions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Notio.Serialization;

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
        private object? _result;
        private Dictionary<string, object?>? _resultObject;
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
                switch (_state)
                {
                    case ReadState.WaitingForRootOpen:
                        WaitForRootOpen();
                        break;

                    case ReadState.WaitingForField:
                        ProcessFieldName();
                        break;

                    case ReadState.WaitingForColon:
                        ProcessColon();
                        break;

                    case ReadState.WaitingForValue:
                        ProcessValue();
                        break;

                    case ReadState.WaitingForNextOrRootClose:
                        ProcessNextOrClose();
                        break;
                }
            }
        }

        internal static object? DeserializeInternal(string json) =>
            new Deserializer(json, 0)._result;

        private void WaitForRootOpen()
        {
            // Skip whitespace
            SkipWhitespace();

            if (_index >= _json.Length)
                throw CreateParserException($"'{OpenObjectChar}' or '{OpenArrayChar}'");

            char ch = _json[_index];
            if (ch == OpenObjectChar)
            {
                _resultObject = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                _state = ReadState.WaitingForField;
            }
            else if (ch == OpenArrayChar)
            {
                _resultArray = [];
                _state = ReadState.WaitingForValue;
            }
            else
            {
                throw CreateParserException($"'{OpenObjectChar}' or '{OpenArrayChar}'");
            }
            _index++; // Consume the opening character
        }

        private void ProcessFieldName()
        {
            // Skip whitespace
            SkipWhitespace();

            // Check for end of object
            if (_json[_index] == CloseObjectChar)
            {
                _result = _resultObject;
                _index++; // Consume closing brace
                return;
            }

            // Field name must start with a quote
            if (_json[_index] != StringQuotedChar)
                throw CreateParserException($"'{StringQuotedChar}'");

            // Get field name
            int charCount = GetFieldNameCount();
            _currentFieldName = Unescape(_json.SliceLength(_index + 1, charCount));
            _index += charCount + 2; // Skip field name and closing quote
            _state = ReadState.WaitingForColon;
        }

        private void ProcessColon()
        {
            SkipWhitespace();

            if (_json[_index] != ValueSeparatorChar)
                throw CreateParserException($"'{ValueSeparatorChar}'");

            _state = ReadState.WaitingForValue;
            _index++; // Consume colon
        }

        private void ProcessValue()
        {
            SkipWhitespace();

            // Handle empty object/array case
            if ((_resultObject != null && _json[_index] == CloseObjectChar) ||
                (_resultArray != null && _json[_index] == CloseArrayChar))
            {
                _result = _resultObject ?? (object?)_resultArray;
                _index++; // Consume closing character
                return;
            }

            ExtractValue();
            _state = ReadState.WaitingForNextOrRootClose;
        }

        private void ProcessNextOrClose()
        {
            SkipWhitespace();

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
                _index++; // Consume comma
            }
            else if ((_resultObject != null && _json[_index] == CloseObjectChar) ||
                    (_resultArray != null && _json[_index] == CloseArrayChar))
            {
                _result = _resultObject ?? (object?)_resultArray;
                _index++; // Consume closing character
                return;
            }
            else
            {
                throw CreateParserException($"'{FieldSeparatorChar}', '{CloseObjectChar}' or '{CloseArrayChar}'");
            }
        }

        private void SkipWhitespace()
        {
            while (_index < _json.Length && char.IsWhiteSpace(_json, _index))
                _index++;
        }

        private static string Unescape(string str)
        {
            // Quick check - if no escape characters, return the original string
            int escapeIndex = str.IndexOf(StringEscapeChar);
            if (escapeIndex < 0)
                return str;

            var builder = new StringBuilder(str.Length);
            builder.Append(str, 0, escapeIndex);

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
            if (i + 4 >= str.Length)
                throw new FormatException($"Invalid Unicode escape sequence at index {i}");

            int code = 0;
            for (int j = i + 1; j <= i + 4; j++)
            {
                if (!Uri.IsHexDigit(str[j]))
                    throw new FormatException($"Invalid hex digit '{str[j]}' in Unicode escape sequence");
                code = (code << 4) | HexValue(str[j]);
            }
            builder.Append((char)code);
            return i + 4;
        }

        private static int HexValue(char c) => c switch
        {
            >= '0' and <= '9' => c - '0',
            >= 'a' and <= 'f' => c - 'a' + 10,
            >= 'A' and <= 'F' => c - 'A' + 10,
            _ => throw new FormatException($"Invalid hex digit: {c}")
        };

        private int GetFieldNameCount()
        {
            int count = 0;
            bool escaped = false;
            for (int j = _index + 1; j < _json.Length; j++)
            {
                char c = _json[j];
                if (c == StringQuotedChar && !escaped)
                    break;
                escaped = c == StringEscapeChar && !escaped;
                count++;
            }
            return count;
        }

        private void ExtractValue()
        {
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

        private void ExtractObject()
        {
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

            // Check for negative number
            if (_json[_index] == '-')
                _index++;

            // Read all number characters including decimal point and exponent
            while (_index < _json.Length &&
                  (char.IsDigit(_json[_index]) || _json[_index] == '.' ||
                   _json[_index] == 'e' || _json[_index] == 'E' ||
                   (_json[_index] == '+' || _json[_index] == '-') &&
                   (_index > 0 && (_json[_index - 1] == 'e' || _json[_index - 1] == 'E'))))
            {
                _index++;
            }

            string numberStr = _json.SliceLength(start, _index - start);
            if (!decimal.TryParse(numberStr, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal number))
                throw CreateParserException("[number]");

            if (_currentFieldName != null && _resultObject != null)
                _resultObject[_currentFieldName] = number;
            else
                _resultArray?.Add(number);

            _index--; // Adjust index for the next iteration
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
            _index++; // Skip opening quote
            int start = _index;
            bool escaped = false;

            // Find the end of the string
            while (_index < _json.Length)
            {
                char c = _json[_index];
                if (c == StringQuotedChar && !escaped)
                    break;

                escaped = c == StringEscapeChar && !escaped;
                _index++;
            }

            if (_index >= _json.Length)
                throw CreateParserException("closing quote");

            string value = Unescape(_json.SliceLength(start, _index - start));

            if (_currentFieldName != null && _resultObject != null)
            {
                _resultObject[_currentFieldName] = value;
            }
            else
            {
                _resultArray?.Add(value);
            }

            _index++; // Skip closing quote
        }

        private FormatException CreateParserException(string expected)
        {
            var (line, col) = _json.TextPositionAt(_index);
            return new FormatException(
                $"Parser error (Line {line}, Col {col}, State {_state}): " +
                $"Expected {expected} but got '{(_index < _json.Length ? _json[_index] : "EOF")}'.");
        }

        private enum ReadState
        {
            WaitingForRootOpen,
            WaitingForField,
            WaitingForColon,
            WaitingForValue,
            WaitingForNextOrRootClose,
        }

        private const char StringEscapeChar = '\\';
    }
}
