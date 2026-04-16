using System;
using System.ComponentModel.DataAnnotations;
using Nalix.Common.Exceptions;
using Nalix.Common.Networking.Packets;
using Nalix.Runtime.Dispatching;
using Nalix.Runtime.Options;
using Xunit;

namespace Nalix.Runtime.Tests;

public sealed class RuntimeOptionsAndMetadataTests
{
    [Fact]
    public void DispatchOptionsValidate_WhenDefaultOptions_AcceptsConfiguration()
    {
        DispatchOptions options = new();

        Exception? ex = Record.Exception(options.Validate);

        Assert.Null(ex);
        Assert.Equal(4096, options.MaxPerConnectionQueue);
    }

    [Fact]
    public void DispatchOptionsValidate_WhenMaxPerConnectionQueueNegative_ThrowsValidationException()
    {
        DispatchOptions options = new()
        {
            MaxPerConnectionQueue = -1
        };

        _ = Assert.Throws<ValidationException>(options.Validate);
    }

    [Fact]
    public void PoolingOptionsValidate_WhenPreallocateExceedsCapacity_ThrowsValidationException()
    {
        PoolingOptions options = new()
        {
            PacketContextCapacity = 8,
            PacketContextPreallocate = 9
        };

        ValidationException ex = Assert.Throws<ValidationException>(options.Validate);

        Assert.Contains("cannot exceed", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PoolingOptionsValidate_WhenValuesAreValid_AcceptsConfiguration()
    {
        PoolingOptions options = new()
        {
            PacketContextCapacity = 64,
            PacketContextPreallocate = 16
        };

        Exception? ex = Record.Exception(options.Validate);

        Assert.Null(ex);
    }

    [Fact]
    public void PacketMetadataBuilderBuild_WhenOpcodeMissing_ThrowsInternalErrorException()
    {
        PacketMetadataBuilder builder = new();

        InternalErrorException ex = Assert.Throws<InternalErrorException>(builder.Build);

        Assert.Contains("requires a non-null Opcode", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PacketMetadataBuilderAdd_WhenSameCustomAttributeTypeAdded_LastValueWins()
    {
        PacketMetadataBuilder builder = new()
        {
            Opcode = new PacketOpcodeAttribute(100)
        };

        TestMarkerAttribute first = new("first");
        TestMarkerAttribute second = new("second");
        builder.Add(first);
        builder.Add(second);

        TestMarkerAttribute? fromBuilder = builder.Get<TestMarkerAttribute>();
        PacketMetadata metadata = builder.Build();
        TestMarkerAttribute? fromMetadata = metadata.GetCustomAttribute<TestMarkerAttribute>();

        Assert.NotNull(fromBuilder);
        Assert.NotNull(fromMetadata);
        Assert.Equal("second", fromBuilder!.Value);
        Assert.Equal("second", fromMetadata!.Value);
    }

    [Fact]
    public void PacketMetadataBuilderBuild_WhenBuilderMutatesAfterBuild_MetadataRemainsImmutableSnapshot()
    {
        PacketMetadataBuilder builder = new()
        {
            Opcode = new PacketOpcodeAttribute(200)
        };
        builder.Add(new TestMarkerAttribute("before"));

        PacketMetadata built = builder.Build();

        builder.Add(new TestMarkerAttribute("after"));
        TestMarkerAttribute? fromBuilt = built.GetCustomAttribute<TestMarkerAttribute>();
        TestMarkerAttribute? fromBuilder = builder.Get<TestMarkerAttribute>();

        Assert.NotNull(fromBuilt);
        Assert.NotNull(fromBuilder);
        Assert.Equal("before", fromBuilt!.Value);
        Assert.Equal("after", fromBuilder!.Value);
    }

    private sealed class TestMarkerAttribute(string value) : Attribute
    {
        public string Value { get; } = value;
    }
}
