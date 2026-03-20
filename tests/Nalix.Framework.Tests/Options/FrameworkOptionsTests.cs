// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel.DataAnnotations;
using Nalix.Framework.Options;
using Xunit;

namespace Nalix.Framework.Tests.Options;

public sealed class FrameworkOptionsTests
{
    [Fact]
    public void CompressionOptionsDefaultsAndValidateAreValid()
    {
        CompressionOptions options = new();

        Assert.True(options.Enabled);
        Assert.Equal(1024, options.MinSizeToCompress);
        options.Validate();
    }

    [Fact]
    public void CompressionOptionsWhenMinSizeIsNegativeValidateThrowsValidationException()
    {
        CompressionOptions options = new()
        {
            MinSizeToCompress = -1
        };

        Assert.Throws<ValidationException>(options.Validate);
    }

    [Fact]
    public void TaskManagerOptionsWhenCleanupIntervalIsTooSmallValidateThrows()
    {
        TaskManagerOptions options = new()
        {
            CleanupInterval = TimeSpan.FromMilliseconds(999)
        };

        Assert.Throws<ValidationException>(options.Validate);
    }

    [Fact]
    public void TaskManagerOptionsWhenCleanupIntervalIsOneSecondValidateSucceeds()
    {
        TaskManagerOptions options = new()
        {
            CleanupInterval = TimeSpan.FromSeconds(1)
        };

        options.Validate();
    }

    [Fact]
    public void TaskManagerOptionsWhenHighCpuIsLessThanLowCpuValidateThrows()
    {
        TaskManagerOptions options = new()
        {
            ThresholdHighCpu = 30,
            ThresholdLowCpu = 50
        };

        Assert.Throws<ValidationException>(options.Validate);
    }

    [Fact]
    public void FragmentOptionsWhenChunkSizeIsNonPositiveValidateThrows()
    {
        FragmentOptions options = new()
        {
            MaxPayloadSize = 1024,
            MaxChunkSize = 0
        };

        Assert.Throws<ValidationException>(options.Validate);
    }

    [Fact]
    public void FragmentOptionsWhenPayloadIsSmallerThanChunkValidateThrows()
    {
        FragmentOptions options = new()
        {
            MaxPayloadSize = 1000,
            MaxChunkSize = 1400
        };

        Assert.Throws<ValidationException>(options.Validate);
    }

    [Fact]
    public void FragmentOptionsWhenComputedChunkCountExceedsWireLimitValidateThrows()
    {
        FragmentOptions options = new()
        {
            MaxPayloadSize = ushort.MaxValue + 1,
            MaxChunkSize = 1
        };

        Assert.Throws<ValidationException>(options.Validate);
    }

    [Fact]
    public void FragmentOptionsWhenFrameSizeExceedsWireLimitValidateThrows()
    {
        FragmentOptions options = new()
        {
            MaxPayloadSize = 100_000,
            MaxChunkSize = ushort.MaxValue
        };

        Assert.Throws<ValidationException>(options.Validate);
    }

    [Fact]
    public void SecurityOptionsDataAnnotationRangeAcceptsBoundaryValues()
    {
        SecurityOptions min = new() { Pbkdf2Iterations = 1000 };
        SecurityOptions max = new() { Pbkdf2Iterations = 10_000_000 };

        Validator.ValidateObject(min, new ValidationContext(min), validateAllProperties: true);
        Validator.ValidateObject(max, new ValidationContext(max), validateAllProperties: true);
    }

    [Fact]
    public void SecurityOptionsDataAnnotationRangeRejectsOutOfRangeValues()
    {
        SecurityOptions options = new() { Pbkdf2Iterations = 999 };

        Assert.Throws<ValidationException>(() =>
            Validator.ValidateObject(options, new ValidationContext(options), validateAllProperties: true));
    }

    [Fact]
    public void WorkerOptionsDefaultsMatchExpectedValues()
    {
        WorkerOptions options = new();

        Assert.Equal((ushort)1, options.MachineId);
        Assert.Null(options.Tag);
        Assert.True(options.RetainFor.HasValue);
        Assert.Equal(TimeSpan.FromMinutes(2), options.RetainFor.Value);
        Assert.Null(options.GroupConcurrencyLimit);
        Assert.False(options.TryAcquireSlotImmediately);
        Assert.False(options.CancellationToken.CanBeCanceled);
        Assert.Null(options.OnCompleted);
        Assert.Null(options.OnFailed);
    }

    [Fact]
    public void RecurringOptionsDefaultsMatchExpectedValues()
    {
        RecurringOptions options = new();

        Assert.Null(options.Tag);
        Assert.True(options.NonReentrant);
        Assert.Equal(TimeSpan.FromMilliseconds(250), options.Jitter);
        Assert.Null(options.ExecutionTimeout);
        Assert.Equal(1, options.FailuresBeforeBackoff);
        Assert.Equal(TimeSpan.FromSeconds(15), options.BackoffCap);
    }

    [Fact]
    public void SnowflakeOptionsDefaultsAndSettableMachineIdWorkAsExpected()
    {
        SnowflakeOptions defaults = new();
        SnowflakeOptions custom = new() { MachineId = 512 };

        Assert.Equal((ushort)1, defaults.MachineId);
        Assert.Equal((ushort)512, custom.MachineId);
    }
}
