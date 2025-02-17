using Notio.Network.Web.Enums;
using Notio.Network.Web.Net.Internal;
using Notio.Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Notio.Network.Web.WebSockets.Internal;

internal class PayloadData
{
    public const ulong MaxLength = long.MaxValue;

    private readonly byte[] _data;
    private ushort? _code;

    internal PayloadData(byte[] data)
    {
        _data = data;
    }

    internal PayloadData(ushort code = 1005, string? reason = null)
    {
        _code = code;
        _data = code == 1005 ? [] : Append(code, reason);
    }

    internal MemoryStream ApplicationData => new(_data);

    internal ulong Length => (ulong)_data.Length;

    private ushort Code
    {
        get
        {
            _code ??= _data.Length > 1
                ? BitConverter.ToUInt16(_data.Take(2).ToArray().ToHostOrder(Endianness.Big), 0)
                : (ushort)1005;

            return _code.Value;
        }
    }

    internal bool HasReservedCode => _data.Length > 1 && (Code == (ushort)CloseStatusCode.Undefined ||
               Code == (ushort)CloseStatusCode.NoStatus ||
               Code == (ushort)CloseStatusCode.Abnormal ||
               Code == (ushort)CloseStatusCode.TlsHandshakeFailure);

    public override string ToString()
    {
        return BitConverter.ToString(_data);
    }

    private static byte[] Append(ushort code, string? reason)
    {
        byte[] ret = code.ToByteArray(Endianness.Big);
        if (string.IsNullOrEmpty(reason))
        {
            return ret;
        }

        List<byte> buff = [.. ret, .. Encoding.UTF8.GetBytes(reason)];

        return [.. buff];
    }

    internal void Mask(byte[] key)
    {
        for (long i = 0; i < _data.Length; i++)
        {
            _data[i] = (byte)(_data[i] ^ key[i % 4]);
        }
    }

    internal byte[] ToArray()
    {
        return _data;
    }
}
