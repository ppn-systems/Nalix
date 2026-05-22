# Nalix.Analyzers.Generators

## Triggers
- Adding a new packet type (`PacketBase<T>` subclass)
- Adding or modifying serialization attributes (`[GenerateFormatter]`, `[SerializeOrder]`, etc.)
- Adding a new config option class to be used with `ConfigurationManager.Bind<T>()`
- Modifying or adding a source generator itself

---

## Rules

### Generator Triggers
| Generator | Trigger condition |
| :--- | :--- |
| `SerializeFormatterGenerator` | Class with `[GenerateFormatter]` |
| `PacketRegistryGenerator` | Class inheriting `PacketBase<T>` with `[SerializeHeader]` |
| `PacketSchemaGenerator` | Same as `SerializeFormatterGenerator` |
| `ConfigurationGenerator` | Class used in a call to `ConfigurationManager.Bind<T>()` |

### KnownNames First
When adding a new attribute that any generator must detect, **add the fully-qualified name to `KnownNames.cs` first** before writing the generator logic that references it. Generators match types by string name only — no assembly reference to runtime Nalix code is allowed.

### Incremental Generator Correctness
- All generators implement `IIncrementalGenerator` — preserve the `IncrementalValueProvider` pipeline to keep IDE performance acceptable
- Never break the caching pipeline by calling non-deterministic APIs (e.g., `DateTime.Now`, `Guid.NewGuid()`) inside a transform step
- Generated source is emitted via `context.AddSource(hintName, sourceText)` — use stable, deterministic `hintName` values

### Build Integration Constraint
Generators are referenced as `OutputItemType="Analyzer" ReferenceOutputAssembly="false"`. The following MSBuild properties are stripped from the generator build context to prevent AOT props leaking into the generator:
```
GlobalPropertiesToRemove="PublishAot;PublishTrimmed;IsTrimmable;IsAotCompatible"
```

---

## Checklists

### Add a new generator-detected attribute
1. Add the fully-qualified attribute name as a constant to `KnownNames.cs`
2. Use `KnownNames.YourAttributeName` in generator logic — never hardcode strings inline
3. Add the attribute definition to `Nalix.Abstractions` (not here)
4. Run `dotnet build` on a consuming project to verify the generator fires

### Modify an existing generator
1. Make the change
2. Run `dotnet build` on the consuming project (e.g., `Nalix.Codec`)
3. Inspect `obj/Debug/net10.0/generated/<GeneratorName>/` to verify the emitted output
4. If trigger conditions changed (e.g., different attribute name): run `dotnet build --no-incremental` to force a clean generation pass

### Debug a generator not firing
1. Check `obj/Debug/net10.0/generated/` — if folder is empty or missing, the trigger condition was not met
2. Verify the consuming project has `OutputItemType="Analyzer" ReferenceOutputAssembly="false"` on the generator project reference
3. Check that the trigger type/attribute matches `KnownNames` exactly (case-sensitive, fully-qualified)
4. Run `dotnet build --no-incremental` to bypass incremental cache

---

## Gotchas

- **Stale generated output after trigger rename**: If you rename the attribute or class that triggers a generator, the old generated files remain in `obj/` until a clean build. Run `dotnet build --no-incremental` or delete `obj/` when changing trigger conditions.

- **Non-deterministic `hintName` breaks incremental caching**: If `hintName` passed to `context.AddSource()` changes between identical inputs (e.g., timestamp-based), the incremental cache treats every build as a change — IDE performance degrades significantly.

- **`FNV1a` hash in `Fnv1a.cs` is for stable name hashing only**: It provides deterministic, order-independent hashing for identifier names in generator output. Do not use it for data integrity or security purposes.

- **`GlobalPropertiesToRemove` is load-bearing**: Removing or changing the AOT property stripping causes generator builds to fail in publish pipelines where `PublishAot=true` is set on the consuming project.

- **Generators run on every keystroke in the IDE**: Any non-trivial allocation inside an `AnalyzeXxx` or generator transform step degrades typing responsiveness for all developers. Keep transforms allocation-free.
