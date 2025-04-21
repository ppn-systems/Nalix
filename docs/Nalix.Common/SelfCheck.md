# SelfCheck Class Documentation

The `SelfCheck` class provides static methods to perform self-checks in library or application code. It generates exceptions with detailed information about the location and context of the failure, facilitating easier debugging and error tracking. This class is part of the `Notio.Common` namespace.

## Namespace

```csharp
using Notio.Common.Exceptions;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
```

## Class Definition

### Summary

The `SelfCheck` class provides a method to create and return an exception indicating that an internal self-check has failed. The exception contains information about the assembly, source file, and line number of the failure.

```csharp
namespace Notio.Common
{
    /// <summary>
    /// Provides methods to perform self-checks in library or application code.
    /// </summary>
    public static class SelfCheck
    {
        // Class implementation...
    }
}
```

## Methods

### Failure

```csharp
public static InternalErrorException Failure(string message,
    [CallerMemberName] string callerMethod = "",
    [CallerFilePath] string filePath = "",
    [CallerLineNumber] int lineNumber = 0)
```

- **Description**: Creates and returns an exception indicating that an internal self-check has failed. The exception will be of type `InternalErrorException`, and its `Message` property will contain the specified message, preceded by details of the assembly, source file, and line number of the failure.
- **Parameters**:
  - `message`: The exception message.
  - `callerMethod`: The name of the method where the failure occurs. This parameter is automatically added by the compiler and should not be provided explicitly.
  - `filePath`: The path of the source file where this method is called. This parameter is automatically added by the compiler and should not be provided explicitly.
  - `lineNumber`: The line number in the source file where this method is called. This parameter is automatically added by the compiler and should not be provided explicitly.
- **Returns**: A newly-created instance of `InternalErrorException`.

### Example Usage

Here's a basic example of how to use the `SelfCheck` class:

```csharp
using Notio.Common;

public class Example
{
    public void PerformCheck()
    {
        try
        {
            // Simulate a self-check failure
            throw SelfCheck.Failure("An internal error has occurred.");
        }
        catch (InternalErrorException ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
}
```

## Remarks

The `SelfCheck` class is designed to provide a standardized way to handle internal self-checks in the Notio framework. It ensures that all failures contain consistent and detailed context information, making it easier to identify and resolve issues.

Feel free to explore the method to understand its specific purposes and implementations. If you need detailed documentation for any specific file or directory, please refer to the source code or let me know!
