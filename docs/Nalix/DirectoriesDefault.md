# DirectoriesDefault Class Documentation

## Overview

The `DirectoriesDefault` class provides utility methods and properties to manage default directories used within the application. It offers enhanced functionality and flexibility for both development and production environments, including containerized environments like Docker and Kubernetes.

## Private Fields

- `DirectoryLock`: A thread-safe reader-writer lock for directory creation.
- `DirectoryCreated`: An event that triggers when a directory is created.
- `IsContainerLazy`: A lazy-loaded flag indicating if the application is running in a container.
- `_basePathOverride`: A string used to override the base path for testing purposes.
- Various lazy-loaded paths for different directory types.

## Public Properties

- `BasePath`: Gets the base directory of the application.
- `LogsPath`: Gets the directory for storing log files.
- `DataPath`: Gets the directory for storing application data files.
- `ConfigPath`: Gets the directory for storing system configuration files.
- `TempPath`: Gets the directory for storing temporary files.
- `StoragePath`: Gets the directory for storing persistent data.
- `DatabasePath`: Gets the directory for storing database files.
- `CachesPath`: Gets the directory for storing cache files.
- `UploadsPath`: Gets the directory for storing uploaded files.
- `BackupsPath`: Gets the directory for storing backup files.
- `IsContainer`: Indicates if the application is running in a container environment.

## Constructors

The static constructor ensures that all necessary directories are created upon initialization.

## Public Methods

- `RegisterDirectoryCreationHandler`: Registers a handler for directory creation events.
- `UnregisterDirectoryCreationHandler`: Unregisters a handler for directory creation events.
- `CreateSubdirectory`: Creates a subdirectory within the specified parent directory if it doesn't exist.
- `CreateTimestampedDirectory`: Creates a timestamped subdirectory within the specified parent directory.
- `GetFilePath`: Gets a path to a file within one of the application's directories.
- `GetTempFilePath`: Gets a path to a temporary file with the given name.
- `GetTimestampedFilePath`: Gets a path to a timestamped file in the specified directory.
- `GetLogFilePath`: Gets a path to a log file with the given name.
- `GetConfigFilePath`: Gets a path to a config file with the given name.
- `GetStorageFilePath`: Gets a path to a storage file with the given name.
- `GetDatabaseFilePath`: Gets a path to a database file with the given name.
- `CleanupDirectory`: Cleans up files in the specified directory that are older than the given timespan.
- `ValidateDirectories`: Ensures all application directories exist and have proper permissions.
- `OverrideBasePathForTesting`: Temporarily overrides the base path for testing purposes.
- `GetFiles`: Gets all files in a directory matching a pattern, with optional recursive search.
- `GetDirectorySize`: Gets the size of a directory in bytes, optionally including subdirectories.
- `CreateDateDirectory`: Creates a subdirectory for today's date in the specified parent directory.
- `CreateHierarchicalDateDirectory`: Creates a hierarchical directory structure based on the current date.

## Private Methods

- `EnsureDirectoryExists`: Ensures that a directory exists, creating it if necessary. Uses a reader-writer lock to ensure thread safety.

## Usage Examples

### Creating a Subdirectory

```csharp
string subdirectoryPath = DirectoriesDefault.CreateSubdirectory(DirectoriesDefault.DataPath, "MySubdirectory");
Console.WriteLine($"Subdirectory created at: {subdirectoryPath}");
```

### Getting a Temporary File Path

```csharp
string tempFilePath = DirectoriesDefault.GetTempFilePath("tempfile.tmp");
Console.WriteLine($"Temporary file path: {tempFilePath}");
```

### Cleaning Up Old Files in a Directory

```csharp
int deletedFilesCount = DirectoriesDefault.CleanupDirectory(DirectoriesDefault.TempPath, TimeSpan.FromDays(7));
Console.WriteLine($"Number of deleted files: {deletedFilesCount}");
```

### Validating Directories

```csharp
bool areDirectoriesValid = DirectoriesDefault.ValidateDirectories();
Console.WriteLine($"Are directories valid? {areDirectoriesValid}");
```

### Overriding Base Path for Testing

```csharp
DirectoriesDefault.OverrideBasePathForTesting("/custom/test/path");
Console.WriteLine($"Overridden base path: {DirectoriesDefault.BasePath}");
```
