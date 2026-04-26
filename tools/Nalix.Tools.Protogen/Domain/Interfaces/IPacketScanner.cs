using System.Collections.Generic;
using Nalix.Tools.Protogen.Domain.Models;

namespace Nalix.Tools.Protogen.Domain.Interfaces;

public interface IPacketScanner
{
    List<PacketDefinition> Scan(IEnumerable<string> inputPaths);
}
