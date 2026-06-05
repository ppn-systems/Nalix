# Configuration Generator
 
The Configuration Generator automates the binding of `.ini` configuration files to strongly-typed C# classes, ensuring consistency and AOT safety.
 
## Source Mapping
 
- `analyzers/Nalix.Analyzers.Generators/ConfigurationGenerator.cs`
 
## Overview
 
Nalix uses a flexible `.ini`-based configuration system. To avoid the performance cost of reflection-based binding at startup, this generator produces partial class implementations that handle the reading and writing of configuration values directly.
 
## How it works
 
1. **Target Identification**: Scans for classes inheriting from `ConfigurationLoader`.
2. **Property Mapping**: Maps each public property to a configuration key.
3. **Partial Implementation**: Generates overrides for `BindProperties` and `CopyPropertiesTo`.
4. **Metadata Preservation**: Respects attributes like `[IniComment]` and `[ConfiguredIgnore]`.
 
## Key Features
 
- **Type-Safe Binding**: Automatically handles primitive types, enums, strings, and common types like `TimeSpan` or `Guid`.
- **Default Value Management**: If a value is missing from the configuration file, the generator writes the current code-defined default value back to the file (with comments).
- **AOT Compatibility**: No reflection is used during the binding process, making it safe for trimmed or AOT-compiled applications.
- **Self-Documenting Configs**: Automatically generates `.ini` comments based on the `[IniComment]` attribute in your code.
 
## Example
 
Your code:
```csharp
[IniComment("Network settings")]
public partial class NetworkOptions : ConfigurationLoader
{
    [IniComment("The port to listen on")]
    public int Port { get; set; } = 8080;
}
```
 
Generated code:
```csharp
partial class NetworkOptions
{
    protected override void BindProperties(IniConfig configFile, string section)
    {
        var _v_Port = configFile.GetInt32(section, "Port");
        if (_v_Port.HasValue) 
            this.Port = _v_Port.Value;
        else 
        {
            configFile.WriteComment(section, "Port", "The port to listen on");
            configFile.WriteValue(section, "Port", this.Port.ToString());
        }
    }
}
```
 
## Related APIs
 
- [Configuration Reference](../../environment/configuration.md)
- [Analyzers Overview](../index.md)
