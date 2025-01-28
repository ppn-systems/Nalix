using System;
using System.Collections.Generic;
using System.IO;

namespace Notio.Shared.Configuration;

/// <summary>
/// Một singleton cung cấp quyền truy cập vào các container giá trị cấu hình.
/// </summary>
public sealed class ConfiguredShared : SingletonBase<ConfiguredShared>
{
    private readonly Dictionary<Type, ConfiguredBinder> _configContainerDict = [];
    private readonly ConfiguredIniFile _iniFile;

    /// <summary>
    /// Khởi tạo một instance của <see cref="ConfiguredShared"/>.
    /// </summary>
    private ConfiguredShared()
        => _iniFile = new(Path.Combine(DefaultDirectories.ConfigPath, "Configured.ini"));

    /// <summary>
    /// Khởi tạo nếu cần và trả về <typeparamref name="TClass"/>.
    /// </summary>
    /// <typeparam name="TClass">Kiểu của container cấu hình.</typeparam>
    /// <returns>Instance của kiểu <typeparamref name="TClass"/>.</returns>
    public TClass Get<TClass>() where TClass : ConfiguredBinder, new()
    {
        if (!_configContainerDict.TryGetValue(typeof(TClass), out ConfiguredBinder? container))
        {
            container = new TClass();
            container.Initialize(_iniFile);

            _configContainerDict.Add(typeof(TClass), container);
        }

        return (TClass)container;
    }
}