# InMemoryStorage Class Documentation

The `InMemoryStorage` class provides an in-memory implementation of the `IFileStorageAsync` interface for storing and retrieving files. This class is part of the `Notio.Storage.Local` namespace and is designed to handle file operations in memory, making it suitable for scenarios where fast access to files is required without persistent storage.

## Namespace

```csharp
using Notio.Storage.Config;
using Notio.Storage.FileFormats;
using Notio.Storage.Generator;
using Notio.Storage.MimeTypes;
using Notio.Storage.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
```

## Class Definition

### Summary

The `InMemoryStorage` class is designed to provide in-memory storage for files. It supports uploading, downloading, checking for existence, and deleting files. It can also generate files in different formats using a configured file generator.

```csharp
namespace Notio.Storage.Local
{
    /// <summary>
    /// Provides an in-memory implementation of <see cref="IFileStorageAsync"/> for storing and retrieving files.
    /// </summary>
    public class InMemoryStorage : IFileStorageAsync
    {
        // Class implementation...
    }
}
```

## Properties

### _storageConfig

```csharp
private readonly InMemoryConfig _storageConfig;
```

- **Description**: Configuration for the in-memory storage.

### _storage

```csharp
private readonly Dictionary<string, InMemoryFile> _storage = new();
```

- **Description**: Dictionary to store files in memory, using a key generated from file name and format.

## Constructors

### InMemoryStorage(InMemoryConfig storageConfig)

```csharp
public InMemoryStorage(InMemoryConfig storageConfig)
```

- **Description**: Initializes a new instance of `InMemoryStorage` with the specified storage configuration.
- **Parameters**:
  - `storageConfig`: The configuration settings for in-memory storage.
- **Exceptions**:
  - `ArgumentNullException`: Thrown if `storageConfig` is null.

### InMemoryStorage()

```csharp
public InMemoryStorage()
```

- **Description**: Initializes a new instance of `InMemoryStorage` with default settings.

## Methods

### DownloadAsync

```csharp
public async Task<IFile> DownloadAsync(string fileName, string format = "original")
```

- **Description**: Downloads a file from the in-memory storage.
- **Parameters**:
  - `fileName`: The name of the file to download.
  - `format`: The format of the file (default is "original").
- **Returns**: An `IFile` object representing the downloaded file.
- **Exceptions**:
  - `ArgumentNullException`: Thrown if `fileName` is null or empty.
  - `FileNotFoundException`: Thrown if the file is not found.

### FileExistsAsync

```csharp
public Task<bool> FileExistsAsync(string fileName, string format = "original")
```

- **Description**: Checks if a file exists in memory.
- **Parameters**:
  - `fileName`: The name of the file.
  - `format`: The format of the file (default is "original").
- **Returns**: `true` if the file exists; otherwise, `false`.

### GetFileUriAsync

```csharp
public Task<string> GetFileUriAsync(string fileName, string format = "original")
```

- **Description**: Gets the URI of a file in memory.
- **Parameters**:
  - `fileName`: The name of the file.
  - `format`: The format of the file (default is "original").
- **Returns**: The URI of the file.

### GetStream

```csharp
public Stream GetStream(string fileName, IEnumerable<FileMeta> metaInfo, string format = "original")
```

- **Description**: Gets a stream for a file in memory. (Not implemented)
- **Parameters**:
  - `fileName`: The name of the file.
  - `metaInfo`: Metadata associated with the file.
  - `format`: The format of the file (default is "original").
- **Returns**: A `Stream` object for the file.
- **Exceptions**:
  - `NotImplementedException`: Always thrown as this method is not implemented.

### UploadAsync

```csharp
public Task UploadAsync(string fileName, byte[] data, IEnumerable<FileMeta> metaInfo, string format = "original")
```

- **Description**: Uploads a file to the in-memory storage.
- **Parameters**:
  - `fileName`: The name of the file to upload.
  - `data`: The file data.
  - `metaInfo`: Metadata associated with the file.
  - `format`: The format of the file (default is "original").
- **Exceptions**:
  - `ArgumentNullException`: Thrown if any required parameter is null or empty.

### DeleteAsync

```csharp
public Task DeleteAsync(string fileName)
```

- **Description**: Deletes a file from in-memory storage.
- **Parameters**:
  - `fileName`: The name of the file to delete.

### GetKey

```csharp
private static string GetKey(string fileName, string format) => $"{format}/{fileName}";
```

- **Description**: Generates a unique key based on the file name and format.
- **Parameters**:
  - `fileName`: The name of the file.
  - `format`: The format of the file.
- **Returns**: A string key used to identify the file in storage.

## Example Usage

Here's a basic example of how to use the `InMemoryStorage` class:

```csharp
using Notio.Storage.Config;
using Notio.Storage.Local;

public class StorageExample
{
    public async Task ExampleUsage()
    {
        var config = new InMemoryConfig(new FileGenerator())
            .UseFileGenerator(new FileGenerator())
            .UseMimeTypeResolver(new MimeTypeResolver());

        var storage = new InMemoryStorage(config);

        // Upload a file
        await storage.UploadAsync("example.txt", new byte[] { 1, 2, 3 }, new List<FileMeta>());

        // Download a file
        var file = await storage.DownloadAsync("example.txt");

        // Check if a file exists
        bool exists = await storage.FileExistsAsync("example.txt");

        // Delete a file
        await storage.DeleteAsync("example.txt");
    }
}
```

## Remarks

The `InMemoryStorage` class is designed to be easily configurable and extendable. By default, it uses a file generator and MIME type resolver, but these can be customized through the `InMemoryConfig` object.

Feel free to explore the individual methods and properties to understand their specific purposes and implementations. If you need detailed documentation for any specific file or directory, please refer to the source code or let me know!