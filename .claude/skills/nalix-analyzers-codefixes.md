# Nalix.Analyzers.CodeFixes

## Role

Roslyn code fix providers paired with `Nalix.Analyzers` diagnostics. Provides automated IDE quick-fixes for each diagnostic rule.

**Dependencies:** `Nalix.Analyzers`, `Microsoft.CodeAnalysis.CSharp.Workspaces`, `System.Composition`

**Target Framework:** `netstandard2.0`

## Code Fix Providers

| Provider | Fixes |
| :--- | :--- |
| `ConfigurationIgnoreCodeFixProvider` | Adds `[ConfiguredIgnore]` attribute |
| `DispatchLoopCountCodeFixProvider` | Fixes dispatch loop count violations |
| `DuplicateSerializeOrderCodeFixProvider` | Resolves duplicate `[SerializeOrder]` values |
| `GenericPacketHandlerCodeFixProvider` | Fixes generic packet handler signatures |
| `MiddlewareCodeFixProvider` | Corrects middleware registration patterns |
| `NullMiddlewareCodeFixProvider` | Handles null middleware references |
| `PacketControllerCodeFixProvider` | Fixes packet controller patterns |
| `PacketDeserializeCodeFixProvider` | Corrects packet deserialization usage |
| `PacketOpcodeCodeFixProvider` | Fixes opcode attribute issues |
| `PacketRegistryDeserializerCodeFixProvider` | Corrects registry deserializer usage |
| `PacketSelfTypeCodeFixProvider` | Fixes `PacketBase<T>` self-type parameter |
| `RedundantPacketCastCodeFixProvider` | Removes unnecessary packet casts |
| `RequestOptionsConsistencyCodeFixProvider` | Ensures request option consistency |
| `ResetForPoolCodeFixProvider` | Adds missing pool reset logic |
| `SerializationConflictCodeFixProvider` | Resolves serialization attribute conflicts |
| `SerializeOrderMissingCodeFixProvider` | Adds missing `[SerializeOrder]` attributes |

## Code Fix Pattern

Each provider follows the standard Roslyn code fix pattern:
1. Register for specific diagnostic IDs from `DiagnosticDescriptors`.
2. Compute fix actions via `RegisterCodeFixesAsync`.
3. Apply `SyntaxNode` transformations to the document.
4. Uses `[ExportCodeFixProvider]` with `System.Composition` for MEF discovery.

## Anti-Patterns

- Do NOT modify multiple documents in a single code fix — keep fixes scoped.
- Do NOT use `SyntaxFactory` without preserving trivia (whitespace/comments).
- Do NOT forget `System.Composition` exports — the fix won't be discovered by the IDE.
