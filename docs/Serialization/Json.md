# Json Class Documentation

## Overview

The `Json` class in the `Notio.Serialization` namespace provides a simple and lightweight JSON serialization and deserialization library. This library is designed for small tasks and educational purposes, and it does not aim to replace full-featured serializers like Serialization.NET.

## Public API

### Methods

#### Serialize

Serializes an object into a JSON string.

**Method Signature:**

```csharp
public static string Serialize(
    object? obj, 
    bool format = false, 
    string? typeSpecifier = null, 
    string[]? includedNames = null, 
    params string[] excludedNames)
```

**Parameters:**

- `obj`: The object to serialize.
- `format`: If `true`, formats and indents the output. Default is `false`.
- `typeSpecifier`: The type specifier. Leave `null` or empty to avoid setting.
- `includedNames`: The included property names.
- `excludedNames`: The excluded property names.

**Returns:**

- A `string` representing the serialized JSON object.

**Examples:**

```csharp
var obj = new { One = "One", Two = "Two" };
var json = Json.Serialize(obj); // {"One": "One", "Two": "Two"}
```

#### SerializeOnly

Serializes an object, including only the specified property names.

**Method Signature:**

```csharp
public static string SerializeOnly(object? obj, bool format, params string[] includeNames)
```

**Parameters:**

- `obj`: The object to serialize.
- `format`: If `true`, formats and indents the output.
- `includeNames`: The included property names.

**Returns:**

- A `string` representing the serialized JSON object.

**Examples:**

```csharp
var obj = new { One = "One", Two = "Two", Three = "Three" };
var json = Json.SerializeOnly(obj, true, "Two", "Three"); // {"Two": "Two", "Three": "Three"}
```

#### SerializeExcluding

Serializes an object, excluding the specified property names.

**Method Signature:**

```csharp
public static string SerializeExcluding(object? obj, bool format, params string[] excludeNames)
```

**Parameters:**

- `obj`: The object to serialize.
- `format`: If `true`, formats and indents the output.
- `excludeNames`: The excluded property names.

**Returns:**

- A `string` representing the serialized JSON object.

**Examples:**

```csharp
var obj = new { One = "One", Two = "Two", Three = "Three" };
var json = Json.SerializeExcluding(obj, false, "Two", "Three"); // {"One": "One"}
```

#### Deserialize

Deserializes a JSON string into an object.

**Method Signature:**

```csharp
public static object? Deserialize(string json)
```

**Parameters:**

- `json`: The JSON string to deserialize.

**Returns:**

- The deserialized object.

**Examples:**

```csharp
var json = "{\"One\":\"One\",\"Two\":\"Two\",\"Three\":\"Three\"}";
var obj = Json.Deserialize(json); // Deserializes into a Dictionary<string, object>
```

#### Deserialize<`T`>

Deserializes a JSON string into an object of type `T`.

**Method Signature:**

```csharp
public static T Deserialize<T>(string json, JsonSerializerCase jsonSerializerCase = JsonSerializerCase.None)
```

**Parameters:**

- `json`: The JSON string to deserialize.
- `jsonSerializerCase`: The JSON serializer case. Default is `JsonSerializerCase.None`.

**Returns:**

- The deserialized object of type `T`.

**Examples:**

```csharp
var json = "{\"One\":\"One\",\"Two\":\"Two\",\"Three\":\"Three\"}";
var obj = Json.Deserialize<Dictionary<string, object>>(json);
```

### Enums

#### JsonSerializerCase

Specifies the case sensitivity for the JSON serializer.

**Values:**

- `None`: No special case handling.
- `CamelCase`: Convert property names to camelCase.
- `SnakeCase`: Convert property names to snake_case.

## Private API

### Methods

#### SerializePrimitiveValue

Serializes primitive values to their JSON representation.

**Method Signature:**

```csharp
private static string SerializePrimitiveValue(object obj)
```

**Parameters:**

- `obj`: The object to serialize.

**Returns:**

- A `string` representing the serialized primitive value.

## Constants

### JSON Literals

- `TrueLiteral`: `"true"`
- `FalseLiteral`: `"false"`
- `NullLiteral`: `"null"`
- `EmptyObjectLiteral`: `"{ }"`
- `EmptyArrayLiteral`: `"[ ]"`

### Characters

- `OpenObjectChar`: `'{ '`
- `CloseObjectChar`: `'} '`
- `OpenArrayChar`: `'['`
- `CloseArrayChar`: `']'`
- `FieldSeparatorChar`: `','`
- `ValueSeparatorChar`: `':'`
- `StringQuotedChar`: `'"'`

## Additional Examples

### Example: Serializing with JsonPropertyAttribute

```csharp
using Notio.Attributes;

class JsonPropertyExample
{
    [JsonProperty("data")]
    public string Data { get; set; }

    [JsonProperty("ignoredData", true)]
    public string IgnoredData { get; set; }
}

class Example
{
    static void Main()
    {
        var obj = new JsonPropertyExample() { Data = "OK", IgnoredData = "OK" };
        var json = Json.Serialize(obj);
        // Output: {"data": "OK"}
    }
}
```

### Example: Deserializing to a Specific Type

```csharp
using System.Collections.Generic;

class Example
{
    static void Main()
    {
        var json = "{\"One\":\"One\",\"Two\":\"Two\",\"Three\":\"Three\"}";
        var obj = Json.Deserialize<Dictionary<string, object>>(json);
    }
}
```

## Conclusion

The `Json` class provides a simple and efficient way to serialize and deserialize JSON data in .NET applications. While it is not intended to replace more comprehensive libraries, it is a useful tool for small tasks and educational purposes.
