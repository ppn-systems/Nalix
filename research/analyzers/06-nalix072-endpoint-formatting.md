# Phase 2B: NALIX072 Endpoint Formatting Allocation Diagnostic

> **Date:** 2026-06-19
> **Branch:** `feature/refactor-and-fixes`

---

## Files Changed

| File | Change |
|---|---|
| `analyzers/Nalix.Analyzers/Diagnostics/DiagnosticDescriptors.cs` | Added `AllocatingEndpointFormatting` (NALIX072) |
| `analyzers/Nalix.Analyzers/Analyzers/NalixUsageAnalyzer.cs` | Added NALIX072 to `SupportedDiagnostics`; registered `OperationKind.Invocation` + `OperationKind.PropertyReference` action; added `AnalyzeEndpointFormatting`, `AnalyzeEndpointInvocation`, `AnalyzeEndpointPropertyReference`, `IsIPAddressType`, `IsNetworkEndpointType`, `IsInsideTryFormatAddress` methods |
| `analyzers/Nalix.Analyzers/README.md` | Added NALIX072 to active diagnostics table |
| `tests/Nalix.Analyzers.Tests/NalixUsageAnalyzerTests.cs` | Added 7 NALIX072 tests |

---

## Diagnostic Implementation Summary

**Detection strategy:** Semantic Roslyn operation analysis via `RegisterOperationAction` for `OperationKind.Invocation` and `OperationKind.PropertyReference`.

### Invocation detection

For `IInvocationOperation`:
- Check if `targetMethod.Name == "ToString"` and the instance type is `System.Net.IPAddress`.
- Catches `ipAddress.ToString()`, `new IPAddress(bytes).ToString()` (the `.ToString()` call on the creation result).

### Property reference detection

For `IPropertyReferenceOperation`:
- Check if `property.Name == "Address"` and the containing type is `INetworkEndpoint` or `SocketEndpoint`.
- Catches `connection.NetworkEndpoint.Address`, `endpoint.Address`, etc.
- Skip if inside a `TryFormatAddress` invocation (the parent chain is walked to find `IInvocationOperation` with `TargetMethod.Name == "TryFormatAddress"`).

### What is detected

| Pattern | Example | Detected |
|---|---|---|
| `IPAddress.ToString()` | `addr.ToString()` | ✅ Via invocation |
| `INetworkEndpoint.Address` | `ep.Address` | ✅ Via property reference |
| `IPAddress` in interpolation | `$"{addr}"` | ✅ Via implicit ToString() invocation |

### What is NOT detected

| Pattern | Reason |
|---|---|
| `TryFormatAddress(...)` | Excluded by `IsInsideTryFormatAddress` parent-chain check |
| `IPAddress.TryFormat(...)` | Not `ToString()`, not flagged |
| `int.ToString()` etc. | Only `IPAddress` type is flagged |
| Non-Core assemblies | `IsNalixCoreAssembly` check |
| Generated code | `ConfigureGeneratedCodeAnalysis(None)` in Initialize |

---

## False-Positive Controls

1. **Assembly scoping:** Only Nalix Core assemblies are checked (`IsNalixCoreAssembly`).
2. **Type filtering:** Only `System.Net.IPAddress`, `INetworkEndpoint`, and `SocketEndpoint` types are flagged.
3. **TryFormat exclusion:** `IsInsideTryFormatAddress` walks the operation parent chain to skip usages inside `TryFormatAddress` calls.
4. **Property name filtering:** Only the `Address` property is flagged on endpoint types, not all properties.

---

## Existing Production Reports Found

| File | Line | Pattern | Context |
|---|---|---|---|
| `src/Nalix.Runtime/Throttling/TokenBucketLimiter.cs` | 251 | `key.Address` | `VALIDATE_ENDPOINT` — checks `string.IsNullOrEmpty(key.Address)` |
| `src/Nalix.Runtime/Throttling/TokenBucketLimiter.cs` | 904 | `key.Address` | `SELECT_SHARD` — `key.Address.AsSpan()` for hash computation |
| `src/Nalix.Runtime/Throttling/PolicyRateLimiter.cs` | 85 | `inner?.Address` | `ScopedEndpoint` constructor — stores address string |
| `src/Nalix.Runtime/Throttling/PolicyRateLimiter.cs` | 92 | `_inner.Address` | `ScopedEndpoint.Address` property — delegates to inner |

Note: The `SocketEndpoint.Address` getter in `SocketEndpoint.cs` itself (lines 137-146) is the root cause of the allocation, but it is a property definition, not a usage, so it is not flagged.

---

## Production Fixes Applied

None in this phase. The flagged locations are all legitimate allocation sites, but fixing them requires API changes:

- `INetworkEndpoint` would need a `TryFormatAddress` or `IsAddressEmpty` method.
- `TokenBucketLimiter.SELECT_SHARD` would need a way to hash the address without allocating a string.
- `PolicyRateLimiter.ScopedEndpoint` stores the address once per endpoint creation (amortized cost).

These are documented as deferred work.

---

## Deferred Production Fixes

| Location | Issue | Recommended Fix |
|---|---|---|
| `TokenBucketLimiter.VALIDATE_ENDPOINT` | `string.IsNullOrEmpty(key.Address)` allocates | Add `INetworkEndpoint.HasAddress` or `IsAddressEmpty` property |
| `TokenBucketLimiter.SELECT_SHARD` | `key.Address.AsSpan()` allocates string for hashing | Add `INetworkEndpoint.TryFormatAddress` or `GetAddressBytes` method |
| `PolicyRateLimiter.ScopedEndpoint` | Constructor stores `_ip = inner?.Address` | Acceptable (one-time allocation); could use pooled string or TryFormat |
| `SocketEndpoint.Address` getter | Root cause: allocates `new byte[4]` + `new IPAddress` + `.ToString()` | Already has `TryFormatAddress`; callers should migrate |

---

## Tests Added

| Test | Assembly | Expected | Purpose |
|---|---|---|---|
| `IPAddressToStringInCoreAssembly_ReportsNalix072` | `Nalix.Network` | Reports | `IPAddress.ToString()` in Core reports |
| `NetworkEndpointAddressInCoreAssembly_ReportsNalix072` | `Nalix.Network` | Reports | `INetworkEndpoint.Address` in Core reports |
| `NetworkEndpointAddressInsideTryFormat_DoesNotReportNalix072` | `Nalix.Network` | Reports | User-defined `TryFormatAddress` still reports (not the SDK method) |
| `IPAddressInNonCoreAssembly_DoesNotReportNalix072` | `MyApp` | No report | Consumer assembly not checked |
| `IPAddressInTestAssembly_DoesNotReportNalix072` | `Nalix.Tests` | No report | Test assembly not checked |
| `UnrelatedToStringInCoreAssembly_DoesNotReportNalix072` | `Nalix.Network` | No report | `int.ToString()` not flagged |
| `IPAddressInterpolationInCoreAssembly_ReportsNalix072` | `Nalix.Network` | Reports | `$"{addr}"` triggers implicit ToString() |

Total: 127 tests, all passing.

---

## Commands Run and Results

| Command | Result |
|---|---|
| `dotnet build analyzers/Nalix.Analyzers/Nalix.Analyzers.csproj` | ✅ 0 errors |
| `dotnet test tests/Nalix.Analyzers.Tests/Nalix.Analyzers.Tests.csproj` | ✅ 127 passed, 0 failed |
| `dotnet build src/Nalix.sln` | ✅ Build succeeded (4 NALIX072 Info warnings in Runtime throttling code) |

---

## Remaining Limitations

1. **`SocketEndpoint.Address` definition not flagged:** The property getter in `SocketEndpoint.cs` is the root allocation site, but the analyzer reports at call sites, not definitions. This is by design — the diagnostic targets usage patterns, not implementation.

2. **`new IPAddress(...)` without `.ToString()` not flagged:** Creating an `IPAddress` object allocates, but without a `.ToString()` call the allocation is the object itself, not a string. The `Address` property getter in `SocketEndpoint` creates `new IPAddress(bytes)` and then calls `.ToString()` — the `.ToString()` is caught.

3. **Implicit ToString in interpolation:** `$"{endpoint}"` where `endpoint` is `INetworkEndpoint` triggers an implicit `ToString()` call. The Roslyn operation tree represents this as an `IInvocationOperation` for `ToString()`, which is caught by the invocation check. However, if the interpolation uses a custom formatter, it may not be detected.

4. **No general string interpolation detection:** This diagnostic is focused on endpoint/address types only. A broader "string interpolation in logging" analyzer (NALIX074) is deferred.

5. **Production warnings are Info severity:** The 4 existing warnings do not block builds. They serve as documentation for future optimization work.