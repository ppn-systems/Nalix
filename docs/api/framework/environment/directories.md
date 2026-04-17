# Directories

`Directories` is the shared path-resolution helper in `Nalix.Framework.Environment`.

## Source mapping

- `src/Nalix.Framework/Environment/Directories.Lazy.cs`
- `src/Nalix.Framework/Environment/Directories.Properties.cs`
- `src/Nalix.Framework/Environment/Directories.PublicMethods.cs`
- `src/Nalix.Framework/Environment/Directories.UnixDirPerms.cs`

## What it does

- resolves standard runtime directories for data, logs, config, storage, database, cache, uploads, backups, and temp files
- supports environment-variable overrides
- detects container environments
- creates directories lazily on first access
- hardens permissions depending on platform
- exposes helper methods for building safe child paths

## Main properties

Common entry points:

- `BaseAssetsDirectory`
- `LogsDirectory`
- `DataDirectory`
- `ConfigurationDirectory`
- `TemporaryDirectory`
- `StorageDirectory`
- `DatabaseDirectory`
- `CacheDirectory`
- `UploadsDirectory`
- `BackupsDirectory`
- `IsRunningInContainer`

## Resolution behavior

The current implementation resolves paths in this order:

1. internal base-path override for tests
2. explicit environment variable override
3. container defaults such as `/data`, `/logs`, `/config`, `/storage`, `/db`
4. OS-specific fallback

Typical environment variables:

- `NALIX_BASE_PATH`
- `NALIX_DATA_PATH`
- `NALIX_LOGS_PATH`
- `NALIX_CONFIG_PATH`
- `NALIX_STORAGE_PATH`
- `NALIX_DB_PATH`
- `NALIX_TEMP_PATH`

## Container and platform behavior

- on Windows, the base fallback is under `%ProgramData%\Nalix`
- on Unix-like systems, the base fallback is under `$XDG_DATA_HOME` or `~/.local/share/Nalix`
- in containers, the helper prefers well-known mounted directories such as `/data` and `/config`

!!! note "Default config locations"
    **Windows**  
    `C:\ProgramData\Nalix\config\default.ini`  

    **Linux / macOS**  
    `~/.local/share/Nalix/config/default.ini`

    **Docker / Container**  
    `/config/default.ini`

## Public helper methods

Useful methods include:

- `CreateSubdirectory(...)`
- `CreateTimestampedDirectory(...)`
- `GetFilePath(...)`
- `GetTempFilePath(...)`
- `GetLogFilePath(...)`
- `GetConfigFilePath(...)`
- `GetStorageFilePath(...)`
- `GetDatabaseFilePath(...)`
- `DeleteOldFiles(...)`
- `EnumerateFiles(...)`
- `CalculateDirectorySize(...)`
- `CreateDateDirectory(...)`
- `CreateHierarchicalDateDirectory(...)`
- `EnsureShardedPath(...)`
- `CanAccessAllDirectories()`

## Event hooks

`Directories` also lets you subscribe to directory-creation events:

```csharp
Directories.RegisterDirectoryCreationHandler(path =>
{
    Console.WriteLine($"created={path}");
});
```

## Safety notes

- child paths are built through a safe combine routine that rejects path traversal outside the base directory
- directory creation is guarded by a lock for thread-safe first access
- Unix permissions are hardened according to directory purpose

## Typical usage

```csharp
string configFile = Directories.GetConfigFilePath("default.ini");
string logFile = Directories.GetLogFilePath("server.log");
string uploads = Directories.CreateSubdirectory(Directories.DataDirectory, "imports");
```

## Example: Docker Compose

Run Nalix with mounted config and persistent storage.

```yaml
services:
  nalix:
    image: your-image
    ports:
      - "57206:57206"
    volumes:
      - ./config:/config
      - ./data:/data
      - ./logs:/logs
    environment:
      - NALIX_CONFIG_PATH=/config
      - NALIX_DATA_PATH=/data
      - NALIX_LOGS_PATH=/logs
```

## Related APIs

- [Configuration](../runtime/configuration.md)
- [Instance Manager (DI)](../runtime/instance-manager.md)
- [Logging Targets](../../logging/targets.md)
- [Installation](../../../installation.md)
