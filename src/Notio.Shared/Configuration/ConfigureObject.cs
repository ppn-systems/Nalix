using System;
using System.Threading;

namespace Notio.Shared.Configuration;

public abstract class ConfiguredObject
{
    private readonly Lock _syncRoot = new();

    private bool _configurationLocked;

    protected bool ConfigurationLocked
    {
        get
        {
            lock (_syncRoot)
            {
                return _configurationLocked;
            }
        }
    }

    protected void LockConfiguration()
    {
        lock (_syncRoot)
        {
            if (!_configurationLocked)
            {
                OnBeforeLockConfiguration();
                _configurationLocked = true;
            }
        }
    }

    protected virtual void OnBeforeLockConfiguration()
    {
    }

    protected void EnsureConfigurationNotLocked()
    {
        if (ConfigurationLocked)
        {
            throw new InvalidOperationException("Configuration of this " + GetType().Name + " instance is locked.");
        }
    }
}