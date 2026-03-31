#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Security;
using Nalix.Common.Serialization;
using Nalix.Framework.DataFrames;
using Nalix.Framework.DataFrames.Chunks;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.Framework.DataFrames.TextFrames;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Options;
using Nalix.Framework.Serialization;
using Nalix.Framework.Serialization.Formatters.Cache;
using Xunit;

namespace Nalix.Framework.Tests.DataFrames
{
    /// <summary>
    /// Covers the public APIs exposed by the DataFrames folder.
    /// </summary>
    public sealed class DataFramesPublicApiTests
    {
        /// <summary>
        /// Verifies that text frame initialization accepts supported content sizes and updates protocol and length correctly.
        /// </summary>
        [Theory]
        [MemberData(nameof(TextFrameInitializeValidCases))]
        public void Initialize_ValidTextInput_UpdatesContentProtocolAndLength(
            Func<string, ProtocolType, FrameBase> createAndInitialize,
            string content,
            ProtocolType protocol,
            int expectedDynamicBytes)
        {
            // Arrange
            FrameBase frame = createAndInitialize(content, protocol);
            byte[] bytes = frame.Serialize();

            // Assert
            string actualContent = frame switch
            {
                Text256 text256 => text256.Content,
                Text512 text512 => text512.Content,
                Text1024 text1024 => text1024.Content,
                _ => throw new InvalidOperationException("Unexpected frame type.")
            };

            Assert.Equal(content, actualContent);
            Assert.Equal(protocol, frame.Protocol);
            Assert.Equal(expectedDynamicBytes, Encoding.UTF8.GetByteCount(actualContent));
            Assert.True(frame.Length >= bytes.Length);
            Assert.True(frame.Length > PacketConstants.HeaderSize);
        }

        /// <summary>
        /// Verifies that text frame initialization rejects content that exceeds the supported UTF-8 size.
        /// </summary>
        [Theory]
        [MemberData(nameof(TextFrameInitializeOverflowCases))]
        public void Initialize_ContentExceedsLimit_ThrowsArgumentOutOfRangeException(Action initialize)
        {
            // Arrange
            // Covered by MemberData.

            // Act
            ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(initialize);

            // Assert
            Assert.Equal("content", exception.ParamName);
        }

        /// <summary>
        /// Verifies that resetting a text frame clears the content and restores the shared header defaults.
        /// </summary>
        [Theory]
        [MemberData(nameof(TextFrameResetCases))]
        public void ResetForPool_FrameContainsText_ResetsContentAndHeaderDefaults(Func<FrameBase> createDirtyFrame)
        {
            // Arrange
            FrameBase frame = createDirtyFrame();

            // Act
            frame.ResetForPool();

            // Assert
            string actualContent = frame switch
            {
                Text256 text256 => text256.Content,
                Text512 text512 => text512.Content,
                Text1024 text1024 => text1024.Content,
                _ => throw new InvalidOperationException("Unexpected frame type.")
            };

            Assert.Equal(string.Empty, actualContent);
            Assert.Equal(PacketFlags.NONE, frame.Flags);
            Assert.Equal(PacketPriority.NONE, frame.Priority);
            Assert.Equal(ProtocolType.NONE, frame.Protocol);
            Assert.Equal(PacketConstants.OpcodeDefault, frame.OpCode);
        }

        [Theory]
        [InlineData(typeof(Control))]
        [InlineData(typeof(Handshake))]
        public static void CheckAllFieldsFormatter(Type modelType)
        {
            // Kéo formatter cho chính modelType (class gốc)
            _ = typeof(FormatterProvider).GetMethod("Get")!.MakeGenericMethod(modelType).Invoke(null, null);

            foreach (FieldInfo field in modelType!.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            {
                Type ft = field.FieldType;

                // For each field, force cache fill!
                _ = typeof(FormatterProvider).GetMethod("Get")!.MakeGenericMethod(ft).Invoke(null, null);

                object? inst = typeof(FormatterCache<>).MakeGenericType(ft)
                                 .GetField("Instance", BindingFlags.Public | BindingFlags.Static)!
                                 .GetValue(null);

                Debug.WriteLine(
                    $"[BUG-SCAN] {modelType.Name}.{field.Name}: Type={ft}, " +
                    (inst == null ?
                        "❌ No formatter in cache!" :
                        "✔️ " + inst.GetType()));
                // Check enum type
                if (ft.IsEnum && inst is not null && !inst.GetType().Name.Contains("EnumFormatter"))
                {
                    Debug.WriteLine($"❌ WARNING: Enum {ft} không dùng EnumFormatter mà là loại: {inst.GetType()}");
                }
            }
        }

        /// <summary>
        /// Verifies that serializing then deserializing a public packet preserves the public state.
        /// </summary>
        [Theory]
        [MemberData(nameof(PacketRoundTripCases))]
        public void SerializeDeserialize_PublicPacketRoundTrips_PublicStateIsPreserved(
            Func<FrameBase> createPacket,
            Action<FrameBase, FrameBase> assertEquivalent)
        {
            // Arrange
            FrameBase original = createPacket();

            // Act
            byte[] bytes = original.Serialize();
            FrameBase deserialized = original switch
            {
                Control => Control.Deserialize(bytes),
                Directive => Directive.Deserialize(bytes),
                Handshake => Handshake.Deserialize(bytes),
                Text256 => Text256.Deserialize(bytes),
                Text512 => Text512.Deserialize(bytes),
                Text1024 => Text1024.Deserialize(bytes),
                _ => throw new InvalidOperationException("Unexpected frame type.")
            };

            // Assert
            assertEquivalent(original, deserialized);
        }

        /// <summary>
        /// Verifies that a control packet initializer populates the requested public fields.
        /// </summary>
        [Fact]
        public void Initialize_ControlPacket_UpdatesPublicProperties()
        {
            // Arrange
            Control packet = new();

            // Act
            packet.Initialize(123, ControlType.PING, 42, ProtocolReason.TIMEOUT, ProtocolType.UDP);

            // Assert
            Assert.Equal((ushort)123, packet.OpCode);
            Assert.Equal(ControlType.PING, packet.Type);
            Assert.Equal(42u, packet.SequenceId);
            Assert.Equal(ProtocolReason.TIMEOUT, packet.Reason);
            Assert.Equal(ProtocolType.UDP, packet.Protocol);
            Assert.Equal(PacketPriority.URGENT, packet.Priority);
            Assert.NotEqual(0L, packet.Timestamp);
            Assert.NotEqual(0L, packet.MonoTicks);
        }

        /// <summary>
        /// Verifies that resetting a control packet clears mutable state and preserves the urgent priority default.
        /// </summary>
        [Fact]
        public void ResetForPool_ControlPacketWasInitialized_ResetsToControlDefaults()
        {
            // Arrange
            Control packet = new();
            packet.Initialize(555, ControlType.ERROR, 7, ProtocolReason.INTERNAL_ERROR, ProtocolType.UDP);
            packet.Flags = PacketFlags.SYSTEM;

            // Act
            packet.ResetForPool();

            // Assert
            Assert.Equal(ControlType.NONE, packet.Type);
            Assert.Equal(ProtocolReason.NONE, packet.Reason);
            Assert.Equal(0u, packet.SequenceId);
            Assert.Equal(0L, packet.Timestamp);
            Assert.Equal(0L, packet.MonoTicks);
            Assert.Equal(PacketPriority.URGENT, packet.Priority);
            Assert.Equal(PacketFlags.NONE, packet.Flags);
            Assert.Equal(ProtocolType.NONE, packet.Protocol);
        }

        /// <summary>
        /// Verifies that the directive initializer stores the supplied arguments in the public properties.
        /// </summary>
        [Fact]
        public void Initialize_DirectivePacket_UpdatesPublicProperties()
        {
            // Arrange
            Directive packet = new();

            // Act
            packet.Initialize(
                77,
                ControlType.REDIRECT,
                ProtocolReason.REDIRECT,
                ProtocolAdvice.RECONNECT,
                99,
                ControlFlags.HAS_REDIRECT | ControlFlags.IS_TRANSIENT,
                1000,
                2000,
                33);

            // Assert
            Assert.Equal((ushort)77, packet.OpCode);
            Assert.Equal(ControlType.REDIRECT, packet.Type);
            Assert.Equal(ProtocolReason.REDIRECT, packet.Reason);
            Assert.Equal(ProtocolAdvice.RECONNECT, packet.Action);
            Assert.Equal(99u, packet.SequenceId);
            Assert.Equal(ControlFlags.HAS_REDIRECT | ControlFlags.IS_TRANSIENT, packet.Control);
            Assert.Equal(1000u, packet.Arg0);
            Assert.Equal(2000u, packet.Arg1);
            Assert.Equal((ushort)33, packet.Arg2);
            Assert.Equal(PacketPriority.URGENT, packet.Priority);
            Assert.Equal(ProtocolType.TCP, packet.Protocol);
        }

        /// <summary>
        /// Verifies that a handshake constructor and reset expose the expected public state transitions.
        /// </summary>
        [Fact]
        public void ResetForPool_HandshakeContainsData_ClearsPayload()
        {
            // Arrange
            Handshake packet = new(12, [1, 2, 3, 4], ProtocolType.UDP);

            // Act
            packet.ResetForPool();

            // Assert
            Assert.NotNull(packet.Data);
            Assert.Empty(packet.Data);
            Assert.Equal(PacketFlags.NONE, packet.Flags);
            Assert.Equal(PacketPriority.NONE, packet.Priority);
            Assert.Equal(ProtocolType.NONE, packet.Protocol);
        }

        /// <summary>
        /// Verifies that the packet registry built from a configure action exposes registered deserializers and round-trips known packets.
        /// </summary>
        [Fact]
        public void TryDeserialize_RegisteredPacketBytes_ReturnsExpectedPacket()
        {
            Debug.WriteLine(FormatterProvider.Get<Control>().GetType().FullName);
            // Arrange
            PacketRegistry registry = new(factory => _ = factory);
            Control packet = new();
            packet.Initialize(33, ControlType.PONG, 88, ProtocolReason.NONE, ProtocolType.TCP);
            byte[] bytes = packet.Serialize();

            // Act
            IPacket deserialized = registry.Deserialize(bytes);

            // Assert=
            Control control = Assert.IsType<Control>(deserialized);
            Assert.Equal(packet.MagicNumber, control.MagicNumber);
            Assert.Equal(packet.Type, control.Type);
            Assert.Equal(packet.SequenceId, control.SequenceId);
            Assert.True(registry.DeserializerCount >= 1);
            Assert.True(registry.IsKnownMagic(packet.MagicNumber));
            Assert.True(registry.IsRegistered<Control>());
        }

        /// <summary>
        /// Verifies that the packet registry reports unknown buffers and unknown magic values without throwing.
        /// </summary>
        [Theory]
        [InlineData(0)]
        [InlineData(3)]
        [InlineData(PacketConstants.HeaderSize - 5)]
        public void TryDeserialize_BufferIsUnknownOrTooShort_ThrowsArgumentException(int bufferLength)
        {
            // Arrange
            PacketRegistry registry = new(factory => _ = factory);
            byte[] raw = new byte[bufferLength];
            if (bufferLength >= PacketConstants.HeaderSize)
            {
                BitConverter.GetBytes(0xDEADBEEFu).CopyTo(raw, 0);
            }

            // Act & Assert
            ArgumentException ex = Assert.Throws<ArgumentException>(() => registry.Deserialize(raw));
            Assert.StartsWith("Raw packet data is too short to contain a valid header", ex.Message);
        }

        [Theory]
        [InlineData(PacketConstants.HeaderSize)]
        [InlineData(PacketConstants.HeaderSize + 10)]
        public void TryDeserialize_HeaderIsUnknown_ThrowsInvalidOperationException(int bufferLength)
        {
            // Arrange
            PacketRegistry registry = new(factory => _ = factory);
            byte[] raw = new byte[bufferLength];
            BitConverter.GetBytes(0xDEADBEEFu).CopyTo(raw, 0);

            // Act & Assert
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => registry.Deserialize(raw));
            Assert.StartsWith("Cannot deserialize packet: Magic", ex.Message);
        }

        /// <summary>
        /// Verifies that factory include methods are chainable and still produce a registry with the built-in packets available.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CreateCatalog_IncludeMethodsUsed_ReturnsRegistryWithBuiltIns(bool recursive)
        {
            // Arrange
            PacketRegistryFactory factory = new();
            PacketRegistryFactory sameFactory = factory.IncludeAssembly(null);
            _ = factory.IncludeCurrentDomain();

            _ = recursive
                ? factory.IncludeNamespaceRecursive("Nalix.Framework.Tests.DataFrames.AssemblyScan")
                : factory.IncludeNamespace("Nalix.Framework.Tests.DataFrames.AssemblyScan");

            // Act
            PacketRegistry registry = factory.CreateCatalog();

            // Assert
            Assert.Same(factory, sameFactory);
            Assert.True(registry.IsRegistered<Control>());
            Assert.True(registry.IsRegistered<Directive>());
            Assert.True(registry.IsRegistered<Handshake>());
        }

        /// <summary>
        /// Verifies that computing a packet magic number is deterministic for the same type.
        /// </summary>
        [Fact]
        public void Compute_SameTypeRepeated_ReturnsSameMagicNumber()
        {
            // Arrange
            Type type = typeof(Control);

            // Act
            uint first = PacketRegistryFactory.Compute(type);
            uint second = PacketRegistryFactory.Compute(type);

            // Assert
            Assert.Equal(first, second);
        }

        /// <summary>
        /// Verifies that computing a packet magic number rejects a null type.
        /// </summary>
        [Fact]
        public void Compute_TypeIsNull_ThrowsArgumentNullException()
        {
            // Arrange
            Type? type = null;

            // Act
            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => PacketRegistryFactory.Compute(type!));

            // Assert
            Assert.Equal("type", exception.ParamName);
        }

        /// <summary>
        /// Verifies that the frame transformer size helpers return values consistent with the selected cipher suite and compression format.
        /// </summary>
        [Theory]
        [InlineData(CipherSuiteType.Chacha20, 10)]
        [InlineData(CipherSuiteType.Chacha20Poly1305, 10)]
        [InlineData(CipherSuiteType.Salsa20, 24)]
        [InlineData(CipherSuiteType.Salsa20Poly1305, 24)]
        public void GetMaxCiphertextSize_ValidSuite_ReturnsExpectedEnvelopeCapacity(CipherSuiteType suite, int plaintextSize)
        {
            // Arrange
            int expected = Security.EnvelopeCipher.HeaderSize
                + Security.EnvelopeCipher.GetNonceLength(suite)
                + plaintextSize
                + Security.EnvelopeCipher.GetTagLength(suite);

            // Act
            int actual = FrameTransformer.GetMaxCiphertextSize(suite, plaintextSize);

            // Assert
            Assert.Equal(expected, actual);
        }

        /// <summary>
        /// Verifies that compression and decompression round-trip the payload while leaving the packet header intact.
        /// </summary>
        [Theory]
        [InlineData("hello world")]
        [InlineData("payload payload payload payload payload")]
        public void CompressDecompress_ValidPacketLease_RoundTripsPayloadAndHeader(string payloadText)
        {
            // Arrange
            byte[] packetBytes = CreatePacketBytes(payloadText);
            using BufferLease source = BufferLease.CopyFrom(packetBytes);
            using BufferLease compressed = BufferLease.Rent(FrameTransformer.Offset + FrameTransformer.GetMaxCompressedSize(source.Length - FrameTransformer.Offset));
            using BufferLease decompressed = BufferLease.Rent(packetBytes.Length);

            // Act
            FrameTransformer.Compress(source, compressed);
            int decompressedLength = FrameTransformer.GetDecompressedLength(compressed.Span[FrameTransformer.Offset..]);
            FrameTransformer.Decompress(compressed, decompressed);

            // Assert
            Assert.Equal(source.Length - FrameTransformer.Offset, decompressedLength);
            Assert.Equal(packetBytes, decompressed.Memory.ToArray());
        }

        /// <summary>
        /// Verifies that encryption and decryption round-trip the payload while preserving the packet header.
        /// </summary>
        [Theory]
        [InlineData(CipherSuiteType.Chacha20)]
        [InlineData(CipherSuiteType.Chacha20Poly1305)]
        public void EncryptDecrypt_ValidPacketLease_RoundTripsPayloadAndHeader(CipherSuiteType suite)
        {
            // Arrange
            byte[] packetBytes = CreatePacketBytes("encrypted payload");
            byte[] key = [.. Enumerable.Range(1, 32).Select(static x => (byte)x)];
            using BufferLease source = BufferLease.CopyFrom(packetBytes);
            using BufferLease encrypted = BufferLease.Rent(FrameTransformer.Offset + FrameTransformer.GetMaxCiphertextSize(suite, source.Length - FrameTransformer.Offset));
            using BufferLease decrypted = BufferLease.Rent(packetBytes.Length);

            // Act
            FrameTransformer.Encrypt(source, encrypted, key, suite);
            int plaintextLength = FrameTransformer.GetPlaintextLength(encrypted.Span);
            FrameTransformer.Decrypt(encrypted, decrypted, key);

            // Assert
            Assert.Equal(source.Length - FrameTransformer.Offset, plaintextLength);
            Assert.Equal(packetBytes, decrypted.Memory.ToArray());
        }

        /// <summary>
        /// Verifies that frame transformer methods reject invalid keys and invalid buffer capacities using their public return contract.
        /// </summary>
        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public void TryEncrypt_InvalidInput_ThrowsArgumentException(bool useEmptyKey, bool useShortSource)
        {
            // Arrange
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

            // Act + Assert
            if (useEmptyKey)
            {
                _ = Assert.Throws<ArgumentNullException>(() => FrameTransformer.Encrypt(source, destination, key, CipherSuiteType.Chacha20));
            }
            else
            {
                _ = Assert.Throws<ArgumentException>(() => FrameTransformer.Encrypt(source, destination, key, CipherSuiteType.Chacha20));
            }
        }

        /// <summary>
        /// Verifies that a fragment header can be written to bytes and read back without losing any public state.
        /// </summary>
        [Theory]
        [InlineData((ushort)1, (ushort)0, (ushort)2, false)]
        [InlineData((ushort)9, (ushort)1, (ushort)2, true)]
        public void WriteToReadFrom_ValidFragmentHeader_RoundTripsValues(ushort streamId, ushort chunkIndex, ushort totalChunks, bool isLast)
        {
            // Arrange
            FragmentHeader header = new(streamId, chunkIndex, totalChunks, isLast);
            Span<byte> buffer = stackalloc byte[FragmentHeader.WireSize];

            // Act
            header.WriteTo(buffer);
            FragmentHeader roundTripped = FragmentHeader.ReadFrom(buffer);

            // Assert
            Assert.Equal(header, roundTripped);
            Assert.Equal(streamId, roundTripped.StreamId);
            Assert.Equal(chunkIndex, roundTripped.ChunkIndex);
            Assert.Equal(totalChunks, roundTripped.TotalChunks);
            Assert.Equal(isLast, roundTripped.IsLast);
        }

        /// <summary>
        /// Verifies that reading a fragment header rejects an invalid magic byte.
        /// </summary>
        [Fact]
        public void ReadFrom_InvalidMagic_ThrowsInvalidDataException()
        {
            // Arrange
            byte[] buffer = new byte[FragmentHeader.WireSize];

            // Act
            InvalidDataException exception = Assert.Throws<InvalidDataException>(() => FragmentHeader.ReadFrom(buffer));

            // Assert
            Assert.Equal("Invalid fragment magic", exception.Message);
        }

        /// <summary>
        /// Verifies that fragment options validation accepts valid settings and rejects invalid public configurations.
        /// </summary>
        [Theory]
        [InlineData(1400, 1400, 1400, true)]
        [InlineData(0, 1400, 1400, false)]
        [InlineData(1000, 1400, 1000, false)]
        public void Validate_FragmentOptionsConfiguration_MatchesExpectedValidity(
            int maxPayloadSize,
            int chunkThreshold,
            int chunkBodySize,
            bool shouldSucceed)
        {
            // Arrange
            FragmentOptions options = new()
            {
                MaxPayloadSize = maxPayloadSize,
                ChunkThreshold = chunkThreshold,
                ChunkBodySize = chunkBodySize
            };

            // Act
            Exception? exception = Record.Exception(options.Validate);

            // Assert
            if (shouldSucceed)
            {
                Assert.Null(exception);
            }
            else
            {
                _ = Assert.IsType<InvalidOperationException>(exception);
            }
        }

        /// <summary>
        /// Verifies that fragment stream identifiers never return zero across multiple allocations.
        /// </summary>
        [Fact]
        public void Next_MultipleAllocations_NeverReturnsZero()
        {
            // Arrange
            ushort[] values = new ushort[128];

            // Act
            for (int index = 0; index < values.Length; index++)
            {
                values[index] = FragmentStreamId.Next();
            }

            // Assert
            Assert.DoesNotContain((ushort)0, values);
        }

        /// <summary>
        /// Verifies that adding all chunks for a stream returns the assembled payload and closes the stream.
        /// </summary>
        [Fact]
        public void Add_AllChunksArriveInOrder_ReturnsAssembledBuffer()
        {
            // Arrange
            using FragmentAssembler assembler = new();
            FragmentHeader first = new(7, 0, 2, false);
            FragmentHeader second = new(7, 1, 2, true);

            // Act
            BufferLease? firstAssembled = assembler.Add(first, Encoding.UTF8.GetBytes("hello "), out bool firstEvicted);
            BufferLease? secondAssembled = assembler.Add(second, Encoding.UTF8.GetBytes("world"), out bool secondEvicted);

            // Assert
            Assert.Null(firstAssembled);
            Assert.False(firstEvicted);
            Assert.False(secondEvicted);
            using BufferLease assembled = Assert.IsType<BufferLease>(secondAssembled);
            Assert.Equal("hello world", Encoding.UTF8.GetString(assembled.Memory.Span));
            Assert.Equal(0, assembler.OpenStreamCount);
        }

        /// <summary>
        /// Verifies that malformed or unexpected chunks are rejected without producing assembled output.
        /// </summary>
        [Theory]
        [InlineData((ushort)0, (ushort)0, (ushort)1)]
        [InlineData((ushort)1, (ushort)1, (ushort)1)]
        [InlineData((ushort)1, (ushort)0, (ushort)0)]
        public void Add_HeaderIsInvalid_ThrowsInvalidDataException(ushort streamId, ushort chunkIndex, ushort totalChunks)
        {
            // Arrange
            using FragmentAssembler assembler = new();
            FragmentHeader header = new(streamId, chunkIndex, totalChunks, false);

            // Assert
            _ = Assert.Throws<InvalidDataException>(() => assembler.Add(header, [1, 2, 3], out _));
        }

        /// <summary>
        /// Verifies that a timed out stream is evicted when the next chunk arrives after the timeout window.
        /// </summary>
        [Fact]
        public void Add_StreamHasTimedOut_EvictsStreamAndReturnsNull()
        {
            // Arrange
            using FragmentAssembler assembler = new() { StreamTimeoutMs = 1 };
            FragmentHeader first = new(15, 0, 2, false);
            FragmentHeader second = new(15, 1, 2, true);
            _ = assembler.Add(first, [1], out _);
            Thread.Sleep(100);

            // Act
            BufferLease? assembled = assembler.Add(second, [2], out bool streamEvicted);

            // Assert
            Assert.Null(assembled);
            Assert.True(streamEvicted);
            Assert.Equal(0, assembler.OpenStreamCount);
        }

        /// <summary>
        /// Verifies that expired streams can be evicted explicitly and that clear removes any remaining public stream count.
        /// </summary>
        [Fact]
        public void EvictExpiredAndClear_StreamsAreOpen_RemovesTrackedStreams()
        {
            // Arrange
            using FragmentAssembler assembler = new() { StreamTimeoutMs = 1 };
            _ = assembler.Add(new FragmentHeader(21, 0, 2, false), [1, 2], out _);
            Thread.Sleep(100);

            // Act
            int evicted = assembler.EvictExpired();
            _ = assembler.Add(new FragmentHeader(22, 0, 2, false), [3, 4], out _);
            assembler.Clear();

            // Assert
            Assert.Equal(1, evicted);
            Assert.Equal(0, assembler.OpenStreamCount);
        }

        /// <summary>
        /// Verifies that fragmented frame detection recognizes valid headers and rejects ordinary payloads.
        /// </summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void IsFragmentedFrame_PayloadVaries_ReturnsExpectedResult(bool useValidPayload)
        {
            // Arrange
            byte[] payload = useValidPayload
                ? CreateFragmentPayload(new FragmentHeader(30, 0, 1, true), [9, 8, 7])
                : [0x01, 0x02, 0x03];

            // Act
            bool isFragment = FragmentAssembler.IsFragmentedFrame(payload, out FragmentHeader header);

            // Assert
            Assert.Equal(useValidPayload, isFragment);
            if (useValidPayload)
            {
                Assert.Equal((ushort)30, header.StreamId);
                Assert.True(header.IsLast);
            }
            else
            {
                Assert.Equal(default, header);
            }
        }

        /// <summary>
        /// Supplies valid text frame initialization scenarios.
        /// </summary>
        public static IEnumerable<object[]> TextFrameInitializeValidCases()
        {
            yield return
            [
                (Func<string, ProtocolType, FrameBase>)((content, protocol) =>
                {
                    Text256 frame = new();
                    frame.Initialize(content, protocol);
                    return frame;
                }),
                string.Empty,
                ProtocolType.TCP,
                0
            ];

            yield return
            [
                (Func<string, ProtocolType, FrameBase>)((content, protocol) =>
                {
                    Text512 frame = new();
                    frame.Initialize(content, protocol);
                    return frame;
                }),
                "hello",
                ProtocolType.UDP,
                Encoding.UTF8.GetByteCount("hello")
            ];

            yield return
            [
                (Func<string, ProtocolType, FrameBase>)((content, protocol) =>
                {
                    Text1024 frame = new();
                    frame.Initialize(content, protocol);
                    return frame;
                }),
                "Xin chao",
                ProtocolType.TCP,
                Encoding.UTF8.GetByteCount("Xin chao")
            ];
        }

        /// <summary>
        /// Supplies text frame overflow scenarios.
        /// </summary>
        public static IEnumerable<object[]> TextFrameInitializeOverflowCases()
        {
            yield return
            [
                (() =>
                {
                    Text256 frame = new();
                    frame.Initialize(new string('a', Text256.DynamicSize + 1));
                })
            ];

            yield return
            [
                (() =>
                {
                    Text512 frame = new();
                    frame.Initialize(new string('b', Text512.DynamicSize + 1));
                })
            ];

            yield return
            [
                (() =>
                {
                    Text1024 frame = new();
                    frame.Initialize(new string('c', Text1024.DynamicSize + 1));
                })
            ];
        }

        /// <summary>
        /// Supplies reset scenarios for the text frames.
        /// </summary>
        public static IEnumerable<object[]> TextFrameResetCases()
        {
            yield return
            [
                (Func<FrameBase>)(() =>
                {
                    Text256 frame = new();
                    frame.Initialize("alpha", ProtocolType.UDP);
                    frame.Flags = PacketFlags.COMPRESSED;
                    frame.Priority = PacketPriority.HIGH;
                    frame.OpCode = 5;
                    return frame;
                })
            ];

            yield return
            [
                (Func<FrameBase>)(() =>
                {
                    Text512 frame = new();
                    frame.Initialize("beta", ProtocolType.TCP);
                    frame.Flags = PacketFlags.ENCRYPTED;
                    frame.Priority = PacketPriority.LOW;
                    frame.OpCode = 6;
                    return frame;
                })
            ];

            yield return
            [
                (Func<FrameBase>)(() =>
                {
                    Text1024 frame = new();
                    frame.Initialize("gamma", ProtocolType.UDP);
                    frame.Flags = PacketFlags.FRAGMENTED;
                    frame.Priority = PacketPriority.MEDIUM;
                    frame.OpCode = 7;
                    return frame;
                })
            ];
        }

        /// <summary>
        /// Supplies public packet round-trip scenarios.
        /// </summary>
        public static IEnumerable<object[]> PacketRoundTripCases()
        {
            yield return
            [
                (Func<FrameBase>)(() =>
                {
                    Control packet = new();
                    packet.Initialize(14, ControlType.HEARTBEAT, 55, ProtocolReason.NONE, ProtocolType.TCP);
                    return packet;
                }),
                (Action<FrameBase, FrameBase>)((expected, actual) =>
                {
                    Control expectedControl = Assert.IsType<Control>(expected);
                    Control actualControl = Assert.IsType<Control>(actual);
                    Assert.Equal(expectedControl.MagicNumber, actualControl.MagicNumber);
                    Assert.Equal(expectedControl.OpCode, actualControl.OpCode);
                    Assert.Equal(expectedControl.Type, actualControl.Type);
                    Assert.Equal(expectedControl.Reason, actualControl.Reason);
                    Assert.Equal(expectedControl.Protocol, actualControl.Protocol);
                    Assert.Equal(expectedControl.SequenceId, actualControl.SequenceId);
                })
            ];

            yield return
            [
                (Func<FrameBase>)(() =>
                {
                    Directive packet = new();
                    packet.Initialize(91, ControlType.THROTTLE, ProtocolReason.THROTTLED, ProtocolAdvice.SLOW_DOWN, 12, ControlFlags.SLOW_DOWN, 9, 8, 7);
                    return packet;
                }),
                (Action<FrameBase, FrameBase>)((expected, actual) =>
                {
                    Directive expectedDirective = Assert.IsType<Directive>(expected);
                    Directive actualDirective = Assert.IsType<Directive>(actual);
                    Assert.Equal(expectedDirective.OpCode, actualDirective.OpCode);
                    Assert.Equal(expectedDirective.Type, actualDirective.Type);
                    Assert.Equal(expectedDirective.Reason, actualDirective.Reason);
                    Assert.Equal(expectedDirective.Action, actualDirective.Action);
                    Assert.Equal(expectedDirective.Control, actualDirective.Control);
                    Assert.Equal(expectedDirective.Arg0, actualDirective.Arg0);
                    Assert.Equal(expectedDirective.Arg1, actualDirective.Arg1);
                    Assert.Equal(expectedDirective.Arg2, actualDirective.Arg2);
                    Assert.Equal(expectedDirective.SequenceId, actualDirective.SequenceId);
                })
            ];

            yield return
            [
                (Func<FrameBase>)(() => new Handshake(17, [1, 2, 3, 4], ProtocolType.UDP)),
                (Action<FrameBase, FrameBase>)((expected, actual) =>
                {
                    Handshake expectedHandshake = Assert.IsType<Handshake>(expected);
                    Handshake actualHandshake = Assert.IsType<Handshake>(actual);
                    Assert.Equal(expectedHandshake.OpCode, actualHandshake.OpCode);
                    Assert.Equal(expectedHandshake.Protocol, actualHandshake.Protocol);
                    Assert.Equal(expectedHandshake.Data, actualHandshake.Data);
                })
            ];

            yield return
            [
                (Func<FrameBase>)(() =>
                {
                    Text256 packet = new();
                    packet.Initialize("short text", ProtocolType.TCP);
                    return packet;
                }),
                (Action<FrameBase, FrameBase>)((expected, actual) =>
                {
                    Text256 expectedText = Assert.IsType<Text256>(expected);
                    Text256 actualText = Assert.IsType<Text256>(actual);
                    Assert.Equal(expectedText.Content, actualText.Content);
                    Assert.Equal(expectedText.Protocol, actualText.Protocol);
                })
            ];

            yield return
            [
                (Func<FrameBase>)(() =>
                {
                    Text512 packet = new();
                    packet.Initialize("mid sized text", ProtocolType.UDP);
                    return packet;
                }),
                (Action<FrameBase, FrameBase>)((expected, actual) =>
                {
                    Text512 expectedText = Assert.IsType<Text512>(expected);
                    Text512 actualText = Assert.IsType<Text512>(actual);
                    Assert.Equal(expectedText.Content, actualText.Content);
                    Assert.Equal(expectedText.Protocol, actualText.Protocol);
                })
            ];

            yield return
            [
                (Func<FrameBase>)(() =>
                {
                    Text1024 packet = new();
                    packet.Initialize("larger text frame content", ProtocolType.TCP);
                    return packet;
                }),
                (Action<FrameBase, FrameBase>)((expected, actual) =>
                {
                    Text1024 expectedText = Assert.IsType<Text1024>(expected);
                    Text1024 actualText = Assert.IsType<Text1024>(actual);
                    Assert.Equal(expectedText.Content, actualText.Content);
                    Assert.Equal(expectedText.Protocol, actualText.Protocol);
                })
            ];
        }

        /// <summary>
        /// Supplies packet instances for buffer size validation.
        /// </summary>
        public static IEnumerable<object[]> PacketSerializeBufferTooSmallCases()
        {
            Control control = new();
            control.Initialize(8, ControlType.PING, 1, ProtocolReason.NONE, ProtocolType.TCP);
            yield return [control];

            Directive directive = new();
            directive.Initialize(ControlType.ACK, ProtocolReason.NONE, ProtocolAdvice.NONE, 1);
            yield return [directive];

            yield return [new Handshake(5, [1, 2, 3], ProtocolType.TCP)];

            Text256 text256 = new();
            text256.Initialize("abc");
            yield return [text256];
        }

        private static byte[] CreatePacketBytes(string payload)
        {
            Text256 text = new();
            text.Initialize(payload, ProtocolType.TCP);
            return text.Serialize();
        }

        private static byte[] CreateFragmentPayload(FragmentHeader header, ReadOnlySpan<byte> body)
        {
            byte[] payload = new byte[FragmentHeader.WireSize + body.Length];
            header.WriteTo(payload);
            body.CopyTo(payload.AsSpan(FragmentHeader.WireSize));
            return payload;
        }
    }
}

namespace Nalix.Framework.Tests.DataFrames.AssemblyScan
{
    /// <summary>
    /// Test-only packet for namespace scanning.
    /// </summary>
    public sealed class AssemblyScanRootPacket : PacketBase<AssemblyScanRootPacket>
    {
        /// <summary>
        /// Gets or sets the test payload value.
        /// </summary>
        [SerializeOrder(PacketHeaderOffset.Region)]
        public ushort Value { get; set; }

        /// <summary>
        /// Deserializes the packet from a buffer for registry scanning tests.
        /// </summary>
        public static new AssemblyScanRootPacket Deserialize(ReadOnlySpan<byte> buffer)
            => PacketBase<AssemblyScanRootPacket>.Deserialize(buffer);
    }
}

namespace Nalix.Framework.Tests.DataFrames.AssemblyScanChild
{
    /// <summary>
    /// Test-only child namespace packet for recursive scanning.
    /// </summary>
    public sealed class AssemblyScanChildPacket : PacketBase<AssemblyScanChildPacket>
    {
        /// <summary>
        /// Gets or sets the test payload value.
        /// </summary>
        [SerializeOrder(PacketHeaderOffset.Region)]
        public ushort Value { get; set; }

        /// <summary>
        /// Deserializes the packet from a buffer for registry scanning tests.
        /// </summary>
        public static new AssemblyScanChildPacket Deserialize(ReadOnlySpan<byte> buffer)
            => PacketBase<AssemblyScanChildPacket>.Deserialize(buffer);
    }
}
