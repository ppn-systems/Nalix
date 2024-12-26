using System;
using System.Reflection;

namespace Notio.Shared.Configuration;

/// <summary>
/// Cung cấp quyền truy cập vào các giá trị cấu hình.
/// </summary>
/// <remarks>
/// Các lớp triển khai lớp này nên có hậu tố Configuration trong tên của chúng (ví dụ, FooConfig). Tên phần và khóa của tệp ini được lấy từ tên lớp và thuộc tính.
/// </remarks>
public abstract class ConfigContainer
{
    /// <summary>
    /// Khởi tạo một phiên bản <see cref="ConfigContainer"/> từ <see cref="ConfigIniFile"/> được cung cấp bằng cách sử dụng reflection.
    /// </summary>
    /// <param name="configFile">Tệp cấu hình ini được sử dụng để khởi tạo.</param>
    internal void Initialize(ConfigIniFile configFile)
    {
        Type type = GetType();  // Sử dụng reflection để lấy cấu hình

        string section = type.Name;
        if (section.EndsWith("Configuration", StringComparison.OrdinalIgnoreCase))
            section = section[..^6];

        foreach (var property in type.GetProperties())
        {
            if (property.IsDefined(typeof(ConfigIgnoreAttribute))) continue;  // Bỏ qua các thuộc tính được chỉ định

            object? value = Type.GetTypeCode(property.PropertyType) switch
            {
                TypeCode.String => configFile.GetString(section, property.Name),
                TypeCode.Boolean => configFile.GetBool(section, property.Name),
                TypeCode.Int32 => configFile.GetInt32(section, property.Name),
                TypeCode.UInt32 => configFile.GetUInt32(section, property.Name),
                TypeCode.Int64 => configFile.GetInt64(section, property.Name),
                TypeCode.UInt64 => configFile.GetUInt64(section, property.Name),
                TypeCode.Single => configFile.GetSingle(section, property.Name),
                _ => throw new NotImplementedException($"Value type {property.PropertyType} is not supported for configuration files."),
            };

            configFile.WriteValue(section, property.Name, property.GetValue(this)?.ToString() ?? "");

            // Kiểm tra nếu giá trị null hoặc là chuỗi rỗng
            if (value == null || value is string strValue && strValue == string.Empty) continue;

            property.SetValue(this, value);  // Gán giá trị đọc được từ tệp cấu hình vào thuộc tính
        }
    }
}