using System;
using System.Collections.Generic;
using System.IO;

namespace Notio.Shared.Configuration;

/// <summary>
/// Một singleton cung cấp quyền truy cập vào các container giá trị cấu hình.
/// </summary>
public sealed class ConfigurationShared : SingletonInstance<ConfigurationShared>
{
    private readonly Dictionary<Type, ConfigurationBinder> _configContainerDict = [];
    private readonly ConfigurationIniFile _iniFile;

    /// <summary>
    /// Khởi tạo một instance của <see cref="ConfigurationShared"/>.
    /// </summary>
    private ConfigurationShared()
        => _iniFile = new(Path.Combine(DefaultDirectories.ConfigPath, "Configuration.ini"));

    /// <summary>
    /// Khởi tạo nếu cần và trả về <typeparamref name="TClass"/>.
    /// </summary>
    /// <typeparam name="TClass">Kiểu của container cấu hình.</typeparam>
    /// <returns>Instance của kiểu <typeparamref name="TClass"/>.</returns>
    public TClass Get<TClass>() where TClass : ConfigurationBinder, new()
    {
        if (!_configContainerDict.TryGetValue(typeof(TClass), out ConfigurationBinder? container))
        {
            container = new TClass();
            container.Initialize(_iniFile);

            _configContainerDict.Add(typeof(TClass), container);
        }

        return (TClass)container;
    }
}