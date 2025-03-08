# DirectoriesDefault Documentation

## Overview

`DirectoriesDefault` provides a comprehensive directory management system for applications, with special support for containerized environments. It manages standard application directories with thread-safe operations, automatic creation, and cleanup capabilities.

## Directory Structure

```texttext
BasePath/
├── Data/
│   ├── Logs/
│   ├── Config/
│   ├── Temp/
│   ├── Storage/
│   ├── Database/
│   ├── Caches/
│   ├── Uploads/
│   └── Backups/
```

## Standard Directories

### Base Properties

```csharp
public static string BasePath { get; }        // Application base directory
public static string DataPath { get; }        // Main data storage
public static string LogsPath { get; }        // Log files
public static string ConfigPath { get; }      // Configuration files
public static string TempPath { get; }        // Temporary files
public static string StoragePath { get; }     // Persistent storage
public static string DatabasePath { get; }    // Database files
public static string CachesPath { get; }      // Cache files
public static string UploadsPath { get; }     // Upload files
public static string BackupsPath { get; }     // Backup files
```

## Container Support

The class automatically detects and adapts to containerized environments:

```csharp
public static bool IsContainer { get; }  // True if running in container

// Container-specific paths:
/app      // Base directory
/data     // Data directory
/logs     // Logs directory
/config   // Configuration directory
/storage  // Storage directory
/db       // Database directory
/tmp/notio// Temporary directory
```

## Key Features

### 1. Directory Creation and Management

```csharp
// Create a subdirectory
string path = DirectoriesDefault.CreateSubdirectory(parentPath, "MyFolder");

// Create timestamped directory
string timestamp = DirectoriesDefault.CreateTimestampedDirectory(
    parentPath, 
    "prefix"
);

// Create date-based directories
string daily = DirectoriesDefault.CreateDateDirectory(parentPath);
string hierarchical = DirectoriesDefault.CreateHierarchicalDateDirectory(parentPath);
```

### 2. File Path Management

```csharp
// Get various file paths
string configFile = DirectoriesDefault.GetConfigFilePath("settings.json");
string logFile = DirectoriesDefault.GetLogFilePath("app.log");
string tempFile = DirectoriesDefault.GetTempFilePath("temp.dat");
string dbFile = DirectoriesDefault.GetDatabaseFilePath("data.db");
```

### 3. Directory Maintenance

```csharp
// Clean up old files
int deleted = DirectoriesDefault.CleanupDirectory(
    DirectoriesDefault.TempPath,
    TimeSpan.FromDays(7)
);

// Calculate directory size
long size = DirectoriesDefault.GetDirectorySize(DirectoriesDefault.StoragePath);

// Validate all directories
bool isValid = DirectoriesDefault.ValidateDirectories();
```

### 4. Event Handling

```csharp
// Register for directory creation events
DirectoriesDefault.RegisterDirectoryCreationHandler(path => 
{
    Console.WriteLine($"Created directory: {path}");
});
```

## Usage Examples

### Basic Directory Operations

```csharp
public class FileManager
{
    public void SaveConfig(string fileName, string content)
    {
        string path = DirectoriesDefault.GetConfigFilePath(fileName);
        File.WriteAllText(path, content);
    }

    public void CreateBackup()
    {
        string backupDir = DirectoriesDefault.CreateTimestampedDirectory(
            DirectoriesDefault.BackupsPath,
            "backup"
        );
        // Perform backup operations...
    }
}
```

### Date-Based Organization

```csharp
public class LogManager
{
    public void SaveLog(string content)
    {
        // Creates YYYY/MM/DD structure
        string logDir = DirectoriesDefault.CreateHierarchicalDateDirectory(
            DirectoriesDefault.LogsPath
        );
        string logFile = Path.Combine(logDir, "application.log");
        File.AppendAllText(logFile, content);
    }
}
```

### Container-Aware Storage

```csharp
public class StorageManager
{
    public string GetStoragePath()
    {
        // Automatically uses /storage in containers, otherwise local path
        return DirectoriesDefault.StoragePath;
    }

    public void ConfigureDatabase(string dbName)
    {
        // Uses /db in containers, otherwise local database path
        string dbPath = DirectoriesDefault.GetDatabaseFilePath(dbName);
        // Configure database with path...
    }
}
```

## Thread Safety

The class implements thread-safe operations using `ReaderWriterLockSlim`:

```csharp
public class ThreadSafeOperation
{
    public void CreateMultipleDirectories()
    {
        // Thread-safe directory creation
        Parallel.ForEach(new[] { "dir1", "dir2", "dir3" }, dirName =>
        {
            DirectoriesDefault.CreateSubdirectory(
                DirectoriesDefault.DataPath, 
                dirName
            );
        });
    }
}
```

## Error Handling

```csharp
try
{
    string path = DirectoriesDefault.CreateSubdirectory(parentPath, "NewDir");
}
catch (DirectoryException ex)
{
    Console.WriteLine($"Failed to create directory: {ex.Path}");
    Console.WriteLine($"Error: {ex.Message}");
}
```

## Best Practices

1. **Directory Creation**

   ```csharp
   // DO: Use provided methods for directory creation
   string logDir = DirectoriesDefault.CreateDateDirectory(
       DirectoriesDefault.LogsPath
   );

   // DON'T: Create directories directly
   string logDir = Path.Combine(DirectoriesDefault.LogsPath, "logs");
   Directory.CreateDirectory(logDir);
   ```

2. **File Path Management**

   ```csharp
   // DO: Use the provided file path methods
   string configPath = DirectoriesDefault.GetConfigFilePath("app.config");

   // DON'T: Construct paths manually
   string configPath = Path.Combine(DirectoriesDefault.ConfigPath, "app.config");
   ```

3. **Temporary Files**

   ```csharp
   // DO: Use TempPath for temporary files
   string tempFile = DirectoriesDefault.GetTempFilePath("process.tmp");

   // DON'T: Use system temp directory
   string tempFile = Path.Combine(Path.GetTempPath(), "process.tmp");
   ```

## Performance Optimization

1. **Lazy Loading**
   - All directories are initialized lazily
   - Resources are only allocated when needed

2. **Caching**
   - Directory existence checks are optimized
   - Path combinations are computed once

3. **Thread Safety**
   - Uses `ReaderWriterLockSlim` for optimal concurrency
   - Minimizes lock contention

## Limitations

1. Base path override only works before first access
2. Container detection might need updates for new container types
3. Directory creation events are not persisted
4. Cleanup operations are best-effort only

## See Also

- [System.IO.Directory Documentation](https://docs.microsoft.com/en-us/dotnet/api/system.io.directory)
- [File System and Path Operations](https://docs.microsoft.com/en-us/dotnet/standard/io)
- [Docker Volume Documentation](https://docs.docker.com/storage/volumes/)
