# Nalix.Analyzers.CodeFixes

> Roslyn code fix providers for automated IDE quick-fixes.

**Nalix.Analyzers.CodeFixes** works in tandem with `Nalix.Analyzers` to provide seamless, automated corrections for diagnostic rules. It ensures that Nalix's high-performance patterns and architectural constraints are easy to maintain and adopt within the IDE.

## Key Code Fixes

| Provider | Purpose |
| :--- | :--- |
| `SerializationConflictCodeFixProvider` | Resolves conflicting serialization attributes |
| `SerializeOrderMissingCodeFixProvider` | Automatically adds missing `[SerializeOrder]` |
| `PacketSelfTypeCodeFixProvider` | Fixes `PacketBase<T>` self-type parameters |
| `ResetForPoolCodeFixProvider` | Injects missing pool reset logic |
| `ConfigurationIgnoreCodeFixProvider` | Adds `[ConfiguredIgnore]` where appropriate |

## Installation

```bash
dotnet add package Nalix.Analyzers.CodeFixes
```

> **Note:** This package is typically installed as a development dependency and does not add any runtime overhead to your production binaries.

## Documentation

For a full list of supported code fixes and their diagnostic counterparts, see the [Analyzers & Code Fixes guide](https://ppn-system.me/api/analyzers/).
