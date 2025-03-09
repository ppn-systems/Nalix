# Notio.Serialization Library

## Overview

**Notio.Serialization** is a simple, light-weight JSON library written by Mario to teach Geo how things are done. This library is useful for small tasks but it doesn't represent a full-featured serializer such as the beloved Serialization.NET.

## Features

- Serialize objects to JSON strings.
- Deserialize JSON strings to objects.
- Supports inclusion and exclusion of property names during serialization.
- Formatting and indentation support for serialized JSON.
- Handles primitive values and complex objects.

## Installation

To use Notio.Serialization in your project, include the necessary namespaces:

```csharp
using Notio.Serialization;
using Notio.Serialization.Internal;
using Notio.Serialization.Internal.Reflection;
```

## Usage

### Serialization

You can serialize objects into JSON strings using the `Serialize` method. Below are some examples:

```csharp
using Notio.Serialization;

class Example
{
    static void Main()
    {
        var obj = new { One = "One", Two = "Two" };
        var serial = Json.Serialize(obj); // {"One": "One","Two": "Two"}
    }
}
```

#### Using `JsonPropertyAttribute`

```csharp
using Notio.Serialization;

class Example
{
    class JsonPropertyExample
    {
        [JsonProperty("data")]
        public string Data { get; set; }

        [JsonProperty("ignoredData", true)]
        public string IgnoredData { get; set; }
    }

    static void Main()
    {
        var obj = new JsonPropertyExample() { Data = "OK", IgnoredData = "OK" };
        var serializedObj = Json.Serialize(obj); // {"data": "OK"}
    }
}
```

### Serialization with Options

You can specify various options during serialization such as formatting, type specifier, included and excluded property names.

```csharp
var includedNames = new[] { "Two", "Three" };
var serializedOnly = Json.SerializeOnly(obj, true, includedNames); // {"Two": "Two","Three": "Three"}

var excludeNames = new[] { "Two", "Three" };
var serializedExcluding = Json.SerializeExcluding(obj, false, excludeNames); // {"One": "One"}
```

### Deserialization

You can deserialize JSON strings to objects using the `Deserialize` method. Below are some examples:

```csharp
using Notio.Serialization;

class Example
{
    static void Main()
    {
        var json = "{\"One\":\"One\",\"Two\":\"Two\",\"Three\":\"Three\"}";
        var data = Json.Deserialize(json); // Deserializes to Dictionary<string, object>
    }
}
```

#### Deserialize to Specific Type

```csharp
var json = "{\"One\":\"One\",\"Two\":\"Two\",\"Three\":\"Three\"}";
var data = Json.Deserialize<MyType>(json);
```

## API Reference

### Public Methods

- `string Serialize(object? obj, bool format = false, string? typeSpecifier = null, string[]? includedNames = null, params string[] excludedNames)`
- `string Serialize(object? obj, JsonSerializerCase jsonSerializerCase, bool format = false, string? typeSpecifier = null)`
- `string SerializeOnly(object? obj, bool format, params string[] includeNames)`
- `string SerializeExcluding(object? obj, bool format, params string[] excludeNames)`
- `object? Deserialize(string json)`
- `T Deserialize<T>(string json, JsonSerializerCase jsonSerializerCase = JsonSerializerCase.None)`
- `T Deserialize<T>(string json, bool includeNonPublic)`
- `object? Deserialize(string json, Type resultType, bool includeNonPublic = false, JsonSerializerCase jsonSerializerCase = JsonSerializerCase.None)`

## Contributing

Contributions are welcome! Please submit a pull request or raise an issue to discuss any changes.
