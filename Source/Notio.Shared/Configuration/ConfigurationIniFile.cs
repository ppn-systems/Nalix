using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Notio.Shared.Configuration;

/// <summary>
/// Một lớp bao bọc để đọc và ghi các tệp ini.
/// </summary>
internal sealed class ConfigurationIniFile
{
    private readonly string _path;
    private readonly Dictionary<string, Dictionary<string, string>> _iniData;

    /// <summary>
    /// Kiểm tra xem tệp có tồn tại tại đường dẫn được cung cấp hay không.
    /// </summary>
    public bool ExistsFile => File.Exists(_path);

    /// <summary>
    /// Khởi tạo một phiên bản mới của <see cref="ConfigurationIniFile"/> cho đường dẫn được chỉ định.
    /// </summary>
    /// <param name="path">Đường dẫn tới tệp ini.</param>
    public ConfigurationIniFile(string path)
    {
        _path = path;
        _iniData = [];
        Load();
    }

    /// <summary>
    /// Đọc dữ liệu từ tệp ini vào bộ nhớ.
    /// </summary>
    private void Load()
    {
        if (!ExistsFile) return;

        var currentSection = string.Empty;

        foreach (var line in File.ReadLines(_path))
        {
            var trimmedLine = line.Trim();

            // Bỏ qua dòng trống hoặc chú thích
            if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith(';'))
                continue;

            // Kiểm tra xem có phải là phần (section) không
            if (trimmedLine.StartsWith('[') && trimmedLine.EndsWith(']'))
            {
                currentSection = trimmedLine[1..^1].Trim();
                if (!_iniData.ContainsKey(currentSection))
                {
                    _iniData[currentSection] = [];
                }
            }
            else
            {
                // Nếu không phải là phần, giả sử đây là cặp khóa-giá trị
                var keyValue = trimmedLine.Split(['='], 2);

                if (keyValue.Length == 2)
                {
                    var key = keyValue[0].Trim();
                    var value = keyValue[1].Trim();

                    _iniData[currentSection][key] = value;
                }
            }
        }
    }

    /// <summary>
    /// Ghi một giá trị vào tệp ini nếu nó chưa tồn tại.
    /// </summary>
    /// <param name="section">Tên phần trong tệp ini.</param>
    /// <param name="key">Tên khóa trong phần.</param>
    /// <param name="value">Giá trị cần ghi.</param>
    public void WriteValue(string section, string key, object value)
    {
        if (!_iniData.TryGetValue(section, out Dictionary<string, string>? sectionData))
        {
            sectionData = [];
            _iniData[section] = sectionData;
        }

        // Chỉ ghi nếu giá trị chưa tồn tại
        if (!sectionData.ContainsKey(key))
        {
            sectionData[key] = value.ToString() ?? string.Empty;
            WriteFile();  // Ghi lại toàn bộ dữ liệu vào tệp
        }
    }

    public char? GetChar(string section, string key)
    {
        var stringValue = GetString(section, key);
        return stringValue != null && stringValue.Length == 1 ? stringValue[0] : (char?)null;
    }

    /// <summary>
    /// Lấy giá trị có khóa được chỉ định từ phần được chỉ định của <see cref="ConfigurationIniFile"/> này dưới dạng <see cref="string"/>.
    /// </summary>
    /// <param name="section">Tên phần trong tệp ini.</param>
    /// <param name="key">Tên khóa trong phần.</param>
    /// <returns>Giá trị dưới dạng chuỗi.</returns>
    public string GetString(string section, string key)
    {
        return _iniData.TryGetValue(section, out Dictionary<string, string>? value) && value.TryGetValue(key, out string? values)
            ? values : string.Empty;
    }

    /// <summary>
    /// Lấy giá trị có khóa được chỉ định từ phần được chỉ định của <see cref="ConfigurationIniFile"/> này dưới dạng <see cref="bool"/>.
    /// </summary>
    /// <param name="section">Tên phần trong tệp ini.</param>
    /// <param name="key">Tên khóa trong phần.</param>
    /// <returns>Giá trị dưới dạng boolean hoặc null nếu không thể chuyển đổi.</returns>
    public bool? GetBool(string section, string key)
    {
        var stringValue = GetString(section, key);
        return stringValue != null && bool.TryParse(stringValue, out bool parsedValue) ? parsedValue : null;
    }

    public decimal? GetDecimal(string section, string key)
    {
        var stringValue = GetString(section, key);
        return stringValue != null && decimal.TryParse(stringValue, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsedValue)
            ? parsedValue
            : null;
    }

    public byte? GetByte(string section, string key)
    {
        var stringValue = GetString(section, key);
        return stringValue != null && byte.TryParse(stringValue, out byte parsedValue)
            ? parsedValue
            : null;
    }

    public sbyte? GetSByte(string section, string key)
    {
        var stringValue = GetString(section, key);
        return stringValue != null && sbyte.TryParse(stringValue, out sbyte parsedValue)
            ? parsedValue
            : null;
    }

    public short? GetInt16(string section, string key)
    {
        var stringValue = GetString(section, key);
        return stringValue != null && short.TryParse(stringValue, out short parsedValue)
            ? parsedValue
            : null;
    }

    public ushort? GetUInt16(string section, string key)
    {
        var stringValue = GetString(section, key);
        return stringValue != null && ushort.TryParse(stringValue, out ushort parsedValue)
            ? parsedValue
            : null;
    }

    /// <summary>
    /// Lấy giá trị có khóa được chỉ định từ phần được chỉ định của <see cref="ConfigurationIniFile"/> này dưới dạng <see cref="int"/>.
    /// </summary>
    /// <param name="section">Tên phần trong tệp ini.</param>
    /// <param name="key">Tên khóa trong phần.</param>
    /// <returns>Giá trị dưới dạng int hoặc null nếu không thể chuyển đổi.</returns>
    public int? GetInt32(string section, string key)
    {
        var stringValue = GetString(section, key);
        return stringValue != null && int.TryParse(stringValue, out int parsedValue) ? parsedValue : null;
    }

    /// <summary>
    /// Lấy giá trị có khóa được chỉ định từ phần được chỉ định của <see cref="ConfigurationIniFile"/> này dưới dạng <see cref="uint"/>.
    /// </summary>
    /// <param name="section">Tên phần trong tệp ini.</param>
    /// <param name="key">Tên khóa trong phần.</param>
    /// <returns>Giá trị dưới dạng uint hoặc null nếu không thể chuyển đổi.</returns>
    public uint? GetUInt32(string section, string key)
    {
        var stringValue = GetString(section, key);
        return stringValue != null && uint.TryParse(stringValue, out uint parsedValue) ? parsedValue : null;
    }

    /// <summary>
    /// Lấy giá trị có khóa được chỉ định từ phần được chỉ định của <see cref="ConfigurationIniFile"/> này dưới dạng <see cref="long"/>.
    /// </summary>
    /// <param name="section">Tên phần trong tệp ini.</param>
    /// <param name="key">Tên khóa trong phần.</param>
    /// <returns>Giá trị dưới dạng long hoặc null nếu không thể chuyển đổi.</returns>
    public long? GetInt64(string section, string key)
    {
        var stringValue = GetString(section, key);
        return stringValue != null && long.TryParse(stringValue, out long parsedValue) ? parsedValue : null;
    }

    /// <summary>
    /// Lấy giá trị có khóa được chỉ định từ phần được chỉ định của <see cref="ConfigurationIniFile"/> này dưới dạng <see cref="ulong"/>.
    /// </summary>
    /// <param name="section">Tên phần trong tệp ini.</param>
    /// <param name="key">Tên khóa trong phần.</param>
    /// <returns>Giá trị dưới dạng ulong hoặc null nếu không thể chuyển đổi.</returns>
    public ulong? GetUInt64(string section, string key)
    {
        var stringValue = GetString(section, key);
        return stringValue != null && ulong.TryParse(stringValue, out ulong parsedValue) ? parsedValue : null;
    }

    /// <summary>
    /// Lấy giá trị có khóa được chỉ định từ phần được chỉ định của <see cref="ConfigurationIniFile"/> này dưới dạng <see cref="float"/>.
    /// </summary>
    /// <param name="section">Tên phần trong tệp ini.</param>
    /// <param name="key">Tên khóa trong phần.</param>
    /// <returns>Giá trị dưới dạng float hoặc null nếu không thể chuyển đổi.</returns>
    public float? GetSingle(string section, string key)
    {
        var stringValue = GetString(section, key);
        return stringValue != null && float.TryParse(stringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedValue)
            ? parsedValue
            : null;
    }

    /// <summary>
    /// Lấy giá trị có khóa được chỉ định từ phần được chỉ định của <see cref="ConfigurationIniFile"/> này dưới dạng <see cref="double"/>.
    /// </summary>
    /// <param name="section">Tên phần trong tệp ini.</param>
    /// <param name="key">Tên khóa trong phần.</param>
    /// <returns>Giá trị dưới dạng double hoặc null nếu không thể chuyển đổi.</returns>
    public double? GetDouble(string section, string key)
    {
        var stringValue = GetString(section, key);
        return stringValue != null && double.TryParse(stringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedValue)
            ? parsedValue
            : null;
    }

    /// <summary>
    /// Lấy giá trị có khóa được chỉ định từ phần được chỉ định của <see cref="ConfigurationIniFile"/> này dưới dạng <see cref="DateTime"/>.
    /// </summary>
    /// <param name="section">Tên phần trong tệp ini.</param>
    /// <param name="key">Tên khóa trong phần.</param>
    /// <returns>Giá trị dưới dạng DateTime hoặc null nếu không thể chuyển đổi.</returns>
    public DateTime? GetDateTime(string section, string key)
    {
        var stringValue = GetString(section, key);
        return stringValue != null && DateTime.TryParse(stringValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedValue)
            ? parsedValue
            : null;
    }

    /// <summary>
    /// Ghi lại nội dung của tệp ini vào tệp đích.
    /// </summary>
    private void WriteFile()
    {
        if (_iniData == null || _iniData.Count == 0)
            return;

        using var writer = new StreamWriter(_path);
        foreach (var section in _iniData)
        {
            writer.WriteLine($"[{section.Key}]");

            foreach (var keyValue in section.Value)
            {
                writer.WriteLine($"{keyValue.Key}={keyValue.Value}");
            }

            writer.WriteLine();  // Dòng trống giữa các section
        }
    }
}