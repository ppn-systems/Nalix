# Nalix.Common Documentation

## Overview

**Nalix.Common** is the foundational library of the Nalix ecosystem, providing essential utilities for logging, memory management, cryptography, security, exception handling, and system operations. It serves as a core dependency for all other Nalix components and offers reusable, high-performance components to enhance application development.

## Key Features

### 🔧 **Core Utilities**
- **Logging System**: Comprehensive logging framework with hierarchical levels and event correlation
- **Memory Management**: Efficient buffer pooling and memory optimization utilities
- **Security**: Permission levels, connection limits, and request throttling
- **Caching**: Buffer pool abstractions for high-performance scenarios
- **Exception Handling**: Structured error handling and reporting
- **System Operations**: Low-level system utilities and interoperability

### 🏗️ **Architecture**
- **Repository Pattern**: Structured data access abstractions
- **Connection Management**: Database and network connection utilities
- **Serialization**: High-performance data serialization components
- **Identity Management**: User and system identity utilities
- **Configuration**: Package and deployment configuration management

## Project Structure

```
Nalix.Common/
├── Caching/                    # Buffer pooling and caching utilities
│   ├── IBufferPool.cs         # Buffer pool interface
│   └── IPoolable.cs           # Poolable object interface
├── Connection/                 # Connection management utilities
├── Constants/                  # Application constants and definitions
├── Cryptography/              # Cryptographic utilities and helpers
├── Exceptions/                # Custom exception types and handling
├── Identity/                  # Identity and user management
├── Logging/                   # Comprehensive logging framework
│   ├── ILogger.cs             # Main logging interface
│   ├── LogEntry.cs            # Log entry structure
│   ├── LogLevel.cs            # Log level enumeration
│   ├── EventId.cs             # Event correlation utilities
│   └── ILoggerTarget.cs       # Log target abstractions
├── Package/                   # Package management utilities
├── Repositories/              # Repository pattern implementations
├── Security/                  # Security and permission utilities
│   ├── PermissionLevel.cs     # Permission level definitions
│   ├── ConnectionLimitType.cs # Connection limiting types
│   └── RequestLimitType.cs    # Request throttling types
└── Serialization/             # Serialization utilities
```

## Core Components

### Logging System

The logging system provides hierarchical logging levels with event correlation:

```csharp
public interface ILogger
{
    void Meta(string message);     // Metadata/configuration info
    void Trace(string message);   // Detailed diagnostics
    void Debug(string message);   // Development debugging
    void Info(string message);    // Normal application flow
    void Warn(string message);    // Potential issues
    void Error(string message);   // Handled exceptions
    void Fatal(string message);   // Critical errors
}
```

**Log Levels (by priority):**
- **Meta**: System configuration and setup information
- **Trace**: Detailed diagnostic information
- **Debug**: Development and troubleshooting information
- **Info**: Normal application flow information
- **Warn**: Potentially harmful situations
- **Error**: Error events that might still allow the application to continue
- **Fatal**: Critical errors that may cause the application to fail

### Memory Management

Efficient buffer pooling system for high-performance scenarios:

```csharp
public interface IBufferPool
{
    // Buffer pool operations for memory optimization
}

public interface IPoolable
{
    // Poolable object lifecycle management
}
```

### Security Framework

Comprehensive security utilities for access control and rate limiting:

```csharp
public enum PermissionLevel
{
    // User permission levels
}

public enum ConnectionLimitType
{
    // Connection limiting strategies
}

public enum RequestLimitType
{
    // Request throttling types
}
```

## Usage Examples

### Basic Logging

```csharp
using Nalix.Common.Logging;

public class ExampleService
{
    private readonly ILogger _logger;
    
    public ExampleService(ILogger logger)
    {
        _logger = logger;
    }
    
    public void ProcessData()
    {
        _logger.Info("Starting data processing");
        
        try
        {
            // Process data
            _logger.Debug("Processing step completed");
        }
        catch (Exception ex)
        {
            _logger.Error("Error processing data", ex);
        }
        
        _logger.Info("Data processing completed");
    }
}
```

### Event Correlation

```csharp
using Nalix.Common.Logging;

public void ProcessWithCorrelation()
{
    var eventId = new EventId(1001, "DataProcessing");
    
    _logger.Info("Starting operation", eventId);
    _logger.Debug("Operation details", eventId);
    _logger.Info("Operation completed", eventId);
}
```

### Security Integration

```csharp
using Nalix.Common.Security;

public class SecurityService
{
    public bool CheckPermission(PermissionLevel required)
    {
        // Permission checking logic
        return true; // or false
    }
    
    public bool CheckConnectionLimit(ConnectionLimitType limitType)
    {
        // Connection limit checking logic
        return true; // or false
    }
}
```

## Integration with Other Nalix Components

### Nalix.Cryptography Integration
```csharp
// Nalix.Common provides the foundation for cryptographic operations
// while Nalix.Cryptography implements the actual algorithms
```

### Nalix.Network Integration
```csharp
// Connection management utilities support network operations
// Logging provides comprehensive network event tracking
```

### Nalix.Shared Integration
```csharp
// Common utilities support shared serialization and configuration
// Logging integrates with shared event systems
```

## Performance Considerations

### High-Performance Features
- **Buffer Pooling**: Reduces garbage collection pressure
- **Unsafe Code**: Optimized memory operations where beneficial
- **Efficient Logging**: Minimal overhead logging framework
- **Memory Management**: Optimized for high-throughput scenarios

### Best Practices
1. **Use Buffer Pools**: For frequently allocated/deallocated objects
2. **Structured Logging**: Use event IDs for correlation
3. **Appropriate Log Levels**: Choose correct levels for performance
4. **Memory Efficiency**: Leverage pooling for large objects

## Dependencies

- **.NET 9.0**: Modern C# 13 features and performance improvements
- **System Libraries**: Core .NET framework components
- **No External Dependencies**: Pure .NET implementation

## Thread Safety

- **Logging**: Thread-safe logging operations
- **Buffer Pools**: Thread-safe buffer management
- **Security**: Thread-safe permission checking

## Error Handling

### Custom Exception Types
```csharp
// Custom exceptions for specific error scenarios
// Integrated with logging system for comprehensive error tracking
```

### Error Recovery
```csharp
// Structured error handling with logging integration
// Graceful degradation strategies
```

## Configuration

### Logger Configuration
```csharp
// Configure logging targets and levels
// Event correlation settings
// Performance optimization settings
```

### Security Configuration
```csharp
// Permission level mappings
// Connection limit settings
// Request throttling configuration
```

## API Reference

### Core Interfaces
- `ILogger`: Main logging interface
- `IBufferPool`: Buffer pool management
- `IPoolable`: Poolable object lifecycle
- `ILoggerTarget`: Log output destinations
- `ILoggerFormatter`: Log message formatting

### Data Structures
- `LogEntry`: Log entry representation
- `EventId`: Event correlation identifier
- `LogLevel`: Logging level enumeration

### Enumerations
- `PermissionLevel`: User permission levels
- `ConnectionLimitType`: Connection limiting strategies
- `RequestLimitType`: Request throttling types

## Version History

### Version 1.4.3 (Current)
- Initial release of Nalix.Common
- Comprehensive logging framework
- Memory management utilities
- Security and permission system
- Repository pattern implementations
- High-performance optimizations

## Contributing

When contributing to Nalix.Common:

1. **Follow SOLID Principles**: Single responsibility, open/closed, etc.
2. **Maintain Performance**: Keep high-performance characteristics
3. **Thread Safety**: Ensure thread-safe implementations
4. **Documentation**: Comprehensive XML documentation
5. **Testing**: Unit tests for all public APIs

## License

Nalix.Common is licensed under the Apache License, Version 2.0.