# Environment Utility Documentation

## Overview

The `Environment` class provides a comprehensive set of utilities for retrieving information about the current application environment, including operating system details, assembly information, and application-specific paths. It includes thread-safe, lazy-loaded properties and efficient system detection mechanisms.

## Enumerations

### OSType

Defines the supported operating systems:

```csharp
public enum OSType
{
    Unknown,
    Windows,
    Unix,
    Osx
}
```

### Endianness

Defines byte order conventions:

```csharp
public enum Endianness
{
    Big,    // Most significant byte in smallest address
    Little  // Least significant byte in smallest address
}
```

## Properties

### Operating System Information

```csharp
// Gets the current operating system type
public static OSType OS { get; }

// Checks if running under MONO runtime
public static bool IsUsingMonoRuntime { get; }
```

### Assembly Information

```csharp
// Gets the entry assembly
public static Assembly EntryAssembly { get; }

// Gets the entry assembly name
public static AssemblyName EntryAssemblyName { get; }

// Gets the entry assembly version
public static Version? EntryAssemblyVersion { get; }

// Gets the entry assembly directory path
public static string EntryAssemblyDirectory { get; }
```

### Application Information

```csharp
// Gets the company name from assembly attributes
public static string CompanyName { get; }

// Gets the product name from assembly attributes
public static string ProductName { get; }

// Gets the product trademark from assembly attributes
public static string ProductTrademark { get; }

// Checks if this is the only instance running
public static bool IsTheOnlyInstance { get; }

// Gets the local storage path for the application
public static string LocalStoragePath { get; }
```

## Methods

### Desktop File Path

```csharp
public static string GetDesktopFilePath(string filename)
```

Builds a full path to a file on the user's desktop.

#### Parameters

- `filename`: The name of the file

#### Returns

The fully qualified path to the file on the desktop.

#### Example

```csharp
string path = Environment.GetDesktopFilePath("config.json");
// Returns: C:\Users\username\Desktop\config.json
```

## Usage Examples

### Basic Usage

```csharp
// Check operating system
if (Environment.OS == OSType.Windows)
{
    // Windows-specific code
}

// Get application information
string company = Environment.CompanyName;
string product = Environment.ProductName;
Version? version = Environment.EntryAssemblyVersion;

// Check for single instance
if (Environment.IsTheOnlyInstance)
{
    // Start application
}
```

### File Path Operations

```csharp
// Get application storage path
string storagePath = Environment.LocalStoragePath;
string configFile = Path.Combine(storagePath, "settings.json");

// Get desktop file path
string desktopFile = Environment.GetDesktopFilePath("export.csv");
```

### Assembly Information

```csharp
// Get assembly details
string assemblyDirectory = Environment.EntryAssemblyDirectory;
AssemblyName assemblyName = Environment.EntryAssemblyName;
```

## Implementation Details

### Lazy Loading

The class uses lazy initialization for optimal performance:

```csharp
private static readonly Lazy<Assembly> EntryAssemblyLazy = new(() =>
    Assembly.GetEntryAssembly() ?? 
    throw new InvalidOperationException("Entry assembly is null."));

private static readonly Lazy<string> CompanyNameLazy = new(() =>
{
    var attribute = EntryAssembly.GetCustomAttribute<AssemblyCompanyAttribute>();
    return attribute?.Company ?? string.Empty;
});
```

### OS Detection

Operating system detection is performed through environment checks:

```csharp
private static readonly Lazy<OSType> OsLazy = new(() =>
{
    var windir = System.Environment.GetEnvironmentVariable("windir");
    if (!string.IsNullOrEmpty(windir) && windir.Contains('\\') && 
        Directory.Exists(windir))
    {
        return OSType.Windows;
    }
    return File.Exists("/proc/sys/kernel/ostype") ? 
        OSType.Unix : OSType.Osx;
});
```

### Thread Safety

The class implements thread-safe patterns:

```csharp
private static readonly Lock SyncLock = new();

// Usage in IsTheOnlyInstance
lock (SyncLock)
{
    // Thread-safe operations
}
```

## Best Practices

1. **Single Instance Check**

   ```csharp
   public class Program
   {
       public static void Main()
       {
           if (!Environment.IsTheOnlyInstance)
           {
               Console.WriteLine("Application is already running");
               return;
           }
           // Continue with application startup
       }
   }
   ```

2. **Path Management**

   ```csharp
   public class ConfigManager
   {
       private readonly string _configPath;
       
       public ConfigManager()
       {
           _configPath = Path.Combine(
               Environment.LocalStoragePath,
               "config.json"
           );
       }
   }
   ```

3. **Version Information**

   ```csharp
   public class VersionInfo
   {
       public static string GetVersionString()
       {
           return Environment.EntryAssemblyVersion?.ToString() ?? 
                  "Unknown Version";
       }
   }
   ```

## Error Handling

The class includes comprehensive error handling:

```csharp
// Assembly location validation
public static string EntryAssemblyDirectory
{
    get
    {
        var location = EntryAssembly.Location;
        if (string.IsNullOrEmpty(location))
            throw new InvalidOperationException(
                "Entry assembly location is null or empty.");
        
        // Additional error handling...
    }
}
```

## Notes and Limitations

1. Some properties may throw `InvalidOperationException` if assembly information is unavailable
2. OS detection is based on environment checks and may not cover all edge cases
3. Single instance checking uses global mutex and may require elevated privileges
4. Local storage path creation may fail if the application lacks write permissions

## See Also

- [System.Environment Documentation](https://docs.microsoft.com/en-us/dotnet/api/system.environment)
- [Assembly Class](https://docs.microsoft.com/en-us/dotnet/api/system.reflection.assembly)
- [Mutex Class](https://docs.microsoft.com/en-us/dotnet/api/system.threading.mutex)
