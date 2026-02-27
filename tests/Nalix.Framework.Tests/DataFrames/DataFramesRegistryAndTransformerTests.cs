
using System;
using System.Linq;
using Nalix.Common.Exceptions;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Security;
using Nalix.Framework.DataFrames;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.Framework.DataFrames.Transforms;
using Nalix.Framework.Memory.Buffers;
using Xunit;

namespace Nalix.Framework.Tests.DataFrames;

public sealed partial class DataFramesPublicApiTests
{
    [Fact]
    public void DeserializeWhenRegisteredPacketBytesAreProvidedReturnsExpectedPacket()
    {
        PacketRegistry registry = new(factory => _ = factory);
        Control packet = new();
        packet.Initialize(33, ControlType.PONG, 88, ProtocolReason.NONE, ProtocolType.TCP);
        byte[] bytes = packet.Serialize();

        IPacket deserialized = registry.Deserialize(bytes);

        Control control = Assert.IsType<Control>(deserialized);
        Assert.Equal(packet.MagicNumber, control.MagicNumber);
        Assert.Equal(packet.Type, control.Type);
        Assert.Equal(packet.SequenceId, control.SequenceId);
        Assert.True(registry.DeserializerCount >= 1);
        Assert.True(registry.IsKnownMagic(packet.MagicNumber));
        Assert.True(registry.IsRegistered<Control>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(PacketConstants.HeaderSize - 5)]
    public void DeserializeWhenBufferIsTooShortThrowsArgumentException(int bufferLength)
    {
        PacketRegistry registry = new(factory => _ = factory);
        byte[] raw = new byte[bufferLength];
        if (bufferLength >= PacketConstants.HeaderSize)
        {
            BitConverter.GetBytes(0xDEADBEEFu).CopyTo(raw, 0);
        }

        ArgumentException ex = Assert.Throws<ArgumentException>(() => registry.Deserialize(raw));
        Assert.StartsWith("Raw packet data is too short to contain a valid header", ex.Message);
    }

    [Theory]
    [InlineData(PacketConstants.HeaderSize)]
    [InlineData(PacketConstants.HeaderSize + 10)]
    public void DeserializeWhenHeaderMagicIsUnknownThrowsInvalidOperationException(int bufferLength)
    {
        PacketRegistry registry = new(factory => _ = factory);
        byte[] raw = new byte[bufferLength];
        BitConverter.GetBytes(0xDEADBEEFu).CopyTo(raw, 0);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => registry.Deserialize(raw));
        Assert.StartsWith("Cannot deserialize packet: Magic", ex.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CreateCatalogWhenIncludeMethodsAreUsedReturnsRegistryWithBuiltIns(bool recursive)
    {
        PacketRegistryFactory factory = new();
        PacketRegistryFactory sameFactory = factory.IncludeAssembly(null);
        _ = factory.IncludeCurrentDomain();

        _ = recursive
            ? factory.IncludeNamespaceRecursive("Nalix.Framework.Tests.DataFrames.AssemblyScan")
            : factory.IncludeNamespace("Nalix.Framework.Tests.DataFrames.AssemblyScan");

        PacketRegistry registry = factory.CreateCatalog();

        Assert.Same(factory, sameFactory);
        Assert.True(registry.IsRegistered<Control>());
        Assert.True(registry.IsRegistered<Directive>());
        Assert.True(registry.IsRegistered<Handshake>());
    }

    [Fact]
    public void ComputeWhenCalledForSameTypeReturnsSameMagicNumber()
    {
        Type type = typeof(Control);

        uint first = PacketRegistryFactory.Compute(type);
        uint second = PacketRegistryFactory.Compute(type);

        Assert.Equal(first, second);
    }

    [Fact]
    public void ComputeWhenTypeIsNullThrowsArgumentNullException()
    {
        Type? type = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => PacketRegistryFactory.Compute(type!));

        Assert.Equal("type", exception.ParamName);
    }

    [Theory]
    [InlineData(CipherSuiteType.Chacha20, 10)]
    [InlineData(CipherSuiteType.Chacha20Poly1305, 10)]
    [InlineData(CipherSuiteType.Salsa20, 24)]
    [InlineData(CipherSuiteType.Salsa20Poly1305, 24)]
    public void GetMaxCiphertextSizeWhenSuiteIsValidReturnsExpectedEnvelopeCapacity(CipherSuiteType suite, int plaintextSize)
    {
        int expected = Security.EnvelopeCipher.HeaderSize
            + Security.EnvelopeCipher.GetNonceLength(suite)
            + plaintextSize
            + Security.EnvelopeCipher.GetTagLength(suite);

        int actual = FrameTransformer.GetMaxCiphertextSize(suite, plaintextSize);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("hello world")]
    [InlineData("payload payload payload payload payload")]
    public void CompressThenDecompressPacketLeaseRoundTripsPayloadAndHeader(string payloadText)
    {
        byte[] packetBytes = CreatePacketBytes(payloadText);
        using BufferLease source = BufferLease.CopyFrom(packetBytes);
        using BufferLease compressed = BufferLease.Rent(FrameTransformer.Offset + FrameTransformer.GetMaxCompressedSize(source.Length - FrameTransformer.Offset));
        using BufferLease decompressed = BufferLease.Rent(packetBytes.Length);

        FrameTransformer.Compress(source, compressed);
        int decompressedLength = FrameTransformer.GetDecompressedLength(compressed.Span[FrameTransformer.Offset..]);
        FrameTransformer.Decompress(compressed, decompressed);

        Assert.Equal(source.Length - FrameTransformer.Offset, decompressedLength);
        Assert.Equal(packetBytes, decompressed.Memory.ToArray());
    }

    [Theory]
    [InlineData(CipherSuiteType.Chacha20)]
    [InlineData(CipherSuiteType.Chacha20Poly1305)]
    public void EncryptThenDecryptPacketLeaseRoundTripsPayloadAndHeader(CipherSuiteType suite)
    {
        byte[] packetBytes = CreatePacketBytes("encrypted payload");
        byte[] key = [.. Enumerable.Range(1, 32).Select(static x => (byte)x)];
        using BufferLease source = BufferLease.CopyFrom(packetBytes);
        using BufferLease encrypted = BufferLease.Rent(FrameTransformer.Offset + FrameTransformer.GetMaxCiphertextSize(suite, source.Length - FrameTransformer.Offset));
        using BufferLease decrypted = BufferLease.Rent(packetBytes.Length);

        FrameTransformer.Encrypt(source, encrypted, key, suite);
        int plaintextLength = FrameTransformer.GetPlaintextLength(encrypted.Span);
        FrameTransformer.Decrypt(encrypted, decrypted, key);

        Assert.Equal(source.Length - FrameTransformer.Offset, plaintextLength);
        Assert.Equal(packetBytes, decrypted.Memory.ToArray());
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void EncryptWhenInputIsInvalidThrowsExpectedArgumentException(bool useEmptyKey, bool useShortSource)
    {
        byte[] packetBytes = CreatePacketBytes("abc");
        byte[] key = useEmptyKey ? [] : [.. Enumerable.Repeat((byte)7, 32)];
        using BufferLease source = useShortSource
            ? BufferLease.Rent(FrameTransformer.Offset)
            : BufferLease.CopyFrom(packetBytes);
        using BufferLease destination = BufferLease.Rent(
            FrameTransformer.Offset + FrameTransformer.GetMaxCiphertextSize(CipherSuiteType.Chacha20, Math.Max(1, packetBytes.Length - FrameTransformer.Offset)));

        if (useShortSource)
        {
            source.CommitLength(FrameTransformer.Offset);
        }

        if (useEmptyKey)
        {
            _ = Assert.Throws<ArgumentNullException>(() => FrameTransformer.Encrypt(source, destination, key, CipherSuiteType.Chacha20));
        }
        else
        {
            _ = Assert.Throws<ArgumentException>(() => FrameTransformer.Encrypt(source, destination, key, CipherSuiteType.Chacha20));
        }
    }

    [Theory]
    [InlineData("tiny")]
    [InlineData("payload near edge")]
    public void CompressWhenDestinationUsesMinimumReportedCapacitySucceeds(string payloadText)
    {
        byte[] packetBytes = CreatePacketBytes(payloadText);
        using BufferLease source = BufferLease.CopyFrom(packetBytes);
        using BufferLease compressed = BufferLease.Rent(FrameTransformer.Offset + FrameTransformer.GetMaxCompressedSize(source.Length - FrameTransformer.Offset));
        using BufferLease decompressed = BufferLease.Rent(packetBytes.Length);

        FrameTransformer.Compress(source, compressed);
        FrameTransformer.Decompress(compressed, decompressed);

        Assert.Equal(packetBytes, decompressed.Memory.ToArray());
    }

    [Theory]
    [InlineData(CipherSuiteType.Chacha20)]
    [InlineData(CipherSuiteType.Chacha20Poly1305)]
    public void EncryptWhenDestinationUsesMinimumReportedCapacitySucceeds(CipherSuiteType suite)
    {
        byte[] packetBytes = CreatePacketBytes("edge");
        byte[] key = [.. Enumerable.Range(1, 32).Select(static x => (byte)x)];
        using BufferLease source = BufferLease.CopyFrom(packetBytes);
        using BufferLease encrypted = BufferLease.Rent(FrameTransformer.Offset + FrameTransformer.GetMaxCiphertextSize(suite, source.Length - FrameTransformer.Offset));
        using BufferLease decrypted = BufferLease.Rent(packetBytes.Length);

        FrameTransformer.Encrypt(source, encrypted, key, suite);
        FrameTransformer.Decrypt(encrypted, decrypted, key);

        Assert.True(decrypted.Length >= packetBytes.Length);
        Assert.Equal(packetBytes, decrypted.Memory.Span[..packetBytes.Length].ToArray());
    }

    [Fact]
    public void DecryptFrameWhenCiphertextIsTooShortThrowsCipherException()
    {
        byte[] key = [.. Enumerable.Range(1, 32).Select(static x => (byte)x)];
        using BufferLease source = BufferLease.CopyFrom(new byte[FrameTransformer.Offset + 1]);

        _ = Assert.Throws<CipherException>(() => PacketCipher.DecryptFrame(source, key));
    }
}
