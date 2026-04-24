# ⚙️ Nalix AI Skill — Configuration & INI Binding

This skill covers the `Nalix.Framework.Configuration` system, which provides a high-performance, attribute-driven way to manage application settings via INI files.

---

## 🏗️ The Configuration Manager

`ConfigurationManager` is the central hub for all Nalix settings. It is designed to be fast and memory-efficient.

### Key Features:
- **`ConfigurationLoader`**: The base class for all configuration objects.
- **INI Mapping:** Automatically maps INI sections and keys to C# properties using reflection (cached).
- **Default Values:** Supports setting default values directly in the class definition.

---

## 📜 Attributes & Binding

### `[IniComment]`
Adds a comment line above the section or key in the generated INI file.

### `[IniSection]`
Specifies the section name in the INI file. If omitted, the class name is used.

### `[IniIgnore]`
Prevents a property from being serialized to or deserialized from the INI file.

---

## 🛠️ Usage Patterns

### Defining Options
```csharp
[IniSection("Network")]
public sealed class MyOptions : ConfigurationLoader
{
    [IniComment("The port to listen on")]
    public ushort Port { get; set; } = 8080;

    [IniComment("Maximum allowed connections")]
    public int MaxConnections { get; set; } = 1000;
}
```

### Loading Options
```csharp
var options = ConfigurationManager.Instance.Get<MyOptions>();
```

---

## 🔄 Hot-Reloading (If Supported)

Nalix supports watching configuration files for changes. When a file is modified, the `ConfigurationManager` can re-bind the objects and notify listeners.

- **`OnReloaded` Event:** Subscribe to this event in your services to react to configuration changes at runtime without restarting the server.

---

## 🛡️ Common Pitfalls

- **Type Mismatch:** Ensure the property types in C# match the values in the INI file (e.g., don't put a string in an `int` property).
- **Missing Sections:** If a section is missing in the INI file, the `ConfigurationManager` will use the default values defined in the class.
- **ReadOnly Properties:** The binder can only set public properties with a setter.
