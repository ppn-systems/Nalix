using System.Collections.Generic;
using Nalix.Tools.Protogen.Domain.Models;

namespace Nalix.Tools.Protogen.Domain.Interfaces;

public interface ICodeGenerator
{
    string LanguageName { get; }
    
    /// <summary>
    /// Generates code files for the given packets.
    /// Returns a dictionary where Key is the File Name and Value is the File Content.
    /// </summary>
    Dictionary<string, string> Generate(IEnumerable<PacketDefinition> packets);
}
