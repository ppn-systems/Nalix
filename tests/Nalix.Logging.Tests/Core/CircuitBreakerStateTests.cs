// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Logging.Core;
using Nalix.Logging.Options;
using Xunit;

namespace Nalix.Logging.Tests.Core;

/// <summary>
/// Unit tests for CircuitBreakerState class.
/// </summary>
public sealed class CircuitBreakerStateTests
{
    [Fact]
    public void Constructor_WithValidOptions_InitializesInClosedState()
    {
        // Arrange
        var options = new CircuitBreakerOptions();

        // Act
        var state = new CircuitBreakerState(options);

        // Assert
        Assert.Equal(CircuitState.Closed, state.State);
        Assert.Equal(0, state.FailureCount);
        Assert.Equal(0, state.SuccessCount);
        Assert.True(state.IsCallAllowed);
    }

    [Fact]
    public void RecordFailure_WhenReachingThreshold_OpensCircuit()
    {
        // Arrange
        var options = new CircuitBreakerOptions
        {
            FailureThreshold = 3
        };
        var state = new CircuitBreakerState(options);

        // Act
        state.RecordFailure();
        state.RecordFailure();
        Assert.Equal(CircuitState.Closed, state.State);
        
        state.RecordFailure();

        // Assert
        Assert.Equal(CircuitState.Open, state.State);
        Assert.False(state.IsCallAllowed);
    }

    [Fact]
    public void RecordSuccess_InClosedState_ResetsFailureCount()
    {
        // Arrange
        var options = new CircuitBreakerOptions();
        var state = new CircuitBreakerState(options);
        
        state.RecordFailure();
        state.RecordFailure();
        Assert.True(state.FailureCount > 0);

        // Act
        state.RecordSuccess();

        // Assert
        Assert.Equal(0, state.FailureCount);
        Assert.Equal(CircuitState.Closed, state.State);
    }

    [Fact]
    public void GetDiagnostics_ReturnsFormattedInformation()
    {
        // Arrange
        var options = new CircuitBreakerOptions();
        var state = new CircuitBreakerState(options);

        // Act
        var diagnostics = state.GetDiagnostics();

        // Assert
        Assert.Contains("Circuit Breaker State", diagnostics);
        Assert.Contains("Failure Count", diagnostics);
        Assert.Contains("Success Count", diagnostics);
    }

    [Fact]
    public void Constructor_WithInvalidOptions_ThrowsArgumentException()
    {
        // Arrange
        var invalidOptions = new CircuitBreakerOptions
        {
            FailureThreshold = 0 // Invalid
        };

        // Act & Assert
        Assert.Throws<System.ArgumentException>(() => new CircuitBreakerState(invalidOptions));
    }
}
