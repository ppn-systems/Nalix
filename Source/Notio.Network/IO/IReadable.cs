using System.IO;

namespace Notio.Network.IO;

public interface IReadable
{
    void Read(BinaryReader reader);
}
