using System;
using System.Threading;

namespace Nalix.Network.Web.Internal;

/// <summary>
/// An abstract base class that provides configuration locking and synchronization mechanisms.
/// </summary>
public abstract class ConfiguredObject
{
    private readonly Lock _syncRoot = new();

    private bool _configurationLocked;

    /// <summary>
    /// Gets a value indicating whether the configuration is locked.
    /// </summary>
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

    /// <summary>
    /// Locks the configuration for the object. Once locked, further modifications are prevented.
    /// </summary>
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

    /// <summary>
    /// Invoked before locking the configuration, allowing for additional logic if needed.
    /// </summary>
    protected virtual void OnBeforeLockConfiguration()
    {
        // This can be overridden by derived classes to add custom behavior before locking
    }

    /// <summary>
    /// Ensures that the configuration is not locked. Throws an exception if the configuration is locked.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the configuration is already locked.
    /// </exception>
    protected void EnsureConfigurationNotLocked()
    {
        if (ConfigurationLocked)
        {
            throw new InvalidOperationException("Configuration of this " + GetType().Name + " instance is locked.");
        }
    }
}
