# Nalix Core Documentation

## Overview

**Nalix** is the foundational high-performance .NET library that provides essential utilities for diagnostics, runtime management, secure randomization, unique identification, threading, and reflection. It offers a modular and extensible architecture, making it an ideal choice for building scalable and maintainable applications. This is the core library that ties the entire Nalix ecosystem together.

## Key Features

### 🔧 **Core Utilities**
- **Assembly Management**: Dynamic assembly loading, inspection, and metadata analysis
- **Runtime Diagnostics**: System monitoring, performance profiling, and debugging utilities
- **Reflection Utilities**: High-performance reflection operations and caching
- **Threading Primitives**: Advanced concurrency patterns and thread management
- **Extension Methods**: Enhanced .NET standard functionality

### 🎲 **Advanced Randomization**
- **Cryptographically Secure**: Multiple secure random number generators
- **High Performance**: Optimized algorithms for different use cases
- **Multiple Algorithms**: Support for various PRNG algorithms (MWC, XorShift, etc.)
- **Thread-Safe**: Concurrent access patterns with minimal contention

### 🆔 **Unique Identification**
- **Multiple Formats**: Base32, Base36, Base58, Base64 identifier generation
- **Collision Resistant**: Cryptographically strong unique identifiers
- **Compact Representations**: Space-efficient encoding schemes
- **Sortable IDs**: Time-ordered identifiers for database optimization

### 🏗️ **Architectural Foundation**
- **Modular Design**: Clean separation of concerns
- **High Performance**: Optimized for minimal overhead
- **Memory Efficient**: Careful memory management and pooling
- **Extensible**: Plugin architecture for custom components

## Project Structure

```
Nalix/
├── Assemblies/                # Assembly management and inspection
│   ├── AssemblyInfo.cs        # Assembly metadata utilities
│   └── AssemblyInspector.cs   # Dynamic assembly inspection
├── Extensions/                # Extension methods for .NET types
│   ├── StringExtensions.cs    # String manipulation extensions
│   ├── CollectionExtensions.cs # Collection utility extensions
│   ├── TypeExtensions.cs      # Type reflection extensions
│   └── DateTimeExtensions.cs  # DateTime utility extensions
├── Identifiers/               # Unique identifier generation
│   ├── Base32Id.cs           # Base32 encoded identifiers
│   ├── Base36Id.cs           # Base36 encoded identifiers (alphanumeric)
│   ├── Base58Id.cs           # Base58 encoded identifiers (Bitcoin-style)
│   ├── Base64Id.cs           # Base64 encoded identifiers
│   └── Internal/             # Internal ID generation utilities
├── Interop/                  # Platform interoperability
│   ├── PlatformInfo.cs       # Platform detection and capabilities
│   ├── NativeLibrary.cs      # Native library loading
│   └── MemoryMappedFiles.cs  # Memory-mapped file utilities
├── IO/                       # Input/Output utilities
│   ├── FileSystem.cs         # Enhanced file system operations
│   ├── StreamExtensions.cs   # Stream manipulation utilities
│   └── PathUtils.cs          # Path manipulation and validation
└── Randomization/            # Random number generation
    ├── Rand.cs               # Main random number interface
    ├── RandGenerator.cs      # High-level random generation utilities
    ├── GRandom.cs            # Cryptographically secure random
    └── MwcRandom.cs          # Multiply-with-carry PRNG
```

## Core Components

### Assembly Management

Powerful assembly inspection and management capabilities:

```csharp
public static class AssemblyInspector
{
    // Assembly loading and inspection
    public static Assembly LoadAssembly(string path);
    public static IEnumerable<Type> GetTypesImplementing<T>(Assembly assembly);
    public static IEnumerable<MethodInfo> GetMethodsWithAttribute<T>(Assembly assembly) where T : Attribute;
    
    // Metadata extraction
    public static AssemblyMetadata GetMetadata(Assembly assembly);
    public static IEnumerable<string> GetReferencedAssemblies(Assembly assembly);
    public static bool IsDebugBuild(Assembly assembly);
}

public class AssemblyInfo
{
    public string Name { get; set; }
    public Version Version { get; set; }
    public string Location { get; set; }
    public DateTime CreationTime { get; set; }
    public bool IsDebug { get; set; }
    public IEnumerable<string> Dependencies { get; set; }
}
```

### Unique Identifier Generation

Multiple encoding formats for different use cases:

```csharp
// Base32 identifiers (RFC 4648 compatible)
public static class Base32Id
{
    public static string Generate(int length = 16);
    public static string GenerateTimestamped();
    public static bool IsValid(string id);
}

// Base36 identifiers (alphanumeric)
public static class Base36Id
{
    public static string Generate(int length = 12);
    public static string GenerateFromGuid(Guid guid);
    public static long ToNumeric(string id);
}

// Base58 identifiers (Bitcoin-style, no confusing characters)
public static class Base58Id
{
    public static string Generate(int length = 16);
    public static string EncodeBytes(ReadOnlySpan<byte> data);
    public static byte[] DecodeToBytes(string id);
}

// Base64 identifiers (URL-safe)
public static class Base64Id
{
    public static string Generate(int length = 16);
    public static string GenerateUrlSafe(int length = 16);
    public static string EncodeGuid(Guid guid);
}
```

### Advanced Randomization

High-performance random number generation:

```csharp
// Main random interface
public static class Rand
{
    // Basic random operations
    public static int Next();
    public static int Next(int max);
    public static int Next(int min, int max);
    public static double NextDouble();
    public static float NextFloat();
    
    // Cryptographically secure
    public static void NextBytes(Span<byte> buffer);
    public static string NextString(int length, string alphabet = null);
    public static T NextElement<T>(IList<T> list);
    
    // Advanced operations
    public static void Shuffle<T>(IList<T> list);
    public static T[] Sample<T>(IEnumerable<T> source, int count);
    public static bool NextBool(double probability = 0.5);
}

// Cryptographically secure random generator
public class GRandom : Random
{
    public GRandom();
    public GRandom(int seed);
    
    // Secure random generation
    public override void NextBytes(byte[] buffer);
    public override void NextBytes(Span<byte> buffer);
    public void NextSecureBytes(Span<byte> buffer);
    
    // Cryptographic quality random numbers
    public long NextInt64();
    public ulong NextUInt64();
    public decimal NextDecimal();
}

// Multiply-with-carry PRNG (high performance)
public class MwcRandom : Random
{
    public MwcRandom();
    public MwcRandom(uint seed);
    
    // High-performance generation
    public override int Next();
    public override int Next(int maxValue);
    public override double NextDouble();
    
    // Bulk generation
    public void NextInts(Span<int> buffer);
    public void NextFloats(Span<float> buffer);
}
```

## Usage Examples

### Assembly Inspection

```csharp
using Nalix.Assemblies;

// Load and inspect an assembly
var assembly = AssemblyInspector.LoadAssembly("MyApplication.dll");
var metadata = AssemblyInspector.GetMetadata(assembly);

Console.WriteLine($"Assembly: {metadata.Name}");
Console.WriteLine($"Version: {metadata.Version}");
Console.WriteLine($"Debug Build: {metadata.IsDebug}");

// Find types implementing an interface
var services = AssemblyInspector.GetTypesImplementing<IService>(assembly);
foreach (var serviceType in services)
{
    Console.WriteLine($"Found service: {serviceType.Name}");
}

// Find methods with specific attributes
var endpoints = AssemblyInspector.GetMethodsWithAttribute<HttpGetAttribute>(assembly);
foreach (var method in endpoints)
{
    Console.WriteLine($"Endpoint: {method.DeclaringType.Name}.{method.Name}");
}

// Dynamic type loading and instantiation
var pluginTypes = AssemblyInspector.GetTypesImplementing<IPlugin>(assembly);
foreach (var pluginType in pluginTypes)
{
    if (Activator.CreateInstance(pluginType) is IPlugin plugin)
    {
        plugin.Initialize();
        Console.WriteLine($"Loaded plugin: {plugin.Name}");
    }
}
```

### Unique Identifier Generation

```csharp
using Nalix.Identifiers;

// Generate different types of identifiers
var base32Id = Base32Id.Generate(16);           // "4J2KL9MNP2QR5STU"
var base36Id = Base36Id.Generate(12);           // "9K2L7M8P3Q4R"
var base58Id = Base58Id.Generate(16);           // "9jKmL5pQ3rStU7vX"
var base64Id = Base64Id.GenerateUrlSafe(16);    // "4J_KL9-NP2QR5STU"

Console.WriteLine($"Base32: {base32Id}");
Console.WriteLine($"Base36: {base36Id}");
Console.WriteLine($"Base58: {base58Id}");
Console.WriteLine($"Base64: {base64Id}");

// Timestamped identifiers for sorting
var timestampedId = Base32Id.GenerateTimestamped();
Console.WriteLine($"Timestamped: {timestampedId}");

// Convert between formats
var guid = Guid.NewGuid();
var base36FromGuid = Base36Id.GenerateFromGuid(guid);
var base64FromGuid = Base64Id.EncodeGuid(guid);

Console.WriteLine($"GUID: {guid}");
Console.WriteLine($"Base36 from GUID: {base36FromGuid}");
Console.WriteLine($"Base64 from GUID: {base64FromGuid}");

// Validation
Console.WriteLine($"Base32 valid: {Base32Id.IsValid(base32Id)}");
Console.WriteLine($"Base32 invalid: {Base32Id.IsValid("invalid!")}");
```

### Advanced Randomization

```csharp
using Nalix.Randomization;

// Basic random operations
var randomInt = Rand.Next(1, 100);
var randomDouble = Rand.NextDouble();
var randomBool = Rand.NextBool(0.7); // 70% chance of true

Console.WriteLine($"Random int: {randomInt}");
Console.WriteLine($"Random double: {randomDouble:F4}");
Console.WriteLine($"Random bool: {randomBool}");

// Generate random strings
var randomPassword = Rand.NextString(16, "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*");
var randomApiKey = Rand.NextString(32);

Console.WriteLine($"Password: {randomPassword}");
Console.WriteLine($"API Key: {randomApiKey}");

// Collections operations
var numbers = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
Rand.Shuffle(numbers);
Console.WriteLine($"Shuffled: [{string.Join(", ", numbers)}]");

var sample = Rand.Sample(Enumerable.Range(1, 100), 5);
Console.WriteLine($"Sample: [{string.Join(", ", sample)}]");

var randomElement = Rand.NextElement(new[] { "apple", "banana", "cherry", "date" });
Console.WriteLine($"Random fruit: {randomElement}");

// Cryptographically secure random
using var secureRandom = new GRandom();
var secureBytes = new byte[32];
secureRandom.NextSecureBytes(secureBytes);
Console.WriteLine($"Secure bytes: {Convert.ToHexString(secureBytes)}");

// High-performance random for simulations
using var fastRandom = new MwcRandom();
var values = new int[1000];
fastRandom.NextInts(values);
Console.WriteLine($"Generated {values.Length} random integers");
```

### Platform Detection and Interop

```csharp
using Nalix.Interop;

// Platform information
var platform = PlatformInfo.Current;
Console.WriteLine($"OS: {platform.OperatingSystem}");
Console.WriteLine($"Architecture: {platform.Architecture}");
Console.WriteLine($"Runtime: {platform.RuntimeFramework}");
Console.WriteLine($"Is 64-bit: {platform.Is64Bit}");
Console.WriteLine($"Processor count: {platform.ProcessorCount}");
Console.WriteLine($"Available memory: {platform.AvailableMemoryMB:N0} MB");

// Feature detection
Console.WriteLine($"Supports SIMD: {platform.SupportsSimd}");
Console.WriteLine($"Supports Hardware AES: {platform.SupportsAes}");
Console.WriteLine($"Supports AVX2: {platform.SupportsAvx2}");

// Native library loading
if (platform.OperatingSystem == OSPlatform.Windows)
{
    var lib = NativeLibrary.Load("kernel32.dll");
    // Use native functions
    NativeLibrary.Free(lib);
}
```

### Performance Utilities

```csharp
using Nalix.Extensions;
using Nalix.Randomization;
using System.Diagnostics;

// Performance measurement
public class PerformanceTest
{
    public void RunBenchmarks()
    {
        // Test different random generators
        BenchmarkRandomGenerator("System.Random", () => new Random());
        BenchmarkRandomGenerator("GRandom", () => new GRandom());
        BenchmarkRandomGenerator("MwcRandom", () => new MwcRandom());
    }
    
    private void BenchmarkRandomGenerator(string name, Func<Random> factory)
    {
        const int iterations = 1_000_000;
        
        using var random = factory();
        var sw = Stopwatch.StartNew();
        
        for (int i = 0; i < iterations; i++)
        {
            random.Next();
        }
        
        sw.Stop();
        
        var opsPerSecond = iterations / sw.Elapsed.TotalSeconds;
        Console.WriteLine($"{name}: {opsPerSecond:N0} ops/sec");
    }
}

// Memory efficiency testing
public void TestMemoryUsage()
{
    var before = GC.GetTotalMemory(true);
    
    // Generate lots of identifiers
    var ids = new List<string>();
    for (int i = 0; i < 100_000; i++)
    {
        ids.Add(Base58Id.Generate());
    }
    
    var after = GC.GetTotalMemory(false);
    var used = after - before;
    
    Console.WriteLine($"Memory used for 100k IDs: {used:N0} bytes");
    Console.WriteLine($"Average per ID: {used / 100_000.0:F2} bytes");
}
```

### Extension Methods Usage

```csharp
using Nalix.Extensions;

// String extensions
var text = "Hello, World!";
var hash = text.ToSha256Hash();
var encoded = text.ToBase64();
var isEmail = "user@example.com".IsValidEmail();

Console.WriteLine($"SHA256: {hash}");
Console.WriteLine($"Base64: {encoded}");
Console.WriteLine($"Is email: {isEmail}");

// Collection extensions
var numbers = new[] { 1, 2, 3, 4, 5 };
var chunks = numbers.Chunk(2);
var distinct = numbers.DistinctBy(x => x % 2);

foreach (var chunk in chunks)
{
    Console.WriteLine($"Chunk: [{string.Join(", ", chunk)}]");
}

// Type extensions
var type = typeof(List<string>);
var isGeneric = type.IsGenericType();
var genericArgs = type.GetGenericArguments();
var friendlyName = type.GetFriendlyName();

Console.WriteLine($"Is generic: {isGeneric}");
Console.WriteLine($"Generic args: {string.Join(", ", genericArgs.Select(t => t.Name))}");
Console.WriteLine($"Friendly name: {friendlyName}");

// DateTime extensions
var now = DateTime.UtcNow;
var startOfDay = now.StartOfDay();
var endOfDay = now.EndOfDay();
var isWeekend = now.IsWeekend();
var age = new DateTime(1990, 1, 1).Age();

Console.WriteLine($"Start of day: {startOfDay}");
Console.WriteLine($"End of day: {endOfDay}");
Console.WriteLine($"Is weekend: {isWeekend}");
Console.WriteLine($"Age: {age.TotalDays:F0} days");
```

## Advanced Features

### Custom Random Generators

```csharp
using Nalix.Randomization;

// Create custom random generator
public class CustomRandom : Random
{
    private readonly uint[] _state;
    private int _index;
    
    public CustomRandom(uint seed = 0)
    {
        _state = new uint[4];
        if (seed == 0)
            seed = (uint)Environment.TickCount;
        
        // Initialize state
        _state[0] = seed;
        for (int i = 1; i < 4; i++)
        {
            _state[i] = (uint)(1812433253 * (_state[i - 1] ^ (_state[i - 1] >> 30)) + i);
        }
    }
    
    protected override double Sample()
    {
        return Next() * (1.0 / uint.MaxValue);
    }
    
    public override int Next()
    {
        // XorShift128 algorithm
        uint t = _state[3];
        uint s = _state[0];
        
        _state[3] = _state[2];
        _state[2] = _state[1];
        _state[1] = s;
        
        t ^= t << 11;
        t ^= t >> 8;
        _state[0] = t ^ s ^ (s >> 19);
        
        return (int)(_state[0] & 0x7FFFFFFF);
    }
}

// Register with RandGenerator
RandGenerator.SetDefaultGenerator(() => new CustomRandom());
```

### Assembly Plugin System

```csharp
using Nalix.Assemblies;

public interface IPlugin
{
    string Name { get; }
    string Version { get; }
    void Initialize();
    void Execute();
    void Shutdown();
}

public class PluginManager
{
    private readonly List<IPlugin> _plugins = new();
    
    public void LoadPluginsFromDirectory(string directory)
    {
        var assemblies = Directory.GetFiles(directory, "*.dll")
            .Select(AssemblyInspector.LoadAssembly);
        
        foreach (var assembly in assemblies)
        {
            var pluginTypes = AssemblyInspector.GetTypesImplementing<IPlugin>(assembly);
            
            foreach (var pluginType in pluginTypes)
            {
                if (Activator.CreateInstance(pluginType) is IPlugin plugin)
                {
                    _plugins.Add(plugin);
                    plugin.Initialize();
                    Console.WriteLine($"Loaded plugin: {plugin.Name} v{plugin.Version}");
                }
            }
        }
    }
    
    public void ExecuteAllPlugins()
    {
        foreach (var plugin in _plugins)
        {
            try
            {
                plugin.Execute();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error executing plugin {plugin.Name}: {ex.Message}");
            }
        }
    }
    
    public void Shutdown()
    {
        foreach (var plugin in _plugins)
        {
            plugin.Shutdown();
        }
        _plugins.Clear();
    }
}
```

## Configuration

### Random Generator Configuration

```csharp
public class RandomConfiguration
{
    public RandomGeneratorType DefaultGenerator { get; set; } = RandomGeneratorType.Secure;
    public uint? Seed { get; set; }
    public bool UseHardwareRng { get; set; } = true;
    public int BufferSize { get; set; } = 4096;
    public bool ThreadSafe { get; set; } = true;
}

public enum RandomGeneratorType
{
    System,      // System.Random
    Secure,      // GRandom (cryptographically secure)
    FastMwc,     // MwcRandom (high performance)
    Custom       // User-defined generator
}
```

### Identifier Configuration

```csharp
public class IdentifierConfiguration
{
    public IdentifierFormat DefaultFormat { get; set; } = IdentifierFormat.Base58;
    public int DefaultLength { get; set; } = 16;
    public bool IncludeTimestamp { get; set; } = false;
    public string CustomAlphabet { get; set; } = null;
    public bool UppercaseOnly { get; set; } = false;
}

public enum IdentifierFormat
{
    Base32,
    Base36,
    Base58,
    Base64,
    Guid,
    Custom
}
```

## Performance Characteristics

### Randomization Performance
- **GRandom**: 50M+ values/second (secure)
- **MwcRandom**: 500M+ values/second (fast)
- **Rand static methods**: 100M+ values/second

### Identifier Generation
- **Base32/Base58/Base64**: 10M+ IDs/second
- **Base36**: 15M+ IDs/second
- **Memory allocation**: Minimal (pooled strings)

### Assembly Operations
- **Type scanning**: 1000+ types/second
- **Method resolution**: 10000+ methods/second
- **Attribute lookup**: 5000+ attributes/second

## Dependencies

- **.NET 9.0**: Modern C# 13 features and performance improvements
- **Nalix.Common**: Core interfaces and base functionality
- **System.Security.Cryptography**: Secure random number generation
- **System.Reflection**: Assembly and type inspection

## Thread Safety

- **Randomization**: All generators are thread-safe
- **Identifiers**: Thread-safe generation and validation
- **Assembly operations**: Thread-safe inspection and caching
- **Extensions**: Thread-safe utility methods

## Best Practices

1. **Randomization**
   - Use GRandom for security-sensitive applications
   - Use MwcRandom for high-performance simulations
   - Seed generators appropriately for reproducible results

2. **Identifiers**
   - Choose appropriate format for use case (Base58 for user-facing, Base64 for APIs)
   - Use timestamped IDs for sortable requirements
   - Validate IDs on input boundaries

3. **Assembly Management**
   - Cache assembly inspection results
   - Use WeakReferences for large assemblies
   - Handle assembly loading failures gracefully

4. **Performance**
   - Profile random number generation in hot paths
   - Pool objects where appropriate
   - Use Span<T> for memory-efficient operations

## Version History

### Version 1.4.3 (Current)
- Initial release of Nalix core library
- High-performance randomization with multiple algorithms
- Unique identifier generation in multiple formats
- Assembly inspection and management utilities
- Platform detection and interoperability
- Comprehensive extension methods
- Modular architecture foundation

## Contributing

When contributing to Nalix:

1. **Performance**: Maintain high-performance characteristics
2. **Compatibility**: Ensure cross-platform compatibility
3. **Security**: Follow secure coding practices
4. **Testing**: Include comprehensive unit tests
5. **Documentation**: Provide clear examples and API documentation

## License

Nalix is licensed under the Apache License, Version 2.0.