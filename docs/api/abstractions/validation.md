# Validation Attributes

Nalix provides AOT-safe validation attributes in `Nalix.Abstractions.Validation` as replacements for `System.ComponentModel.DataAnnotations`. These attributes are read by the `ConfigurationGenerator` source generator at compile time — no runtime reflection or DataAnnotations dependency is required.

## Source Mapping

- `src/Nalix.Abstractions/Validation/ValueRangeAttribute.cs`
- `src/Nalix.Abstractions/Validation/DurationRangeAttribute.cs`
- `src/Nalix.Abstractions/Validation/LengthAttribute.cs`
- `src/Nalix.Abstractions/Validation/RequiredAttribute.cs`
- `src/Nalix.Abstractions/Validation/AllowedEnumAttribute.cs`
- `src/Nalix.Abstractions/Exceptions/ValidationException.cs`

## Attributes

### `ValueRangeAttribute`

Specifies the numeric inclusive range that a property value must fall within.

- **Namespace:** `Nalix.Abstractions.Validation`
- **Target:** Properties
- **Constructors:**

  ```csharp
  ValueRangeAttribute(double minimum, double maximum)
  ValueRangeAttribute(long minimum, long maximum)
  ```

- **Properties:** `Minimum`, `Maximum`, `MinimumInt64`, `MaximumInt64`, `UseInt64`
- Use the `(long, long)` overload when the range bound includes `int.MaxValue` or `long.MaxValue`.

### `DurationRangeAttribute`

Specifies the inclusive `TimeSpan` range that a property value must fall within.

- **Namespace:** `Nalix.Abstractions.Validation`
- **Target:** Properties
- **Constructor:**

  ```csharp
  DurationRangeAttribute(string minimum, string maximum)
  ```

- **Properties:** `Minimum`, `Maximum` (parseable time strings, e.g. `"00:00:01"`, `"1.00:00:00"`)

### `RequiredAttribute`

Specifies that a property value must not be `null`.

- **Namespace:** `Nalix.Abstractions.Validation`
- **Target:** Properties

For string properties that must also be non-empty, prefer `LengthAttribute` with a minimum of 1.

### `LengthAttribute`

Specifies the minimum length for a string or collection property.

- **Namespace:** `Nalix.Abstractions.Validation`
- **Target:** Properties
- **Constructor:**

  ```csharp
  LengthAttribute(int minimum)
  ```

- **Properties:** `Minimum`
- Applies to `string`, arrays, and collection types.

### `AllowedEnumAttribute`

Specifies that a property value must be a defined member of its enum type.

- **Namespace:** `Nalix.Abstractions.Validation`
- **Target:** Properties

## ValidationException

Thrown when a configuration or data validation check fails. This is the AOT-safe replacement for `System.ComponentModel.DataAnnotations.ValidationException`.

- **Namespace:** `Nalix.Abstractions.Exceptions`
- **Constructors:**

  ```csharp
  ValidationException()
  ValidationException(string message)
  ValidationException(string message, Exception innerException)
  ```

## Usage in Options

Nalix option types use these attributes and call `ValidateDataAnnotations()` in their `Validate()` method:

```csharp
using Nalix.Abstractions.Validation;
using Nalix.Environment.Configuration.Binding;

public sealed class MyOptions : ConfigurationLoader, IValidatableConfiguration
{
    [ValueRange(1, 10_000)]
    public int MaxItems { get; set; } = 100;

    [DurationRange("00:00:01", "1.00:00:00")]
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);

    public void Validate() => this.ValidateDataAnnotations();
}
```

## Related APIs

- [Configuration](../environment/configuration.md)
- [Abstractions Overview](./index.md)
