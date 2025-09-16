# Configuration System Documentation

## Overview

The Configuration System provides a high-performance, thread-safe mechanism for managing application configuration through INI files. It consists of three main components:

1. `ConfiguredShared`: A singleton manager for configuration containers
2. `ConfiguredBinder`: A base class for configuration containers with property binding
3. `ConfiguredIgnoreAttribute`: An attribute to exclude properties from configuration binding

## ConfiguredShared

### Purpose

Manages configuration containers and provides access to the INI file system with optimized performance for high-throughput applications.

### Usage

```csharp
// Access the configuration manager
var config = ConfiguredShared.Instance;

// Get or create a configuration container
var myConfig = config.Get<MyAppConfig>();

// Reload all configurations
config.ReloadAll();
```

### Key Features

- Thread-safe operations
- Lazy loading of configuration files
- Caching of configuration containers
- Automatic directory creation
- Flush capabilities for persistence

### Example Implementation

```csharp
public class MyAppConfig : ConfiguredBinder
{
    public string ApplicationName { get; set; } = "DefaultApp";
    public int MaxConnections { get; set; } = 100;
    
    [ConfiguredIgnore]
    public string RuntimeValue { get; set; }
}

// Usage
var config = ConfiguredShared.Instance;
var appConfig = config.Get<MyAppConfig>();
Console.WriteLine(appConfig.ApplicationName);
```

## ConfiguredBinder

### Purpose

Base class for configuration containers that provides automatic property binding from INI files.

### Features

- High-performance reflection with caching
- Automatic type conversion
- Default value handling
- Property metadata caching
- Clone capability

### Supported Types

```csharp
// Supported property types:
- char
- byte, sbyte
- string
- boolean
- decimal
- short, ushort
- int, uint
- long, ulong
- float, double
- DateTime
```

### Example Configuration Class

```csharp
public class ServerConfig : ConfiguredBinder
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 8080;
    public bool EnableSsl { get; set; } = false;
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    
    [ConfiguredIgnore]
    public string RuntimeStatus { get; set; }
}
```

## Configuration File Structure

The system uses INI file format with sections derived from class names:

```ini
[Server]
Host=localhost
Port=8080
EnableSsl=false
StartTime=2025-03-02T12:59:08Z
```

## Best Practices

### 1. Configuration Class Design

```csharp
public class DatabaseConfig : ConfiguredBinder
{
    // Provide default values
    public string ConnectionString { get; set; } = "default_connection";
    
    // Use ConfiguredIgnore for runtime-only properties
    [ConfiguredIgnore]
    public int ActiveConnections { get; set; }
}
```

### 2. Thread-Safe Access

```csharp
public class ConfigurationManager
{
    private readonly DatabaseConfig _dbConfig;
    
    public ConfigurationManager()
    {
        _dbConfig = ConfiguredShared.Instance.Get<DatabaseConfig>();
    }
}
```

### 3. Handling Configuration Reloads

```csharp
public class Service
{
    public void ReloadConfiguration()
    {
        if (ConfiguredShared.Instance.ReloadAll())
        {
            // Configuration reloaded successfully
            OnConfigurationReloaded();
        }
    }
}
```

## Performance Considerations

1. **Caching**
   - Configuration containers are cached
   - Property metadata is cached
   - Section names are cached

2. **Lazy Loading**

   ```csharp
   // Configuration is only loaded when accessed
   private readonly Lazy<ConfiguredIniFile> _iniFile;
   ```

3. **Thread Synchronization**

   ```csharp
   // Using ReaderWriterLockSlim for optimal concurrent access
   private readonly ReaderWriterLockSlim _configLock = 
       new(LockRecursionPolicy.NoRecursion);
   ```

## Error Handling

### 1. File System Errors

```csharp
try
{
    var config = ConfiguredShared.Instance.Get<AppConfig>();
}
catch (InvalidOperationException ex)
{
    // Handle configuration directory creation failure
}
```

### 2. Configuration Loading Errors

```csharp
if (!ConfiguredShared.Instance.ReloadAll())
{
    // Handle reload failure
    // Previous configuration remains active
}
```

## Implementation Example

```csharp
public class ApplicationConfig : ConfiguredBinder
{
    public string Environment { get; set; } = "Development";
    public int MaxThreads { get; set; } = 4;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
    
    [ConfiguredIgnore]
    public string BuildVersion { get; set; }
}

public class ConfigurationService : IDisposable
{
    private readonly ConfiguredShared _configManager;
    private readonly ApplicationConfig _appConfig;
    
    public ConfigurationService()
    {
        _configManager = ConfiguredShared.Instance;
        _appConfig = _configManager.Get<ApplicationConfig>();
    }
    
    public void Reload()
    {
        if (_configManager.ReloadAll())
        {
            // Configuration updated
        }
    }
    
    public void Dispose()
    {
        _configManager.Flush();
        _configManager.Dispose();
    }
}
```

## Thread Safety Considerations

### 1. Reading Configuration

```csharp
// Thread-safe read access
public string GetEnvironment()
{
    return ConfiguredShared.Instance
        .Get<ApplicationConfig>()
        .Environment;
}
```

### 2. Reloading Configuration

```csharp
// Thread-safe reload with lock
public void ReloadConfig()
{
    if (ConfiguredShared.Instance.ReloadAll())
    {
        // Configuration reloaded
        NotifyConfigurationChanged();
    }
}
```

## Cleanup and Disposal

```csharp
public void Shutdown()
{
    var config = ConfiguredShared.Instance;
    
    try
    {
        // Ensure changes are persisted
        config.Flush();
    }
    finally
    {
        // Clean up resources
        config.Dispose();
    }
}
```
