using System;
using System.Collections.Generic;
using System.IO;

namespace Notio.Shared.Configuration;

/// <summary>
/// Một singleton cung cấp quyền truy cập vào các container giá trị cấu hình.
/// </summary>
public sealed class ConfigManager
{
    private readonly Dictionary<Type, ConfigContainer> _configContainerDict = [];
    private readonly ConfigIniFile _iniFile;

    /// <summary>
    /// Cung cấp quyền truy cập vào instance của <see cref="ConfigManager"/>.
    /// </summary>
    public static ConfigManager Instance { get; } = new();

    /// <summary>
    /// Khởi tạo một instance của <see cref="ConfigManager"/>.
    /// </summary>
    private ConfigManager()
        => _iniFile = new(Path.Combine(DefaultDirectories.ConfigPath, "Configuration.ini"));

    /// <summary>
    /// Khởi tạo nếu cần và trả về <typeparamref name="TClass"/>.
    /// </summary>
    /// <typeparam name="TClass">Kiểu của container cấu hình.</typeparam>
    /// <returns>Instance của kiểu <typeparamref name="TClass"/>.</returns>
    public TClass GetConfig<TClass>() where TClass : ConfigContainer, new()
    {
        if (!_configContainerDict.TryGetValue(typeof(TClass), out ConfigContainer? container))
        {
            container = new TClass();
            container.Initialize(_iniFile);

            _configContainerDict.Add(typeof(TClass), container);
        }

        return (TClass)container;
    }
}