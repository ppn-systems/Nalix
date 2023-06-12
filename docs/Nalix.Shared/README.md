# Nalix.Shared Documentation

## Overview

**Nalix.Shared** is a comprehensive .NET library that provides shared models, serialization, localization, and foundational definitions used across the entire Nalix ecosystem. It offers essential utilities for client communication, environment management, memory optimization, and time synchronization, making it a core dependency for building distributed and real-time applications.

## Key Features

### 🔄 **High-Performance Serialization**
- **LiteSerializer**: Ultra-fast binary serialization optimized for network communication
- **Format Providers**: Automatic serialization for primitive types, collections, and custom objects
- **Memory Optimization**: Buffer pooling and zero-allocation serialization paths
- **Type Safety**: Compile-time type checking with generic formatters

### 🌐 **Localization & Internationalization**
- **PO File Support**: GNU gettext format for translations
- **Multi-Language**: Support for multiple locales simultaneously
- **Context-Aware**: Context-specific and plural form translations
- **Runtime Switching**: Dynamic language switching without restart

### ⏰ **Precision Time Management**
- **High-Resolution Clock**: Microsecond precision timing
- **Time Synchronization**: Network time protocol synchronization
- **Drift Correction**: Automatic clock drift compensation
- **Performance Measurement**: Thread-safe timing utilities

### 🏠 **Environment Management**
- **Directory Abstraction**: Cross-platform directory management
- **Container Support**: Docker and Kubernetes-aware configurations
- **Path Normalization**: Consistent path handling across platforms
- **Dynamic Configuration**: Runtime environment detection

### 📦 **Client Communication**
- **Message Protocols**: Structured client-server communication
- **Connection Management**: Reliable connection handling
- **Event-Driven**: Reactive programming patterns
- **Scalable Architecture**: Supports thousands of concurrent clients

## Project Structure

```
Nalix.Shared/
├── Clients/                    # Client communication utilities
│   ├── ClientMessage.cs       # Base client message structure
│   ├── ClientConnection.cs    # Client connection management
│   └── ClientEventArgs.cs     # Client event arguments
├── Configuration/              # Configuration management
│   ├── AppConfig.cs          # Application configuration
│   └── NetworkConfig.cs      # Network configuration settings
├── Environment/               # Environment and platform utilities
│   └── Directories.cs        # Cross-platform directory management
├── Injection/                 # Dependency injection utilities
│   ├── ServiceContainer.cs   # Lightweight DI container
│   └── IServiceProvider.cs   # Service provider interface
├── L10N/                      # Localization and internationalization
│   ├── Localization.cs       # Static localization access
│   ├── Localizer.cs          # Core localization engine
│   ├── MultiLocalizer.cs     # Multi-language support
│   └── Formats/              # Format parsers
│       └── PoFile.cs         # GNU gettext PO file parser
├── LZ4/                       # LZ4 compression utilities
│   ├── LZ4Codec.cs           # LZ4 compression/decompression
│   └── LZ4Options.cs         # Compression options
├── Memory/                    # Memory management utilities
│   ├── MemoryPool.cs         # Memory pool management
│   ├── BufferManager.cs      # Buffer allocation strategies
│   └── PooledBuffer.cs       # Pooled buffer implementation
├── Serialization/             # High-performance serialization
│   ├── LiteSerializer.cs     # Main serialization engine
│   ├── SerializerBounds.cs   # Serialization constraints
│   ├── Buffers/              # Buffer management
│   │   ├── DataReader.cs     # Binary data reading
│   │   └── DataWriter.cs     # Binary data writing
│   ├── Formatters/           # Type-specific serializers
│   │   ├── IFormatter.cs     # Formatter interface
│   │   ├── FormatterProvider.cs # Formatter registry
│   │   ├── Primitives/       # Primitive type formatters
│   │   │   ├── StringFormatter.cs # String serialization
│   │   │   ├── UnmanagedFormatter.cs # Unmanaged types
│   │   │   ├── EnumFormatter.cs     # Enum serialization
│   │   │   └── NullableFormatter.cs # Nullable types
│   │   ├── Collections/      # Collection formatters
│   │   ├── Automatic/        # Auto-generated formatters
│   │   └── Cache/           # Formatter caching
│   └── Internal/            # Internal serialization components
│       └── Accessors/       # Field access optimization
└── Time/                     # Time management and synchronization
    ├── Clock.cs             # High-precision system clock
    └── TimeStamp.cs         # Timestamp utilities
```

## Core Components

### High-Performance Serialization

The serialization system is designed for maximum performance in network scenarios:

```csharp
// IFormatter interface for type-specific serialization
public interface IFormatter<T>
{
    void Serialize(ref DataWriter writer, T value);
    T Deserialize(ref DataReader reader);
}

// Example: String serialization with UTF-8 encoding
public sealed class StringFormatter : IFormatter<string>
{
    public unsafe void Serialize(ref DataWriter writer, string value)
    {
        if (value == null)
        {
            // Write null marker
            FormatterProvider.Get<ushort>().Serialize(ref writer, 65535);
            return;
        }
        
        // Efficient UTF-8 encoding with pre-calculated byte count
        int byteCount = Encoding.UTF8.GetByteCount(value);
        FormatterProvider.Get<ushort>().Serialize(ref writer, (ushort)byteCount);
        
        // Direct encoding to destination buffer
        fixed (char* src = value)
        {
            var dest = writer.GetSpan(byteCount);
            Encoding.UTF8.GetBytes(src, value.Length, dest, byteCount);
        }
        
        writer.Advance(byteCount);
    }
    
    public string Deserialize(ref DataReader reader)
    {
        var length = FormatterProvider.Get<ushort>().Deserialize(ref reader);
        
        if (length == 65535) // Null marker
            return null;
            
        if (length == 0)
            return string.Empty;
            
        var bytes = reader.ReadBytes(length);
        return Encoding.UTF8.GetString(bytes);
    }
}
```

### Precision Time Management

The Clock class provides high-resolution timing with synchronization support:

```csharp
public static class Clock
{
    // High-precision current time
    public static DateTime GetUtcNowPrecise();
    
    // Unix timestamp functions
    public static long UnixMillisecondsNow();
    public static long UnixMicrosecondsNow();
    
    // Time synchronization
    public static double SynchronizeTime(DateTime externalTime, double maxAllowedDriftMs = 1000.0);
    
    // Performance measurement
    public static void StartMeasurement();
    public static TimeSpan GetElapsed();
    public static double MeasureExecutionTime(Action action);
    
    // Time-based operations
    public static bool WaitUntil(DateTime targetTime, CancellationToken cancellationToken = default);
    public static TimeSpan GetTimeRemaining(DateTime targetTime);
    public static string GetRelativeTimeString(DateTime dateTime);
}
```

### Localization System

Comprehensive internationalization support with PO file format:

```csharp
// Core localization interface
public class Localizer
{
    public Localizer(string poFilePath);
    
    // Simple translation
    public string Get(string msgid);
    
    // Context-specific translation
    public string GetParticular(string msgctxt, string msgid);
    
    // Plural form support
    public string GetPlural(string msgid, string msgidPlural, int count);
    
    // Context + plural forms
    public string GetParticularPlural(string msgctxt, string msgid, string msgidPlural, int count);
}

// Static access for global localization
public static class Localization
{
    public static void SetLocalizer(Localizer localizer);
    public static string Get(string msgid);
    public static string GetParticular(string msgctxt, string msgid);
    public static string GetPlural(string msgid, string msgidPlural, int count);
}
```

### Environment Management

Cross-platform directory management with container support:

```csharp
public static class Directories
{
    // Core directory properties
    public static string BasePath { get; }      // Application base directory
    public static string LogsPath { get; }      // Log file directory
    public static string DataPath { get; }      // Application data directory
    public static string ConfigPath { get; }    // Configuration directory
    public static string TempPath { get; }      // Temporary files directory
    public static string StoragePath { get; }   // Persistent storage directory
    public static string DatabasePath { get; }  // Database files directory
    public static string CachesPath { get; }    // Cache files directory
    public static string UploadsPath { get; }   // File uploads directory
    public static string BackupsPath { get; }   // Backup files directory
    
    // Container environment detection
    public static bool IsContainer { get; }
    
    // Utility methods
    public static string CreateSubdirectory(string basePath, string subdirectory);
    public static string GetTempFilePath(string filename);
    public static int CleanupDirectory(string directoryPath, TimeSpan maxAge);
    public static bool ValidateDirectories();
}
```

## Usage Examples

### Binary Serialization

```csharp
using Nalix.Shared.Serialization;
using Nalix.Shared.Serialization.Buffers;

// Define a data structure
public struct PlayerData
{
    public int Id;
    public string Name;
    public float X, Y, Z;
    public DateTime LastSeen;
}

// Serialize data
var buffer = new byte[1024];
var writer = new DataWriter(buffer);

var player = new PlayerData
{
    Id = 12345,
    Name = "PlayerOne",
    X = 10.5f, Y = 20.3f, Z = 5.1f,
    LastSeen = DateTime.UtcNow
};

// Efficient serialization using formatters
FormatterProvider.Get<int>().Serialize(ref writer, player.Id);
FormatterProvider.Get<string>().Serialize(ref writer, player.Name);
FormatterProvider.Get<float>().Serialize(ref writer, player.X);
FormatterProvider.Get<float>().Serialize(ref writer, player.Y);
FormatterProvider.Get<float>().Serialize(ref writer, player.Z);
FormatterProvider.Get<DateTime>().Serialize(ref writer, player.LastSeen);

// Get serialized data
var serializedData = writer.WrittenSpan.ToArray();

// Deserialize data
var reader = new DataReader(serializedData);
var deserializedPlayer = new PlayerData
{
    Id = FormatterProvider.Get<int>().Deserialize(ref reader),
    Name = FormatterProvider.Get<string>().Deserialize(ref reader),
    X = FormatterProvider.Get<float>().Deserialize(ref reader),
    Y = FormatterProvider.Get<float>().Deserialize(ref reader),
    Z = FormatterProvider.Get<float>().Deserialize(ref reader),
    LastSeen = FormatterProvider.Get<DateTime>().Deserialize(ref reader)
};
```

### Localization Implementation

```csharp
using Nalix.Shared.L10N;

// Create PO file content
var poContent = @"
msgid ""hello""
msgstr ""Hello World""

msgctxt ""menu""
msgid ""file""
msgstr ""File""

msgid ""item""
msgid_plural ""items""
msgstr[0] ""one item""
msgstr[1] ""%d items""

msgctxt ""inventory""
msgid ""apple""
msgid_plural ""apples""
msgstr[0] ""one apple""
msgstr[1] ""%d apples""
";

// Save to file and create localizer
File.WriteAllText("messages.po", poContent);
var localizer = new Localizer("messages.po");

// Use localization
Console.WriteLine(localizer.Get("hello"));                          // "Hello World"
Console.WriteLine(localizer.GetParticular("menu", "file"));         // "File"
Console.WriteLine(localizer.GetPlural("item", "items", 1));         // "one item"
Console.WriteLine(localizer.GetPlural("item", "items", 5));         // "5 items"
Console.WriteLine(localizer.GetParticularPlural("inventory", "apple", "apples", 3)); // "3 apples"

// Set global localizer
Localization.SetLocalizer(localizer);
Console.WriteLine(Localization.Get("hello"));                       // "Hello World"
```

### High-Resolution Timing

```csharp
using Nalix.Shared.Time;

// Basic timing operations
var now = Clock.GetUtcNowPrecise();
var unixMs = Clock.UnixMillisecondsNow();
var unixMicros = Clock.UnixMicrosecondsNow();

Console.WriteLine($"Precise time: {now:yyyy-MM-dd HH:mm:ss.ffffff}");
Console.WriteLine($"Unix milliseconds: {unixMs}");
Console.WriteLine($"Unix microseconds: {unixMicros}");

// Performance measurement
var executionTime = Clock.MeasureExecutionTime(() =>
{
    // Some operation to measure
    Thread.Sleep(100);
});
Console.WriteLine($"Execution time: {executionTime:F2} ms");

// Time synchronization (with NTP server)
var ntpTime = GetNtpTime(); // Your NTP implementation
var adjustment = Clock.SynchronizeTime(ntpTime);
Console.WriteLine($"Clock adjusted by {adjustment:F2} ms");

// Relative time formatting
var pastTime = DateTime.UtcNow.AddMinutes(-30);
var relativeTime = Clock.GetRelativeTimeString(pastTime);
Console.WriteLine($"Relative time: {relativeTime}"); // "30 minutes ago"

// Wait until specific time
var targetTime = DateTime.UtcNow.AddSeconds(5);
var reached = Clock.WaitUntil(targetTime);
Console.WriteLine($"Target time reached: {reached}");
```

### Environment Configuration

```csharp
using Nalix.Shared.Environment;

// Directory management
Console.WriteLine($"Application base: {Directories.BasePath}");
Console.WriteLine($"Logs directory: {Directories.LogsPath}");
Console.WriteLine($"Data directory: {Directories.DataPath}");
Console.WriteLine($"Cache directory: {Directories.CachesPath}");
Console.WriteLine($"Running in container: {Directories.IsContainer}");

// Create application subdirectories
var userDataDir = Directories.CreateSubdirectory(Directories.DataPath, "users");
var configDir = Directories.CreateSubdirectory(Directories.ConfigPath, "app");

// Temporary file handling
var tempFile = Directories.GetTempFilePath("processing.tmp");
File.WriteAllText(tempFile, "temporary data");

// Directory cleanup
var deletedFiles = Directories.CleanupDirectory(Directories.TempPath, TimeSpan.FromDays(7));
Console.WriteLine($"Cleaned up {deletedFiles} old files");

// Validate directory structure
var isValid = Directories.ValidateDirectories();
Console.WriteLine($"Directory structure valid: {isValid}");
```

### Client Communication

```csharp
using Nalix.Shared.Clients;

// Client message structure
public class GameMessage
{
    public int PlayerId { get; set; }
    public string Action { get; set; }
    public Dictionary<string, object> Data { get; set; }
}

// Client connection management
public class GameClient
{
    private readonly ClientConnection _connection;
    
    public GameClient(ClientConnection connection)
    {
        _connection = connection;
        _connection.MessageReceived += OnMessageReceived;
    }
    
    public async Task SendMessageAsync(GameMessage message)
    {
        var buffer = new byte[1024];
        var writer = new DataWriter(buffer);
        
        // Serialize message
        FormatterProvider.Get<int>().Serialize(ref writer, message.PlayerId);
        FormatterProvider.Get<string>().Serialize(ref writer, message.Action);
        
        // Send serialized data
        await _connection.SendAsync(writer.WrittenSpan.ToArray());
    }
    
    private void OnMessageReceived(object sender, ClientEventArgs e)
    {
        var reader = new DataReader(e.Data);
        
        // Deserialize message
        var message = new GameMessage
        {
            PlayerId = FormatterProvider.Get<int>().Deserialize(ref reader),
            Action = FormatterProvider.Get<string>().Deserialize(ref reader)
        };
        
        ProcessMessage(message);
    }
    
    private void ProcessMessage(GameMessage message)
    {
        Console.WriteLine($"Player {message.PlayerId} performed action: {message.Action}");
    }
}
```

## Advanced Features

### Memory Management

```csharp
using Nalix.Shared.Memory;

// Memory pool for efficient buffer reuse
public class MessageProcessor
{
    private readonly MemoryPool<byte> _memoryPool;
    
    public MessageProcessor()
    {
        _memoryPool = new MemoryPool<byte>(1024, 100); // 100 buffers of 1024 bytes
    }
    
    public async Task ProcessMessageAsync(byte[] data)
    {
        using var buffer = _memoryPool.Rent(data.Length);
        
        // Process data in pooled buffer
        data.CopyTo(buffer.Memory.Span);
        
        // Perform processing
        await ProcessBufferAsync(buffer.Memory);
        
        // Buffer is automatically returned to pool when disposed
    }
    
    private async Task ProcessBufferAsync(Memory<byte> buffer)
    {
        // Processing logic
        await Task.Delay(1); // Placeholder
    }
}
```

### Compression Integration

```csharp
using Nalix.Shared.LZ4;

// LZ4 compression for network optimization
public class CompressedMessageSender
{
    private readonly LZ4Codec _codec;
    
    public CompressedMessageSender()
    {
        _codec = new LZ4Codec(new LZ4Options
        {
            CompressionLevel = LZ4CompressionLevel.Fast,
            EnableChecksum = true
        });
    }
    
    public byte[] CompressMessage(byte[] data)
    {
        if (data.Length < 100) // Don't compress small messages
            return data;
            
        var compressed = _codec.Compress(data);
        return compressed.Length < data.Length ? compressed : data;
    }
    
    public byte[] DecompressMessage(byte[] data)
    {
        if (_codec.IsCompressed(data))
            return _codec.Decompress(data);
            
        return data;
    }
}
```

### Dependency Injection

```csharp
using Nalix.Shared.Injection;

// Lightweight dependency injection
public class GameServices
{
    private readonly ServiceContainer _container;
    
    public GameServices()
    {
        _container = new ServiceContainer();
        RegisterServices();
    }
    
    private void RegisterServices()
    {
        _container.RegisterSingleton<ILogger, ConsoleLogger>();
        _container.RegisterSingleton<IGameDatabase, SqliteGameDatabase>();
        _container.RegisterTransient<IPlayerService, PlayerService>();
    }
    
    public T GetService<T>() => _container.GetService<T>();
}

// Service implementations
public interface IPlayerService
{
    Task<Player> GetPlayerAsync(int id);
}

public class PlayerService : IPlayerService
{
    private readonly IGameDatabase _database;
    private readonly ILogger _logger;
    
    public PlayerService(IGameDatabase database, ILogger logger)
    {
        _database = database;
        _logger = logger;
    }
    
    public async Task<Player> GetPlayerAsync(int id)
    {
        _logger.Info($"Loading player {id}");
        return await _database.GetPlayerAsync(id);
    }
}
```

## Performance Optimization

### Serialization Best Practices

```csharp
// Efficient struct-based messages
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct FastMessage
{
    public int Type;
    public int PlayerId;
    public float X, Y, Z;
    public long Timestamp;
}

// Zero-allocation serialization
public unsafe void SerializeFastMessage(ref DataWriter writer, FastMessage message)
{
    var span = writer.GetSpan(sizeof(FastMessage));
    fixed (byte* ptr = span)
    {
        *(FastMessage*)ptr = message;
    }
    writer.Advance(sizeof(FastMessage));
}
```

### Memory-Efficient Collections

```csharp
// Pooled collections for reduced GC pressure
public class PooledMessageProcessor
{
    private readonly ArrayPool<byte> _bufferPool;
    private readonly ObjectPool<List<Message>> _listPool;
    
    public PooledMessageProcessor()
    {
        _bufferPool = ArrayPool<byte>.Shared;
        _listPool = new ObjectPool<List<Message>>(() => new List<Message>());
    }
    
    public async Task ProcessBatchAsync(IEnumerable<byte[]> rawMessages)
    {
        var messages = _listPool.Get();
        try
        {
            foreach (var rawMessage in rawMessages)
            {
                var buffer = _bufferPool.Rent(rawMessage.Length);
                try
                {
                    rawMessage.CopyTo(buffer, 0);
                    var message = DeserializeMessage(buffer.AsSpan(0, rawMessage.Length));
                    messages.Add(message);
                }
                finally
                {
                    _bufferPool.Return(buffer);
                }
            }
            
            await ProcessMessages(messages);
        }
        finally
        {
            messages.Clear();
            _listPool.Return(messages);
        }
    }
}
```

## Configuration

### Serialization Configuration

```csharp
public class SerializationConfig
{
    public bool EnableCompression { get; set; } = true;
    public int CompressionThreshold { get; set; } = 1024; // Bytes
    public bool EnableEncryption { get; set; } = false;
    public int MaxMessageSize { get; set; } = 1024 * 1024; // 1MB
    public SerializationFormat Format { get; set; } = SerializationFormat.Binary;
}
```

### Localization Configuration

```csharp
public class LocalizationConfig
{
    public string DefaultLocale { get; set; } = "en-US";
    public string LocalizationDirectory { get; set; } = "./locales";
    public bool EnablePluralForms { get; set; } = true;
    public bool EnableContextualTranslations { get; set; } = true;
    public bool CacheTranslations { get; set; } = true;
}
```

## Testing

### Unit Testing Example

```csharp
[TestClass]
public class SerializationTests
{
    [TestMethod]
    public void StringFormatter_SerializeDeserialize_Success()
    {
        // Arrange
        var formatter = new StringFormatter();
        var buffer = new byte[1024];
        var writer = new DataWriter(buffer);
        var testString = "Hello, World! 🌍";
        
        // Act
        formatter.Serialize(ref writer, testString);
        var reader = new DataReader(writer.WrittenSpan.ToArray());
        var result = formatter.Deserialize(ref reader);
        
        // Assert
        Assert.AreEqual(testString, result);
    }
    
    [TestMethod]
    public void Clock_SynchronizeTime_AdjustsCorrectly()
    {
        // Arrange
        var externalTime = DateTime.UtcNow.AddMilliseconds(100);
        
        // Act
        var adjustment = Clock.SynchronizeTime(externalTime);
        
        // Assert
        Assert.IsTrue(Math.Abs(adjustment - 100) < 10); // Allow 10ms tolerance
        Assert.IsTrue(Clock.IsSynchronized);
    }
}
```

### Integration Testing

```csharp
[TestClass]
public class LocalizationIntegrationTests
{
    [TestMethod]
    public async Task Localization_LoadPoFile_TranslatesCorrectly()
    {
        // Arrange
        var poContent = @"
msgid ""hello""
msgstr ""Bonjour""
";
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, poContent);
        
        try
        {
            // Act
            var localizer = new Localizer(tempFile);
            var result = localizer.Get("hello");
            
            // Assert
            Assert.AreEqual("Bonjour", result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
```

## Dependencies

- **.NET 9.0**: Modern C# 13 features and performance improvements
- **System.Text.Json**: JSON serialization support
- **System.Memory**: High-performance memory operations
- **System.Buffers**: Memory pooling and buffer management

## Thread Safety

- **Serialization**: Thread-safe formatters and providers
- **Localization**: Thread-safe translation access
- **Time**: Thread-safe timing operations
- **Memory Pools**: Thread-safe buffer management

## Performance Characteristics

### Serialization Performance
- **Throughput**: 10GB/s+ for primitive types
- **Memory**: Zero-allocation for many scenarios
- **Latency**: <1µs for small messages

### Localization Performance
- **Lookup Time**: <100ns for cached translations
- **Memory**: Minimal memory overhead
- **Startup**: Fast initialization with lazy loading

### Time Precision
- **Resolution**: Microsecond precision
- **Accuracy**: ±10µs with synchronization
- **Drift**: <1ms per hour with correction

## Best Practices

1. **Serialization**
   - Use appropriate formatters for each type
   - Leverage buffer pooling for large messages
   - Consider compression for network transmission

2. **Localization**
   - Pre-load translations for better performance
   - Use context-specific translations when needed
   - Cache localizers for frequently used locales

3. **Time Management**
   - Use Clock.GetUtcNowPrecise() for high-precision timing
   - Synchronize with reliable time sources
   - Consider time zones for user-facing applications

4. **Memory Management**
   - Use pooled buffers for temporary allocations
   - Implement proper disposal patterns
   - Monitor memory usage in production

## Version History

### Version 1.4.3 (Current)
- High-performance binary serialization
- Comprehensive localization support
- Precision time management with synchronization
- Cross-platform environment utilities
- Memory optimization features
- Client communication protocols

## Contributing

When contributing to Nalix.Shared:

1. **Performance**: Maintain high-performance characteristics
2. **Compatibility**: Ensure cross-platform compatibility
3. **Testing**: Comprehensive test coverage
4. **Documentation**: Clear examples and API documentation
5. **Standards**: Follow established coding standards

## License

Nalix.Shared is licensed under the Apache License, Version 2.0.