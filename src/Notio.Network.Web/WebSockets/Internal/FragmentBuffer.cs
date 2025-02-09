using Notio.Network.Web.Enums;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Network.Web.WebSockets.Internal;

internal class FragmentBuffer(Opcode frameOpcode, bool frameIsCompressed) : MemoryStream
{
    private readonly bool _fragmentsCompressed = frameIsCompressed;
    private readonly Opcode _fragmentsOpcode = frameOpcode;

    public void AddPayload(MemoryStream data)
    {
        data.CopyTo(this, 1024);
    }

    public async Task<MessageEventArgs> GetMessage(CompressionMethod compression)
    {
        MemoryStream data = _fragmentsCompressed
            ? await this.CompressAsync(compression, false, CancellationToken.None).ConfigureAwait(false)
            : this;

        return new MessageEventArgs(_fragmentsOpcode, data.ToArray());
    }
}