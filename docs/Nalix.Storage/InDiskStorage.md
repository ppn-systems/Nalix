# InDiskStorage Class Documentation

The `InDiskStorage` class provides an implementation of the `IFileStorage` interface that stores files on disk. This class is part of the `Notio.Storage.Local` namespace and is designed to handle file operations using disk-based storage.

## Namespace

```csharp
using Notio.Shared;
using Notio.Storage.Config;
using Notio.Storage.FileFormats;
using Notio.Storage.Generator;
using Notio.Storage.MimeTypes;
using Notio.Storage.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
```

## Class Definition

### Summary

The `InDiskStorage` class is designed to provide disk-based storage for files. It supports uploading, downloading, checking for existence, and deleting files. It can also generate files in different formats using a configured file generator.

```csharp
namespace Notio.Storage.Local
{
    /// <summary>
    /// Provides an implementation of <see cref="IFileStorage"/> that stores files on disk.
    /// </summary>
    public class InDiskStorage : IFileStorage
    {
        // Class implementation...
    }
}
```

## Properties

### _storageConfig

```csharp
private readonly InDiskConfig _storageConfig;
```

- **Description**: Configuration for disk-based storage.

## Constructors

### InDiskStorage(InDiskConfig storageSettings)

```csharp
public InDiskStorage(InDiskConfig storageSettings)
```

- **Description**: Initializes a new instance of `InDiskStorage` with the specified configuration.
- **Parameters**:
  - `storageSettings`: The configuration settings for disk storage.
- **Exceptions**:
  - `ArgumentNullException`: Thrown if `storageSettings` is null.

### InDiskStorage()

```csharp
public InDiskStorage()
```

- **Description**: Initializes a new instance of `InDiskStorage` with default settings.

## Methods

### Upload

```csharp
public void Upload(string fileName, byte[] data, IEnumerable<FileMeta> metaInfo, string format = "original")
```

- **Description**: Uploads a file to the disk storage.
- **Parameters**:
  - `fileName`: The name of the file to upload.
  - `data`: The file data.
  - `metaInfo`: Metadata associated with the file.
  - `format`: The format of the file (default is "original").
- **Exceptions**:
  - `ArgumentNullException`: Thrown if any required parameter is null or empty.

### Download

```csharp
public IFile Download(string fileName, string format = "original")
```

- **Description**: Downloads a file from the disk storage.
- **Parameters**:
  - `fileName`: The name of the file to download.
  - `format`: The format of the file (default is "original").
- **Returns**: An `IFile` object representing the downloaded file.
- **Exceptions**:
  - `ArgumentNullException`: Thrown if `fileName` is null or empty.
  - `FileNotFoundException`: Thrown if the file is not found.

### GetFileUri

```csharp
public string GetFileUri(string fileName, string format = "original")
```

- **Description**: Gets the URI of a file on disk.
- **Parameters**:
  - `fileName`: The name of the file.
  - `format`: The format of the file (default is "original").
- **Returns**: The URI of the file.
- **Exceptions**:
  - `ArgumentNullException`: Thrown if `fileName` is null or empty.

### FileExists

```csharp
public bool FileExists(string fileName, string format = "original")
```

- **Description**: Checks if a file exists on disk.
- **Parameters**:
  - `fileName`: The name of the file.
  - `format`: The format of the file (default is "original").
- **Returns**: `true` if the file exists; otherwise, `false`.
- **Exceptions**:
  - `ArgumentNullException`: Thrown if `fileName` is null or empty.

### GetStream

```csharp
public Stream GetStream(string fileName, IEnumerable<FileMeta> metaInfo, string format = "original")
```

- **Description**: Gets a stream for a file on disk. (Not implemented)
- **Parameters**:
  - `fileName`: The name of the file.
  - `metaInfo`: Metadata associated with the file.
  - `format`: The format of the file (default is "original").
- **Returns**: A `Stream` object for the file.
- **Exceptions**:
  - `NotImplementedException`: Always thrown as this method is not implemented.

### Delete

```csharp
public void Delete(string fileName)
```

- **Description**: Deletes a file from disk storage.
- **Parameters**:
  - `fileName`: The name of the file to delete.
- **Exceptions**:
  - `ArgumentNullException`: Thrown if `fileName` is null or empty.

## Example Usage

Here's a basic example of how to use the `InDiskStorage` class:

```csharp
using Notio.Storage.Config;
using Notio.Storage.Local;

public class StorageExample
{
    public void ExampleUsage()
    {
        var config = new InDiskConfig("storage/path")
            .UseFileGenerator(new FileGenerator())
            .UseMimeTypeResolver(new MimeTypeResolver());

        var storage = new InDiskStorage(config);

        // Upload a file
        storage.Upload("example.txt", new byte[] { 1, 2, 3 }, new List<FileMeta>());

        // Download a file
        var file = storage.Download("example.txt");

        // Check if a file exists
        bool exists = storage.FileExists("example.txt");

        // Delete a file
        storage.Delete("example.txt");
    }
}
```

## Remarks

The `InDiskStorage` class is designed to be easily configurable and extendable. By default, it uses a file generator and MIME type resolver, but these can be customized through the `InDiskConfig` object.

Feel free to explore the individual methods and properties to understand their specific purposes and implementations. If you need detailed documentation for any specific file or directory, please refer to the source code or let me know!
