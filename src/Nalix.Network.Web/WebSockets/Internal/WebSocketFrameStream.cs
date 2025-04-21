using Nalix.Network.Web.Enums;
using System;
using System.IO;
using System.Threading.Tasks;
using Nalix.Network.Web.WebSockets.Internal.Enums;
using Nalix.Extensions.Primitives;

namespace Nalix.Network.Web.WebSockets.Internal;

internal class WebSocketFrameStream(Stream? stream, bool unmask = false)
{
    private readonly bool _unmask = unmask;
    private readonly Stream? _stream = stream;

    internal async Task<WebSocketFrame?> ReadFrameAsync(WebSocket webSocket)
    {
        if (_stream == null)
        {
            return null;
        }

        WebSocketFrame frame = ProcessHeader(await _stream.ReadBytesAsync(2).ConfigureAwait(false));

        await ReadExtendedPayloadLengthAsync(frame).ConfigureAwait(false);
        await ReadMaskingKeyAsync(frame).ConfigureAwait(false);
        await ReadPayloadDataAsync(frame).ConfigureAwait(false);

        if (_unmask)
        {
            frame.Unmask();
        }

        frame.Validate(webSocket);

        frame.Unmask();

        return frame;
    }

    private static bool IsOpcodeData(byte opcode) => opcode is 0x1 or 0x2;

    private static bool IsOpcodeControl(byte opcode) => opcode is > 0x7 and < 0x10;

    private static WebSocketFrame ProcessHeader(byte[] header)
    {
        if (header.Length != 2)
        {
            throw new WebSocketException("The header of a frame cannot be read from the stream.");
        }

        // FIN
        Fin fin = (header[0] & 0x80) == 0x80 ? Fin.Final : Fin.More;

        // RSV1
        Rsv rsv1 = (header[0] & 0x40) == 0x40 ? Rsv.On : Rsv.Off;

        // RSV2
        Rsv rsv2 = (header[0] & 0x20) == 0x20 ? Rsv.On : Rsv.Off;

        // RSV3
        Rsv rsv3 = (header[0] & 0x10) == 0x10 ? Rsv.On : Rsv.Off;

        // Opcode
        byte opcode = (byte)(header[0] & 0x0f);

        // MASK
        Mask mask = (header[1] & 0x80) == 0x80 ? Mask.On : Mask.Off;

        // Payload Length
        byte payloadLen = (byte)(header[1] & 0x7f);

        string? err = !Enum.IsDefined(typeof(Opcode), opcode) ? "An unsupported opcode."
        : !IsOpcodeData(opcode) && rsv1 == Rsv.On ? "A non data frame is compressed."
        : IsOpcodeControl(opcode) && fin == Fin.More ? "A control frame is fragmented."
        : IsOpcodeControl(opcode) && payloadLen > 125 ? "A control frame has a long payload length."
        : null;

        return err != null
            ? throw new WebSocketException(CloseStatusCode.ProtocolError, err)
            : new WebSocketFrame(fin, rsv1, rsv2, rsv3, (Opcode)opcode, mask, payloadLen);
    }

    private async Task ReadExtendedPayloadLengthAsync(WebSocketFrame frame)
    {
        if (_stream == null)
            throw new ArgumentNullException(nameof(frame));

        int len = frame.ExtendedPayloadLengthCount;

        if (len == 0)
        {
            frame.ExtendedPayloadLength = [];
            return;
        }

        byte[] bytes = await _stream.ReadBytesAsync(len).ConfigureAwait(false);

        if (bytes.Length != len)
        {
            throw new WebSocketException(
                "The extended payload length of a frame cannot be read from the stream.");
        }

        frame.ExtendedPayloadLength = bytes;
    }

    private async Task ReadMaskingKeyAsync(WebSocketFrame frame)
    {
        ArgumentNullException.ThrowIfNull(_stream);

        int len = frame.IsMasked ? 4 : 0;

        if (len == 0)
        {
            frame.MaskingKey = [];
            return;
        }

        byte[] bytes = await _stream.ReadBytesAsync(len).ConfigureAwait(false);
        if (bytes.Length != len)
        {
            throw new WebSocketException(
                  "The masking key of a frame cannot be read from the stream.");
        }

        frame.MaskingKey = bytes;
    }

    private async Task ReadPayloadDataAsync(WebSocketFrame frame)
    {
        ArgumentNullException.ThrowIfNull(_stream);

        ulong len = frame.FullPayloadLength;
        if (len == 0)
        {
            frame.PayloadData = new PayloadData();

            return;
        }

        if (len > PayloadData.MaxLength)
        {
            throw new WebSocketException(CloseStatusCode.TooBig, "A frame has a long payload length.");
        }

        byte[] bytes = frame.PayloadLength < 127
            ? await _stream.ReadBytesAsync((int)len).ConfigureAwait(false)
            : await _stream.ReadBytesAsync((int)len, 1024).ConfigureAwait(false);

        if (bytes.Length != (int)len)
        {
            throw new WebSocketException(
                  "The payload data of a frame cannot be read from the stream.");
        }

        frame.PayloadData = new PayloadData(bytes);
    }
}
