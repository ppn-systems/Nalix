# Nalix.Logging - Enhanced Features Guide

## Overview

Nalix.Logging now includes advanced features for production-ready logging systems:

- **Circuit Breaker Pattern** - Prevents cascading failures
- **Retry Policies** - Handles transient failures automatically
- **Health Monitoring** - Tracks system health and performance metrics
- **Security Enhancements** - Sanitizes and redacts sensitive data
- **Performance Optimizations** - Memory pooling and lock-free structures

## Circuit Breaker Pattern

Circuit breakers prevent cascading failures by temporarily stopping calls to failing targets.

### Basic Usage

```csharp
using Nalix.Logging.Extensions;
using Nalix.Logging.Sinks;

// Wrap any target with circuit breaker
var fileTarget = new FileLogTarget();
var protectedTarget = fileTarget.WithCircuitBreaker();

// Register with logging
var logger = new NLogix(options => options.RegisterTarget(protectedTarget));
```

### Advanced Configuration

```csharp
var circuitBreakerOptions = new CircuitBreakerOptions
{
    FailureThreshold = 5,           // Open after 5 failures
    OpenDuration = TimeSpan.FromSeconds(30),
    SuccessThreshold = 2,            // Close after 2 successes
    UseExponentialBackoff = true,
    MaxOpenDuration = TimeSpan.FromMinutes(5)
};

var protectedTarget = target.WithCircuitBreaker(
    circuitBreakerOptions,
    onError: errorContext =>
    {
        Console.WriteLine($"Circuit breaker error: {errorContext.ErrorMessage}");
    });
```

### Integration with NLogix Options

```csharp
var logger = new NLogix(options =>
{
    options.RegisterTargetWithCircuitBreaker(
        new FileLogTarget(),
        circuitBreakerOptions: new CircuitBreakerOptions
        {
            FailureThreshold = 10,
            OpenDuration = TimeSpan.FromSeconds(60)
        });
});
```

## Retry Policies

Retry policies handle transient failures automatically with configurable strategies.

### Exponential Backoff

```csharp
using Nalix.Logging.Core;
using Nalix.Logging.Extensions;

var retryPolicy = RetryPolicyExtensions.CreateExponentialBackoff(options =>
{
    options.MaxRetryAttempts = 3;
    options.InitialDelay = TimeSpan.FromMilliseconds(100);
    options.MaxDelay = TimeSpan.FromSeconds(10);
    options.BackoffMultiplier = 2.0;
});

// Use with synchronous operations
retryPolicy.Execute(() =>
{
    // Your logging operation
}, onRetry: (ex, attempt) =>
{
    Console.WriteLine($"Retry attempt {attempt} after {ex.Message}");
});

// Use with async operations
await retryPolicy.ExecuteAsync(async () =>
{
    // Your async logging operation
    await Task.Delay(100);
});
```

### Fixed Delay

```csharp
var retryPolicy = RetryPolicyExtensions.CreateFixedDelay(options =>
{
    options.MaxRetryAttempts = 5;
    options.InitialDelay = TimeSpan.FromSeconds(1);
});
```

## Health Monitoring

Monitor logging system health with metrics and diagnostics.

### Setup

```csharp
using Nalix.Logging.Core;
using Nalix.Logging.Extensions;

var logger = new NLogix();
// Access the distributor through reflection or DI
var distributor = /* get distributor */;

var healthCheck = distributor.CreateHealthCheck(options =>
{
    options.Enabled = true;
    options.CheckInterval = TimeSpan.FromSeconds(30);
    options.MaxErrorRatePercent = 5.0;
    options.MaxQueueDepth = 1000;
    options.CriticalQueueDepth = 5000;
});

// Check health manually
var status = healthCheck.CheckHealth();
Console.WriteLine($"Status: {status}");
Console.WriteLine($"Throughput: {healthCheck.ThroughputPerSecond:F1} entries/sec");
Console.WriteLine($"Error Rate: {healthCheck.ErrorRatePercent:F1}%");

// Get detailed diagnostics
Console.WriteLine(healthCheck.GetDiagnostics());
```

### Health Status

- **Healthy** - System operating normally
- **Degraded** - Experiencing issues but operational
- **Unhealthy** - Not operational

## Security Features

### Automatic Sanitization

All log messages are automatically sanitized to prevent log injection attacks:

```csharp
logger.Info("User input: \x00\x01dangerous\x02"); 
// Output: "User input: dangerous"
```

### Sensitive Data Redaction

Sensitive data is automatically detected and redacted:

```csharp
logger.Info("Credit card: 1234-5678-9012-3456");
// Output: "Credit card: [REDACTED]"

logger.Info("Email: user@example.com");
// Output: "Email: u***@example.com"

logger.Info("password=secret123");
// Output: "password=[REDACTED]"
```

### Manual Security Checks

```csharp
using Nalix.Logging.Internal.Security;

// Check if input is safe
if (!LogSecurity.IsInputSafe(userInput))
{
    // Handle unsafe input
}

// Manually sanitize
var sanitized = LogSecurity.SanitizeLogMessage(userInput);

// Manually redact sensitive data
var redacted = LogSecurity.RedactSensitiveData(message);
```

## Structured Error Context

Rich error context with metadata for debugging:

```csharp
using Nalix.Logging.Core;

var errorContext = new StructuredErrorContext(
    errorMessage: "Failed to write log entry",
    exception: ex,
    severity: LogLevel.Error,
    targetName: "FileLogTarget",
    category: ErrorCategory.DiskIO,
    retryAttempt: 2,
    elapsedTime: TimeSpan.FromMilliseconds(150),
    metadata: new Dictionary<string, object>
    {
        ["FileName"] = "app.log",
        ["QueueDepth"] = 1500
    });

Console.WriteLine(errorContext.ToString());
// Output includes correlation ID, timestamp, thread info, machine name, etc.
```

## Performance Optimizations

### Memory Pooling

StringBuilder pooling reduces allocations:

```csharp
using Nalix.Logging.Internal.Pooling;

// Automatic pooling
var result = StringBuilderPool.Rent(sb =>
{
    sb.Append("Log message");
    sb.Append(" with details");
});

// Manual pooling
var sb = StringBuilderPool.Rent();
try
{
    sb.Append("Log entry");
    return sb.ToString();
}
finally
{
    StringBuilderPool.Return(sb);
}
```

## Configuration Examples

### Complete Production Setup

```csharp
var logger = new NLogix(options =>
{
    // Set minimum log level
    options.SetMinimumLevel(LogLevel.Info);

    // File target with circuit breaker
    options.RegisterTargetWithCircuitBreaker(
        new FileLogTarget(fileOptions =>
        {
            fileOptions.LogFileName = "app.log";
            fileOptions.MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB
            fileOptions.MaxQueueSize = 10000;
            fileOptions.UseBackgroundThread = true;
        }),
        circuitBreakerOptions: new CircuitBreakerOptions
        {
            FailureThreshold = 5,
            OpenDuration = TimeSpan.FromSeconds(30),
            UseExponentialBackoff = true
        },
        onError: error =>
        {
            // Send to monitoring system
            Console.Error.WriteLine($"[ALERT] {error.ErrorMessage}");
        });

    // Console target without circuit breaker
    options.RegisterTarget(new ConsoleLogTarget());

    // Configure file options
    options.ConfigureFileOptions(fileOpts =>
    {
        fileOpts.Append = true;
        fileOpts.FlushInterval = TimeSpan.FromSeconds(5);
    });
});
```

### Testing Configuration

```csharp
var logger = new NLogix(options =>
{
    options.SetMinimumLevel(LogLevel.Debug);
    
    options.RegisterTarget(new ConsoleLogTarget());
    
    // File target without circuit breaker for deterministic testing
    options.RegisterTarget(new FileLogTarget(fileOptions =>
    {
        fileOptions.LogFileName = "test.log";
        fileOptions.UseBackgroundThread = false; // Synchronous for testing
    }));
});
```

## Best Practices

1. **Use Circuit Breakers** - Wrap all external targets (file, network, database)
2. **Configure Retry Policies** - Use exponential backoff with jitter for network operations
3. **Monitor Health** - Set up health checks with appropriate thresholds
4. **Validate Input** - Use security utilities for user-provided data
5. **Pool Resources** - Use memory pooling for high-throughput scenarios
6. **Set Thresholds** - Configure failure thresholds based on your SLAs
7. **Handle Errors** - Always provide error callbacks for critical logging paths

## Troubleshooting

### Circuit Breaker Keeps Opening

- Check error logs for root cause
- Increase `FailureThreshold` if transient errors are common
- Verify target configuration (file permissions, disk space, etc.)
- Check `OpenDuration` - may need to increase for slow recovery

### High Memory Usage

- Reduce `MaxQueueSize` in file options
- Enable `UseBackgroundThread` for async processing
- Check for memory leaks in custom targets
- Verify StringBuilder pooling is working

### Logs Being Dropped

- Increase `MaxQueueSize`
- Set `BlockWhenQueueFull = true` if you can't afford to lose logs
- Add health check monitoring to detect queue buildup
- Consider multiple targets to distribute load

### Performance Issues

- Enable background thread processing
- Use memory pooling
- Reduce log verbosity (increase MinLevel)
- Optimize formatter and target implementations

## Migration Guide

Existing code continues to work without changes. To use new features:

1. **Add Circuit Breakers** - Wrap existing targets
2. **Enable Health Checks** - Add monitoring to your startup code
3. **Review Security** - Sensitive data redaction is automatic
4. **Optimize** - Use memory pooling for custom formatters

## API Reference

See XML documentation in the source code for complete API details.
