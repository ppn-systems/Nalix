using Notio.Network.Web.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Notio.Shared.Extensions;
using Notio.Network.Web.WebSockets.Internal.Enums;

namespace Notio.Network.Web.WebSockets.Internal;

internal class WebSocketStream(byte[] data, Opcode opcode, CompressionMethod compression) : MemoryStream(data)
{
    private const int FragmentLength = 1016;

    private readonly CompressionMethod _compression = compression;
    private readonly Opcode _opcode = opcode;

    public IEnumerable<WebSocketFrame> GetFrames()
    {
        bool compressed = _compression != CompressionMethod.None;
        MemoryStream stream = compressed
            ? this.CompressAsync(_compression, true, CancellationToken.None).Await()
            : this;

        long len = stream.Length;

        /* Not fragmented */

        if (len == 0)
        {
            yield return new WebSocketFrame(Fin.Final, _opcode, Array.Empty<byte>(), compressed);
            yield break;
        }

        long quo = len / FragmentLength;
        int rem = (int)(len % FragmentLength);

        byte[] buff;

        if (quo == 0)
        {
            buff = new byte[rem];

            if (stream.Read(buff, 0, rem) == rem)
            {
                yield return new WebSocketFrame(Fin.Final, _opcode, buff, compressed);
            }

            yield break;
        }

        buff = new byte[FragmentLength];
        if (quo == 1 && rem == 0)
        {
            if (stream.Read(buff, 0, FragmentLength) == FragmentLength)
            {
                yield return new WebSocketFrame(Fin.Final, _opcode, buff, compressed);
            }

            yield break;
        }

        /* Send fragmented */

        // Begin
        if (stream.Read(buff, 0, FragmentLength) != FragmentLength)
        {
            yield break;
        }

        yield return new WebSocketFrame(Fin.More, _opcode, buff, compressed);

        long n = rem == 0 ? quo - 2 : quo - 1;
        for (int i = 0; i < n; i++)
        {
            if (stream.Read(buff, 0, FragmentLength) != FragmentLength)
            {
                yield break;
            }

            yield return new WebSocketFrame(Fin.More, Opcode.Cont, buff, compressed);
        }

        // End
        if (rem == 0)
        {
            rem = FragmentLength;
        }
        else
        {
            buff = new byte[rem];
        }

        if (stream.Read(buff, 0, rem) == rem)
        {
            yield return new WebSocketFrame(Fin.Final, Opcode.Cont, buff, compressed);
        }
    }
}
