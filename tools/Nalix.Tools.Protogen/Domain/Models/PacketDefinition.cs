using System.Collections.Generic;

namespace Nalix.Tools.Protogen.Domain.Models;

public class PacketDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public uint MagicNumber { get; set; }
    public ushort? OpCode { get; set; }
    public List<PropertyDefinition> Properties { get; set; } = new();
}
