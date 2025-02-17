using Notio.Network.Web.Enums;
using Notio.Network.Web.Net.Internal;
using Notio.Shared;
using System;
using System.Globalization;
using System.IO;
using Notio.Shared.Extensions.Primitives;

namespace Notio.Network.Web.WebSockets.Internal;

internal class WebSocketFrame
{
    internal static readonly byte[] EmptyPingBytes = CreatePingFrame().ToArray();

    internal WebSocketFrame(Opcode opcode, PayloadData payloadData)
        : this(Fin.Final, opcode, payloadData)
    {
    }

    internal WebSocketFrame(Fin fin, Opcode opcode, byte[] data, bool compressed)
        : this(fin, opcode, new PayloadData(data), compressed)
    {
    }

    private WebSocketFrame(
        Fin fin,
        Opcode opcode,
        PayloadData payloadData,
        bool compressed = false)
    {
        Fin = fin;
        Rsv1 = IsOpcodeData(opcode) && compressed ? Rsv.On : Rsv.Off;
        Rsv2 = Rsv.Off;
        Rsv3 = Rsv.Off;
        Opcode = opcode;

        ulong len = payloadData.Length;
        if (len < 126)
        {
            PayloadLength = (byte)len;
            ExtendedPayloadLength = [];
        }
        else if (len < 0x010000)
        {
            PayloadLength = 126;
            ExtendedPayloadLength = ((ushort)len).ToByteArray(Endianness.Big);
        }
        else
        {
            PayloadLength = 127;
            ExtendedPayloadLength = len.ToByteArray(Endianness.Big);
        }

        Mask = Mask.Off;
        MaskingKey = [];
        PayloadData = payloadData ?? throw new ArgumentNullException(nameof(payloadData));
    }

    internal WebSocketFrame(
        Fin fin,
        Rsv rsv1,
        Rsv rsv2,
        Rsv rsv3,
        Opcode opcode,
        Mask mask,
        byte payloadLength)
    {
        Fin = fin;
        Rsv1 = rsv1;
        Rsv2 = rsv2;
        Rsv3 = rsv3;
        Opcode = opcode;
        Mask = mask;
        PayloadLength = payloadLength;
        MaskingKey = [];
        PayloadData = new PayloadData([]);
    }

    public byte[]? ExtendedPayloadLength { get; internal set; }

    public Fin Fin { get; internal set; }

    public bool IsCompressed => Rsv1 == Rsv.On;

    public bool IsFragment => Fin == Fin.More || Opcode == Opcode.Cont;

    public bool IsMasked => Mask == Mask.On;

    private Mask Mask { get; set; }

    public byte[] MaskingKey { get; internal set; }

    public Opcode Opcode { get; internal set; }

    public PayloadData PayloadData { get; internal set; }

    public byte PayloadLength { get; internal set; }

    private Rsv Rsv1 { get; set; }

    private Rsv Rsv2 { get; set; }

    private Rsv Rsv3 { get; set; }

    internal int ExtendedPayloadLengthCount => PayloadLength < 126 ? 0 : PayloadLength == 126 ? 2 : 8;

    internal ulong FullPayloadLength => PayloadLength < 126
        ? PayloadLength
        : PayloadLength == 126
            ? BitConverter.ToUInt16(ExtendedPayloadLength?.ToHostOrder(Endianness.Big) ?? [], 0)
            : BitConverter.ToUInt64(ExtendedPayloadLength?.ToHostOrder(Endianness.Big) ?? [], 0);

    public string PrintToString()
    {
        // Payload Length
        byte payloadLen = PayloadLength;

        // Extended Payload Length
        string extPayloadLen = payloadLen > 125 ? FullPayloadLength.ToString(CultureInfo.InvariantCulture) : string.Empty;

        // Masking Key
        string maskingKey = BitConverter.ToString(MaskingKey);

        // Payload Data
        string payload = payloadLen == 0
            ? string.Empty
            : payloadLen > 125
                ? "---"
                : Opcode == Opcode.Text && !(IsFragment || IsMasked || IsCompressed)
                    ? PayloadData.ApplicationData.ToArray().ToText()
                    : PayloadData.ToString();

        return $@"
                    FIN: {Fin}
                   RSV1: {Rsv1}
                   RSV2: {Rsv2}
                   RSV3: {Rsv3}
                 Opcode: {Opcode}
                   MASK: {Mask}
         Payload Length: {payloadLen}
Extended Payload Length: {extPayloadLen}
            Masking Key: {maskingKey}
           Payload Data: {payload}";
    }

    public byte[] ToArray()
    {
        using MemoryStream buff = new();
        int header = (int)Fin;

        header = (header << 1) + (int)Rsv1;
        header = (header << 1) + (int)Rsv2;
        header = (header << 1) + (int)Rsv3;
        header = (header << 4) + (int)Opcode;
        header = (header << 1) + (int)Mask;
        header = (header << 7) + PayloadLength;
        buff.Write(((ushort)header).ToByteArray(Endianness.Big), 0, 2);

        if (PayloadLength > 125)
        {
            buff.Write(ExtendedPayloadLength ?? [], 0, PayloadLength == 126 ? 2 : 8);
        }

        if (Mask == Mask.On)
        {
            buff.Write(MaskingKey, 0, 4);
        }

        if (PayloadLength > 0)
        {
            byte[] bytes = PayloadData.ToArray();
            if (PayloadLength < 127)
            {
                buff.Write(bytes, 0, bytes.Length);
            }
            else
            {
                using MemoryStream input = new(bytes);
                input.CopyTo(buff, 1024);
            }
        }

        return buff.ToArray();
    }

    internal static WebSocketFrame CreateCloseFrame(PayloadData? payloadData)
        => new(Fin.Final, Opcode.Close, payloadData ?? new PayloadData());

    private static WebSocketFrame CreatePingFrame()
        => new(Fin.Final, Opcode.Ping, new PayloadData());

    internal static WebSocketFrame CreatePingFrame(byte[] data)
        => new(Fin.Final, Opcode.Ping, new PayloadData(data));

    internal void Validate(WebSocket webSocket)
    {
        if (!IsMasked)
        {
            throw new WebSocketException(CloseStatusCode.ProtocolError, "A frame from a client isn't masked.");
        }

        if (webSocket.InContinuation && (Opcode == Opcode.Text || Opcode == Opcode.Binary))
        {
            throw new WebSocketException(CloseStatusCode.ProtocolError,
                "A data frame has been received while receiving continuation frames.");
        }

        if (IsCompressed && webSocket.Compression == CompressionMethod.None)
        {
            throw new WebSocketException(CloseStatusCode.ProtocolError,
                "A compressed frame has been received without any agreement for it.");
        }

        if (Rsv2 == Rsv.On)
        {
            throw new WebSocketException(CloseStatusCode.ProtocolError,
                "The RSV2 of a frame is non-zero without any negotiation for it.");
        }

        if (Rsv3 == Rsv.On)
        {
            throw new WebSocketException(CloseStatusCode.ProtocolError,
                "The RSV3 of a frame is non-zero without any negotiation for it.");
        }
    }

    internal void Unmask()
    {
        if (Mask == Mask.Off)
        {
            return;
        }

        Mask = Mask.Off;
        PayloadData.Mask(MaskingKey);
        MaskingKey = [];
    }

    private static bool IsOpcodeData(Opcode opcode) => opcode is Opcode.Text or Opcode.Binary;
}
