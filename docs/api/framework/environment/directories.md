# Directories

`Directories` is the shared path-resolution helper in `Nalix.Framework.Environment`. It provides a centralized, platform-aware mechanism for resolving and hardening essential application directories.

## Path Resolution Logic

The following diagram illustrates the priority-based resolution chain used by the `Directories` helper.

```mermaid
flowchart TD
    Start([Request Directory Path]) --> Override{Internal Override?}
    Override -- Yes --> Resolve[Return Specific Path]
    Override -- No --> Env{Env Variable Set?}
    
    Env -- Yes --> Resolve
    Env -- No --> Container{In Container?}
    
    Container -- Yes --> CDefault[Return /data, /config, etc.]
    Container -- No --> OS{Operating System?}
    
    OS -- Windows --> WinFallback[Return %ProgramData%\Nalix]
    OS -- Unix/macOS --> UnixFallback[Return $XDG_DATA_HOME or ~/.local/share/Nalix]
    
    Resolve --> Hardening[Ensure Created & Harden Permissions]
    CDefault --> Hardening
    WinFallback --> Hardening
    UnixFallback --> Hardening
    
    Hardening --> End([Return Absolute Path])
```

## Source Mapping

- `src/Nalix.Framework/Environment/Directories.Lazy.cs`
- `src/Nalix.Framework/Environment/Directories.Properties.cs`
- `src/Nalix.Framework/Environment/Directories.PublicMethods.cs`
- `src/Nalix.Framework/Environment/Directories.UnixDirPerms.cs`

## Key Capabilities

- **Lazy Creation**: Directories are created automatically upon the first property access.
- **Environment Aware**: Automatically detects Docker/Kubernetes environments and adapts fallbacks.
- **Security-First**: Applies restricted permissions (e.g., `0700` or `0750` on Unix) depending on the directory's purpose.
- **Safety Guards**: Prevents path traversal via safe combination routines.
- **Auto-Cleanup**: The `TemporaryDirectory` automatically removes files older than a retention period (default: 7 days).

## Main Properties

| Property | Default Purpose | Default Subdirectory |
| :--- | :--- | :--- |
| `BaseAssetsDirectory` | Application root for all assets | `Nalix/` |
| `DataDirectory` | Core persistent application data | `data/` |
| `LogsDirectory` | Application execution logs | `logs/` |
| `ConfigurationDirectory` | INI and security configuration files | `config/` |
| `TemporaryDirectory` | Transient files with auto-cleanup | `tmp/` |
| `DatabaseDirectory` | Database and WAL files | `db/` |
| `StorageDirectory` | Large file storage | `storage/` |
| `CacheDirectory` | Non-critical cache files | `caches/` |
| `UploadsDirectory` | Incoming user-uploaded files | `uploads/` |
| `BackupsDirectory` | System and data backups | `backups/` |

## Resolution Behavior

Paths are resolved using the following priority:

1. **Internal Override**: Primarily used for unit testing.
2. **Explicit Environment Variable**: Variables like `NALIX_BASE_PATH`, `NALIX_DATA_PATH`, `NALIX_LOGS_PATH`, etc.
3. **Container Defaults**: Prefers mounted volumes like `/data`, `/logs`, `/config`.
4. **OS Fallback**: Platform-specific user/system data directories.

!!! note "Container Detection"
    The helper detects containers via heuristics including `DOTNET_RUNNING_IN_CONTAINER`, `KUBERNETES_SERVICE_HOST`, presence of `/.dockerenv`, and `/proc/1/cgroup` markers.

## Manual Path Resolution

The helper provides several methods for building safe sub-paths:

- `GetFilePath(string fileName)`: Resolves a file within the base directory.
- `GetLogFilePath(string fileName)`: Resolves a file within the logs directory.
- `CreateSubdirectory(string parent, string name)`: Creates a named child directory safely.
- `CreateTimestampedDirectory(string parent, string prefix)`: Creates unique directories for exports or temporary processing.

## Management Methods

- `DeleteOldFiles(string path, TimeSpan age)`: Removes files older than the specified duration.
- `CalculateDirectorySize(string path)`: Recursively computes total size on disk.
- `CanAccessAllDirectories()`: Diagnostic check for read/write permissions across all managed paths.

## Event Hooks

You can monitor directory creation globally:

```csharp
Directories.RegisterDirectoryCreationHandler(path =>
{
    Console.WriteLine($"Infrastructure created: {path}");
});
```

## Typical usage

```csharp
// Get paths
string configFile = Directories.GetConfigFilePath("default.ini");
string logFile = Directories.GetLogFilePath("server.log");

// Create structure
string imports = Directories.CreateSubdirectory(Directories.DataDirectory, "imports");
```

## Related APIs

- [Configuration](../runtime/configuration.md)
- [Instance Manager (DI)](../runtime/instance-manager.md)
- [Logging Targets](../../logging/targets.md)
- [Installation](../../../installation.md)
