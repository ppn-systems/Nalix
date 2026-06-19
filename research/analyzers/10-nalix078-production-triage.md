# Phase 2C.1: NALIX078 Production Warning Triage

> **Date:** 2026-06-19
> **Branch:** `feature/refactor-and-fixes`

---

## Warning Locations

| # | File | Line | Code | Assembly |
|---|---|---|---|---|
| 1 | `src/Nalix.Codec/Serialization/Internal/Types/TypeMetadata.Cache.cs` | 57 | `prop?.GetValue(null)` | Nalix.Codec |
| 2 | `src/Nalix.Hosting/Internal/DiagnosticChannel.cs` | 222 | `accessor.MessageProperty?.GetValue(value.Value)` | Nalix.Hosting |
| 3 | `src/Nalix.Hosting/Internal/DiagnosticChannel.cs` | 223 | `accessor.ExceptionProperty?.GetValue(value.Value)` | Nalix.Hosting |
| 4 | `src/Nalix.Hosting/Internal/DiagnosticChannel.cs` | 244 | `accessor.OtherProperties[i].GetValue(value.Value)` | Nalix.Hosting |

---

## Classification

### Warning 1: TypeMetadata.Cache.cs:57

**Code:**
```csharp
PropertyInfo? prop = type.GetProperty(nameof(IFixedSizeSerializable.Size), Flags);
if (prop?.GetValue(null) is int size)
```

**Classification:** Intentional non-hot-path metadata inspection.

**Reasoning:**
- Runs in a static constructor (`Cache<T>.cctor`) — one-time per type, not on serialization hot paths.
- The type parameter `T` is annotated with `[DynamicallyAccessedMembers(PropertyAccess)]`, ensuring the trimmer preserves properties.
- Reads a static constant (`IFixedSizeSerializable.Size`), not an instance property on a live object.
- The surrounding code has `try/catch` for graceful fallback if reflection fails.
- No source-generated alternative exists for this generic type metadata cache pattern yet.

**Resolution:** `#pragma warning disable NALIX078` around the single `GetValue` call with explanatory comment.

### Warnings 2-4: DiagnosticChannel.cs:222, 223, 244

**Code:**
```csharp
string? message = accessor.MessageProperty?.GetValue(value.Value) as string;
Exception? exception = accessor.ExceptionProperty?.GetValue(value.Value) as Exception;
// ...
object? propVal = accessor.OtherProperties[i].GetValue(value.Value);
```

**Classification:** Intentional diagnostic inspection path.

**Reasoning:**
- Bridges `DiagnosticListener` events to `ILogger` — observational/logging infrastructure, not runtime activation or packet dispatch.
- The `ObjectAccessor` constructor already has `[UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "Diagnostic payload property scanning is observational only")]`.
- The `ObjectAccessor` cache (`ConcurrentDictionary<Type, ObjectAccessor>`) is populated once per event type, not per event.
- Not on any networking hot path, serialization path, or AOT-critical activation path.
- Future source-generated diagnostic adapters could replace this, but that requires a larger redesign.

**Resolution:** `#pragma warning disable NALIX078` around each `GetValue` call with explanatory comment.

---

## Selected Resolution

**Approach: Narrow `#pragma` suppressions (Option C from task guidance).**

No analyzer allowlist logic was added. The analyzer continues to flag all `PropertyInfo.GetValue` usages in Core assemblies. The 4 known intentional cases are suppressed locally with comments explaining why reflection is used and what the future replacement would be.

**Rationale for not adding analyzer allowlist:**
- An allowlist based on containing type name or file path would be fragile and could hide real risks.
- `PropertyInfo.GetValue` is genuinely dangerous in AOT hot paths — suppressing it globally would weaken the diagnostic.
- The 4 cases are clearly non-hot-path and non-activation, making narrow suppressions the correct choice.

---

## Files Changed

| File | Change |
|---|---|
| `src/Nalix.Codec/Serialization/Internal/Types/TypeMetadata.Cache.cs` | Added `#pragma warning disable/restore NALIX078` around `GetValue` call with justification comment |
| `src/Nalix.Hosting/Internal/DiagnosticChannel.cs` | Added `#pragma warning disable/restore NALIX078` around 3 `GetValue` calls with justification comments |

---

## Validation Results

| Command | Result |
|---|---|
| `dotnet build analyzers/Nalix.Analyzers/Nalix.Analyzers.csproj` | ✅ 0 errors |
| `dotnet test tests/Nalix.Analyzers.Tests/Nalix.Analyzers.Tests.csproj` | ✅ 138 passed, 0 failed |
| `dotnet build src/Nalix.sln` | ✅ Build succeeded, **0 NALIX078 warnings** |

---

## Remaining Limitations

1. **Future source-generated alternatives:** Both suppressed cases could benefit from source-generated replacements in a future phase:
   - `TypeMetadata.Cache`: Source-generate `IFixedSizeSerializable.Size` lookup per type.
   - `DiagnosticChannel`: Source-generate typed diagnostic event adapters.

2. **No analyzer allowlist:** If more intentional reflection patterns emerge, a narrow allowlist helper (`IsNonHotPathMetadataInspection`) could be added. Current approach of per-site suppressions is preferred until the pattern is more common.

3. **NALIX078 still catches all other `PropertyInfo.GetValue` usages.** The suppressions are strictly local and do not weaken the diagnostic for new code.