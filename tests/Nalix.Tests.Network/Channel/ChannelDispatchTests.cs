using Nalix.Common.Package.Enums;
using Nalix.Network.Configurations;
using Nalix.Network.Dispatch.Channel;
using System;
using System.Collections.Generic;
using Xunit;

namespace Nalix.Tests.Network.Channel;

/// <summary>
/// Test suite for the ChannelDispatch class.
/// </summary>
public class ChannelDispatchTests
{
    private static DispatchQueueConfig CreateDefaultConfig()
    {
        return new DispatchQueueConfig
        {
            MaxCapacity = 100,
            EnableMetrics = true,
            EnableValidation = true,
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    [Fact]
    public void Enqueue_ValidPacket_ShouldAddToQueue()
    {
        // Arrange
        var config = CreateDefaultConfig();
        var dispatch = new ChannelDispatch<TestPacket>(config);
        var packet = new TestPacket { Priority = PacketPriority.Medium };

        // Act
        bool result = dispatch.Enqueue(packet);

        // Assert
        Assert.True(result);
        Assert.Equal(1, dispatch.Count);
    }

    [Fact]
    public void Enqueue_NullPacket_ShouldReturnFalse()
    {
        // Arrange
        var config = CreateDefaultConfig();
        var dispatch = new ChannelDispatch<TestPacket>(config);

        // Act
        bool result = dispatch.Enqueue((TestPacket?)null!); // Explicitly cast to resolve ambiguity

        // Assert
        Assert.False(result);
        Assert.Equal(0, dispatch.Count);
    }

    [Fact]
    public void Enqueue_ExceedMaxCapacity_ShouldReturnFalse()
    {
        // Arrange
        var config = CreateDefaultConfig();
        config.MaxCapacity = 2;
        var dispatch = new ChannelDispatch<TestPacket>(config);

        var packet1 = new TestPacket { Priority = PacketPriority.Low };
        var packet2 = new TestPacket { Priority = PacketPriority.Low };
        var packet3 = new TestPacket { Priority = PacketPriority.High };

        // Act
        dispatch.Enqueue(packet1);
        dispatch.Enqueue(packet2);
        bool result = dispatch.Enqueue(packet3);

        // Assert
        Assert.False(result);
        Assert.Equal(2, dispatch.Count);
    }

    [Fact]
    public void Dequeue_ShouldReturnPacketInPriorityOrder()
    {
        // Arrange
        var config = CreateDefaultConfig();
        var dispatch = new ChannelDispatch<TestPacket>(config);

        var lowPriorityPacket = new TestPacket { Priority = PacketPriority.Low };
        var highPriorityPacket = new TestPacket { Priority = PacketPriority.High };

        dispatch.Enqueue(lowPriorityPacket);
        dispatch.Enqueue(highPriorityPacket);

        // Act
        var dequeuedPacket = dispatch.Dequeue();

        // Assert
        Assert.Equal(highPriorityPacket, dequeuedPacket); // High priority should be dequeued first
        Assert.Equal(1, dispatch.Count);
    }

    [Fact]
    public void Dequeue_EmptyQueue_ShouldThrowException()
    {
        // Arrange
        var config = CreateDefaultConfig();
        var dispatch = new ChannelDispatch<TestPacket>(config);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => dispatch.Dequeue());
    }

    [Fact]
    public void TryDequeue_ShouldReturnTrueWhenPacketAvailable()
    {
        // Arrange
        var config = CreateDefaultConfig();
        var dispatch = new ChannelDispatch<TestPacket>(config);
        var packet = new TestPacket { Priority = PacketPriority.Low };

        dispatch.Enqueue(packet);

        // Act
        bool result = dispatch.TryDequeue(out var dequeuedPacket);

        // Assert
        Assert.True(result);
        Assert.Equal(packet, dequeuedPacket);
        Assert.Equal(0, dispatch.Count);
    }

    [Fact]
    public void TryDequeue_EmptyQueue_ShouldReturnFalse()
    {
        // Arrange
        var config = CreateDefaultConfig();
        var dispatch = new ChannelDispatch<TestPacket>(config);

        // Act
        bool result = dispatch.TryDequeue(out var dequeuedPacket);

        // Assert
        Assert.False(result);
        Assert.Null(dequeuedPacket);
    }

    [Fact]
    public void Enqueue_MultiplePackets_ShouldAddAllToQueue()
    {
        // Arrange
        var config = CreateDefaultConfig();
        var dispatch = new ChannelDispatch<TestPacket>(config);

        var packets = new List<TestPacket>
        {
            new() { Priority = PacketPriority.Low },
            new() { Priority = PacketPriority.Medium },
            new() { Priority = PacketPriority.High }
        };

        // Act
        int addedCount = dispatch.Enqueue(packets);

        // Assert
        Assert.Equal(3, addedCount);
        Assert.Equal(3, dispatch.Count);
    }

    [Fact]
    public void DequeueBatch_ShouldReturnUpToLimitPackets()
    {
        // Arrange
        var config = CreateDefaultConfig();
        var dispatch = new ChannelDispatch<TestPacket>(config);

        var packets = new List<TestPacket>
        {
            new() { Priority = PacketPriority.Low },
            new() { Priority = PacketPriority.Medium },
            new() { Priority = PacketPriority.High }
        };

        dispatch.Enqueue(packets);

        // Act
        var dequeuedPackets = dispatch.DequeueBatch(2);

        // Assert
        Assert.Equal(2, dequeuedPackets.Count);
        Assert.Equal(1, dispatch.Count); // One remaining in the queue
    }

    [Fact]
    public void TryRequeue_ShouldAddPacketToSpecifiedPriority()
    {
        // Arrange
        var config = CreateDefaultConfig();
        var dispatch = new ChannelDispatch<TestPacket>(config);

        var packet = new TestPacket { Priority = PacketPriority.Low };

        // Act
        bool result = dispatch.TryRequeue(packet, PacketPriority.High);

        // Assert
        Assert.True(result);
        Assert.Equal(1, dispatch.Count);
        Assert.Equal(packet, dispatch.Dequeue());
    }

    [Fact]
    public void Dequeue_ShouldRespectFIFOForSamePriority()
    {
        // Arrange
        var config = CreateDefaultConfig();
        var dispatch = new ChannelDispatch<TestPacket>(config);

        var packet1 = new TestPacket { Priority = PacketPriority.Medium };
        var packet2 = new TestPacket { Priority = PacketPriority.Medium };

        dispatch.Enqueue(packet1);
        dispatch.Enqueue(packet2);

        // Act
        var firstDequeued = dispatch.Dequeue();
        var secondDequeued = dispatch.Dequeue();

        // Assert
        Assert.Equal(packet1, firstDequeued); // First in, first out
        Assert.Equal(packet2, secondDequeued);
        Assert.Equal(0, dispatch.Count);
    }

    [Fact]
    public void Enqueue_InvalidPriority_ShouldThrowException()
    {
        // Arrange
        var config = CreateDefaultConfig();
        var dispatch = new ChannelDispatch<TestPacket>(config);
        var invalidPacket = new TestPacket { Priority = (PacketPriority)99 }; // Invalid priority

        // Act & Assert
        Assert.Throws<IndexOutOfRangeException>(() => dispatch.Enqueue(invalidPacket));
    }

    [Fact]
    public void Dequeue_WithPredicate_ShouldStopAtCondition()
    {
        // Arrange
        var config = CreateDefaultConfig();
        var dispatch = new ChannelDispatch<TestPacket>(config);

        var packet1 = new TestPacket { Priority = PacketPriority.Low };
        var packet2 = new TestPacket { Priority = PacketPriority.Medium };
        var packet3 = new TestPacket { Priority = PacketPriority.High };

        dispatch.Enqueue(packet1);
        dispatch.Enqueue(packet2);
        dispatch.Enqueue(packet3);

        // Act
        var dequeuedPackets = dispatch.Dequeue(p => p.Priority == PacketPriority.Medium);

        // Assert
        Assert.Single(dequeuedPackets); // Only stops at the medium priority
        Assert.Equal(packet2, dequeuedPackets[0]);
        Assert.Equal(1, dispatch.Count); // Medium and High remain in the queue
    }

    [Fact]
    public void DequeueBatch_EmptyQueue_ShouldReturnEmptyList()
    {
        // Arrange
        var config = CreateDefaultConfig();
        var dispatch = new ChannelDispatch<TestPacket>(config);

        // Act
        var dequeuedPackets = dispatch.DequeueBatch(5);

        // Assert
        Assert.Empty(dequeuedPackets);
    }

    [Fact]
    public void TryRequeue_WithPriorityOverride_ShouldChangePriority()
    {
        // Arrange
        var config = CreateDefaultConfig();
        var dispatch = new ChannelDispatch<TestPacket>(config);

        var packet = new TestPacket { Priority = PacketPriority.Low };

        // Act
        bool result = dispatch.TryRequeue(packet, PacketPriority.Low);

        // Assert
        Assert.True(result);
        Assert.Equal(1, dispatch.Count);
        var dequeuedPacket = dispatch.Dequeue();
        Assert.Equal(PacketPriority.Low, dequeuedPacket.Priority);
    }
}
