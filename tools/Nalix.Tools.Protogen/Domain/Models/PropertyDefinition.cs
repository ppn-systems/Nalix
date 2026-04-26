using System.Collections.Generic;

namespace Nalix.Tools.Protogen.Domain.Models;

public class PropertyDefinition
{
    public string Name { get; set; } = string.Empty;
    public string CSharpType { get; set; } = string.Empty;
    public TypeKind Kind { get; set; }
    public int Order { get; set; }
    public bool IsNullable { get; set; }
    
    // For collections
    public string? ElementType { get; set; }
    public string? KeyType { get; set; }
    public string? ValueType { get; set; }
    
    // For ValueTuple
    public List<string> TupleElements { get; set; } = new();
}
