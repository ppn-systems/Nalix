# Nalix.Logging - Testing Guide

## Overview

This document provides guidance for testing the enhanced Nalix.Logging system. Test implementations are provided as examples that can be integrated when xUnit test infrastructure is available.

## Test Coverage Areas

### 1. Circuit Breaker Tests

#### CircuitBreakerStateTests
- State transitions (Closed → Open → HalfOpen → Closed)
- Failure threshold behavior
- Success threshold in HalfOpen state
- Exponential backoff calculation
- Diagnostics output
- Thread safety

#### CircuitBreakerLogTargetTests
- Call blocking when circuit is open
- Error callback invocation
- Metrics tracking (calls attempted, succeeded, failed, blocked)
- Integration with inner target
- Disposal behavior

### 2. Retry Policy Tests

#### ExponentialBackoffRetryPolicyTests
- Exponential delay calculation
- Jitter application
- Max delay capping
- Transient exception detection
- Retry attempt counting
- Async execution

#### FixedDelayRetryPolicyTests
- Fixed delay behavior
- Max retry attempts
- Exception filtering
- Synchronous and asynchronous execution

### 3. Health Monitoring Tests

#### LoggingHealthCheckTests
- Health status calculation (Healthy, Degraded, Unhealthy)
- Throughput calculation
- Error rate calculation
- Queue depth monitoring
- Metrics window cleanup
- Periodic health checks

### 4. Security Tests

#### LogSecurityTests
- Control character sanitization
- Credit card redaction
- SSN redaction
- Email partial redaction
- Password value redaction
- Sensitive keyword detection
- Input safety validation
- Log injection prevention

### 5. Structured Error Context Tests

#### StructuredErrorContextTests
- Correlation ID generation
- Metadata capture
- Thread information capture
- Retry increment behavior
- ToString formatting
- Error categorization

### 6. Performance Tests

#### StringBuilderPoolTests
- Rent and return behavior
- Capacity limits
- Thread-local caching
- Action-based rent

## Sample Test Implementation

The following test files are provided as templates (located in tests/Nalix.Logging.Tests/):

- `Core/CircuitBreakerStateTests.cs` - Circuit breaker state machine tests
- `Security/LogSecurityTests.cs` - Security sanitization and redaction tests

## Manual Testing Scenarios

### Scenario 1: Circuit Breaker Under Load

```csharp
// Test circuit breaker behavior under simulated failures
var failingTarget = new ThrowingLogTarget();
var protectedTarget = failingTarget.WithCircuitBreaker(new CircuitBreakerOptions
{
    FailureThreshold = 3,
    OpenDuration = TimeSpan.FromSeconds(5)
});

for (int i = 0; i < 10; i++)
{
    try
    {
        protectedTarget.Publish(new LogEntry(LogLevel.Info, EventId.Empty, "Test"));
        Console.WriteLine($"{i}: Success");
    }
    catch
    {
        Console.WriteLine($"{i}: Failed");
    }
    
    Thread.Sleep(1000);
}

Console.WriteLine(protectedTarget.GetDiagnostics());
```

Expected behavior:
- First 3 attempts fail and record failures
- Circuit opens after 3rd failure
- Attempts 4-8 are blocked (fail immediately)
- After 5 seconds, circuit moves to HalfOpen
- Next attempt tests recovery

### Scenario 2: Sensitive Data Redaction

```csharp
var logger = new NLogix();

// Test various sensitive data patterns
logger.Info("Credit card: 1234-5678-9012-3456");  // Should be redacted
logger.Info("SSN: 123-45-6789");                   // Should be redacted
logger.Info("Email: user@example.com");             // Should be partially redacted
logger.Info("password=secret123");                  // Should be redacted
logger.Info("Safe message with no sensitive data"); // Should pass through

// Verify redaction in log output
```

### Scenario 3: Health Monitoring

```csharp
var logger = new NLogix(options =>
{
    options.RegisterTarget(new FileLogTarget());
});

// Get distributor and create health check
var healthCheck = /* create health check */;

// Generate load
for (int i = 0; i < 1000; i++)
{
    logger.Info($"Message {i}");
    
    if (i % 100 == 0)
    {
        var status = healthCheck.CheckHealth();
        Console.WriteLine($"Health: {status}");
        Console.WriteLine($"Throughput: {healthCheck.ThroughputPerSecond:F1}/s");
        Console.WriteLine($"Error Rate: {healthCheck.ErrorRatePercent:F1}%");
    }
}

Console.WriteLine(healthCheck.GetDiagnostics());
```

### Scenario 4: Retry Policy

```csharp
var retryPolicy = RetryPolicyExtensions.CreateExponentialBackoff(options =>
{
    options.MaxRetryAttempts = 3;
    options.InitialDelay = TimeSpan.FromMilliseconds(100);
});

int attempts = 0;
retryPolicy.Execute(() =>
{
    attempts++;
    Console.WriteLine($"Attempt {attempts}");
    
    if (attempts < 3)
    {
        throw new IOException("Transient failure");
    }
}, onRetry: (ex, attempt) =>
{
    Console.WriteLine($"Retrying after: {ex.Message}");
});

Console.WriteLine($"Succeeded after {attempts} attempts");
```

## Performance Benchmarks

### Benchmark 1: Circuit Breaker Overhead

Compare throughput with and without circuit breaker:

```csharp
// Without circuit breaker
var target = new FileLogTarget();
MeasureThroughput(target, iterations: 10000);

// With circuit breaker
var protectedTarget = target.WithCircuitBreaker();
MeasureThroughput(protectedTarget, iterations: 10000);

// Expected: < 5% overhead for circuit breaker
```

### Benchmark 2: Sanitization Performance

Measure performance impact of security sanitization:

```csharp
var messages = GenerateMessages(count: 10000);

// Without sanitization
var sw1 = Stopwatch.StartNew();
foreach (var msg in messages)
{
    ProcessMessage(msg);
}
sw1.Stop();

// With sanitization
var sw2 = Stopwatch.StartNew();
foreach (var msg in messages)
{
    var sanitized = LogSecurity.SanitizeLogMessage(msg);
    ProcessMessage(sanitized);
}
sw2.Stop();

Console.WriteLine($"Without: {sw1.ElapsedMilliseconds}ms");
Console.WriteLine($"With: {sw2.ElapsedMilliseconds}ms");
// Expected: < 10% overhead for sanitization
```

### Benchmark 3: Memory Pooling

Compare allocations with and without StringBuilder pooling:

```csharp
// Without pooling
long allocsBefore = GC.GetAllocatedBytesForCurrentThread();
for (int i = 0; i < 10000; i++)
{
    var sb = new StringBuilder();
    sb.Append("Log message ");
    sb.Append(i);
    _ = sb.ToString();
}
long allocsWithout = GC.GetAllocatedBytesForCurrentThread() - allocsBefore;

// With pooling
allocsBefore = GC.GetAllocatedBytesForCurrentThread();
for (int i = 0; i < 10000; i++)
{
    _ = StringBuilderPool.Rent(sb =>
    {
        sb.Append("Log message ");
        sb.Append(i);
    });
}
long allocsWith = GC.GetAllocatedBytesForCurrentThread() - allocsBefore;

Console.WriteLine($"Without pooling: {allocsWithout:N0} bytes");
Console.WriteLine($"With pooling: {allocsWith:N0} bytes");
Console.WriteLine($"Reduction: {(1 - (double)allocsWith / allocsWithout) * 100:F1}%");
// Expected: 30-50% reduction in allocations
```

## Integration Tests

### Test 1: End-to-End Logging with All Features

```csharp
var logger = new NLogix(options =>
{
    options.SetMinimumLevel(LogLevel.Info);
    
    options.RegisterTargetWithCircuitBreaker(
        new FileLogTarget(),
        new CircuitBreakerOptions
        {
            FailureThreshold = 5,
            OpenDuration = TimeSpan.FromSeconds(30)
        });
});

// Generate various log messages
logger.Info("Normal message");
logger.Warning("Warning with sensitive data: password=secret");
logger.Error("Error with credit card: 1234-5678-9012-3456");

// Verify:
// 1. All messages are written
// 2. Sensitive data is redacted
// 3. Circuit breaker remains closed
// 4. No exceptions thrown
```

### Test 2: Failure Recovery Scenario

```csharp
// Simulate transient disk failures
var fileTarget = new FileLogTarget();
var protectedTarget = fileTarget.WithCircuitBreaker();

// Cause failures
SimulateDiskFailure();
for (int i = 0; i < 10; i++)
{
    protectedTarget.Publish(CreateLogEntry());
}

// Allow recovery
RestoreDiskAccess();
Thread.Sleep(TimeSpan.FromSeconds(35)); // Wait for circuit to reset

// Verify recovery
for (int i = 0; i < 10; i++)
{
    protectedTarget.Publish(CreateLogEntry());
}

// Expected: Circuit opens, then recovers
```

## Test Data Generators

### Generate Test Messages

```csharp
public static string[] GenerateTestMessages(int count)
{
    var messages = new string[count];
    var random = new Random();
    
    for (int i = 0; i < count; i++)
    {
        messages[i] = random.Next(4) switch
        {
            0 => $"Normal message {i}",
            1 => $"Message with CC: {GenerateFakeCreditCard()}",
            2 => $"Message with email: user{i}@example.com",
            3 => $"Message with password: password={GenerateFakePassword()}",
            _ => $"Default message {i}"
        };
    }
    
    return messages;
}
```

## Continuous Integration

When setting up CI/CD:

1. Run all unit tests
2. Run integration tests
3. Run performance benchmarks (with baseline comparison)
4. Run security tests (sensitive data detection)
5. Measure code coverage (target: >90%)
6. Generate test reports

## Test Checklist

- [ ] Circuit breaker state transitions work correctly
- [ ] Circuit breaker exponential backoff functions properly
- [ ] Retry policies handle transient failures
- [ ] Health checks report accurate metrics
- [ ] Sensitive data is detected and redacted
- [ ] Log injection attacks are prevented
- [ ] Memory pooling reduces allocations
- [ ] Performance overhead is acceptable (<10%)
- [ ] All features work together without conflicts
- [ ] Backward compatibility is maintained
- [ ] Error handling is robust
- [ ] Thread safety is guaranteed

## Notes

- Test files are provided as templates in `tests/Nalix.Logging.Tests/`
- To use tests, add xUnit dependencies to test project
- Run tests in isolation to avoid interference
- Use deterministic test data for reliable results
- Mock external dependencies for unit tests
- Use actual components for integration tests
