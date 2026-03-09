using Nalix.Common.Networking.Protocols;
using Nalix.Shared.Frames.Controls;
using System;
using System.Diagnostics;
using Xunit;

public class DirectiveLengthTests
{
    /// <summary>
    /// Test serialize buffer phải khớp Length property của Directive.
    /// </summary>
    [Fact]
    public void Serialize_Length_Match()
    {
        // Arrange
        var directive = new Directive();
        directive.Initialize(
            type: ControlType.NACK,
            reason: ProtocolReason.ABORTED,
            action: ProtocolAdvice.DO_NOT_RETRY,
            sequenceId: 1234,
            flags: ControlFlags.HAS_REDIRECT,
            arg0: 111,
            arg1: 222,
            arg2: 333
        );
        UInt16 expectedLength = directive.Length;

        // Act
        Span<Byte> buffer = stackalloc Byte[expectedLength]; // buffer kích thước Length
        Int32 written = directive.Serialize(buffer);

        // Assert
        Assert.Equal(expectedLength, written); // Phát hiện bug thiếu/thừa byte ở đây
    }

    /// <summary>
    /// Test serialize buffer khi cấp thiếu kích thước (nên throw)
    /// </summary>
    [Fact]
    public void Serialize_InsufficientBuffer_Throws()
    {
        var directive = new Directive();
        directive.Initialize(ControlType.NOTICE, ProtocolReason.NONE, ProtocolAdvice.NONE, 6789);
        UInt16 required = directive.Length;

        // Gọi method helper để tránh lambda capture ref struct
        void AttemptSerialize()
        {
            Span<Byte> smallBuffer = stackalloc Byte[required];
            directive.Serialize(smallBuffer);

            Debug.WriteLine(directive.Length);
        }

        Assert.Throws<ArgumentException>(AttemptSerialize);
    }
}