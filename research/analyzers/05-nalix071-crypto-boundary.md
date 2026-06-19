# Phase 2A: NALIX071 Crypto Boundary Diagnostic

> **Date:** 2026-06-19
> **Branch:** `feature/refactor-and-fixes`

---

## Files Changed

| File | Change |
|---|---|
| `analyzers/Nalix.Analyzers/Diagnostics/DiagnosticDescriptors.cs` | Added `DisallowedCryptographyUsage` (NALIX071) |
| `analyzers/Nalix.Analyzers/Analyzers/NalixUsageAnalyzer.cs` | Added NALIX071 to `SupportedDiagnostics`; registered `OperationKind.Invocation` + `OperationKind.ObjectCreation` action; added `AnalyzeCryptographyUsage`, `IsInCryptoNamespace`, `IsAllowedCryptographyUsage`, `IsApprovedCryptoShimType` methods |
| `analyzers/Nalix.Analyzers/README.md` | Added NALIX071 to active diagnostics table |
| `tests/Nalix.Analyzers.Tests/NalixUsageAnalyzerTests.cs` | Added 7 NALIX071 tests |

---

## Diagnostic Implementation Summary

**Detection strategy:** Semantic Roslyn analysis via `RegisterOperationAction` for `OperationKind.Invocation` and `OperationKind.ObjectCreation`.

For each operation:
1. Check if the compilation assembly is a Nalix Core assembly (`IsNalixCoreAssembly`).
2. Get the containing type of the invoked method or created object.
3. Check if the type's namespace chain contains `System.Security.Cryptography` (`IsInCryptoNamespace`).
4. If allowed by the allowlist (`IsAllowedCryptographyUsage`), skip.
5. Otherwise, report NALIX071.

**What is detected:**
- `SHA256.HashData(...)`, `SHA256.Create()`, `Aes.Create()`, etc.
- `RandomNumberGenerator.Fill(...)`, `RandomNumberGenerator.GetBytes(...)`
- `new HMACSHA256(...)`, any crypto type instantiation
- Any type or method under `System.Security.Cryptography.*`

**What is NOT detected:**
- `using System.Security.Cryptography;` alone (only reports when the imported types are actually used)
- References in non-Core assemblies
- Allowlisted usages (see below)

---

## Allowlist Policy

| Allowlisted Pattern | Rationale |
|---|---|
| `CryptographicOperations.FixedTimeEquals(...)` | Temporary compat shim. Nalix has `BitwiseOperations.FixedTimeEquals` but this BCL call is allowed until all call sites are migrated. |
| Types named `OsCsprng`, `OsRandom`, `PlatformCsprng` | Platform CSPRNG fallback code where Nalix intentionally delegates to OS-provided CSPRNG APIs. Checked by containing type name. |

The allowlist is intentionally narrow. Adding new exceptions requires updating `IsAllowedCryptographyUsage` and `IsApprovedCryptoShimType` in the analyzer.

---

## Current Crypto Usages Found

| File | Usage | Status |
|---|---|---|
| `src/Nalix.Environment/Random/OsCsprng.cs:304` | `System.Security.Cryptography.RandomNumberGenerator.Fill(b)` | **Allowlisted** — `OsCsprng` type name matches approved shim pattern |
| `src/Nalix.Codec/Security/ProofOfWork.cs:75` | `CryptographicOperations.FixedTimeEquals(expectedMac, providedMac)` | **Allowlisted** — `FixedTimeEquals` method is in the explicit allowlist |
| `tests/Nalix.Network.Tests/ConnectionHubTests.cs` | `RandomNumberGenerator.GetBytes(...)` | **Not reported** — test assembly, not Core |
| `tests/Nalix.Framework.Tests/Cryptography/*.cs` | `RandomNumberGenerator.Fill(...)` | **Not reported** — test assembly |
| `tests/Nalix.Codec.AotCompare/Program.cs` | `SHA256.HashData(...)` | **Not reported** — test/benchmark assembly |

---

## Production Code Changes

No production code changes in this phase. Both existing usages are covered by the allowlist:

1. `OsCsprng.cs` — platform CSPRNG fallback, allowlisted by type name.
2. `ProofOfWork.cs` — `CryptographicOperations.FixedTimeEquals`, allowlisted by method name. A follow-up could migrate this to `BitwiseOperations.FixedTimeEquals` and remove the allowlist entry.

---

## Tests Added

| Test | Assembly | Expected | Purpose |
|---|---|---|---|
| `CryptoSha256InCoreAssembly_ReportsNalix071` | `Nalix.Codec` | Reports | SHA256.HashData in Core reports |
| `CryptoRngFillInCoreAssembly_ReportsNalix071` | `Nalix.Environment` | Reports | RandomNumberGenerator.Fill in Core reports |
| `OsCsprngAllowlistInCoreAssembly_DoesNotReportNalix071` | `Nalix.Environment` | No report | OsCsprng-type class is allowlisted |
| `NalixInternalCryptoInCoreAssembly_DoesNotReportNalix071` | `Nalix.Codec` | No report | Nalix internal hashing is fine |
| `CryptoInNonCoreAssembly_DoesNotReportNalix071` | `MyApp` | No report | Consumer assembly not checked |
| `CryptoInTestAssembly_DoesNotReportNalix071` | `Nalix.Tests` | No report | Test assembly not checked |
| `FixedTimeEqualsAllowlistInCoreAssembly_DoesNotReportNalix071` | `Nalix.Codec` | No report | FixedTimeEquals is allowlisted |

Total: 120 tests, all passing.

---

## Commands Run and Results

| Command | Result |
|---|---|
| `dotnet build analyzers/Nalix.Analyzers/Nalix.Analyzers.csproj` | ✅ 0 warnings, 0 errors |
| `dotnet test tests/Nalix.Analyzers.Tests/Nalix.Analyzers.Tests.csproj` | ✅ 120 passed, 0 failed |
| `dotnet build src/Nalix.sln` | ✅ 0 NALIX071 warnings, 0 errors |

---

## Remaining Limitations

1. **Allowlist maintenance:** New platform shim types must be added to `IsApprovedCryptoShimType` manually. The list is currently `OsCsprng`, `OsRandom`, `PlatformCsprng`.

2. **FixedTimeEquals allowlist is temporary:** Once all call sites migrate from `CryptographicOperations.FixedTimeEquals` to `BitwiseOperations.FixedTimeEquals`, the allowlist entry should be removed and the usage in `ProofOfWork.cs` should be flagged.

3. **Using directive not reported:** A `using System.Security.Cryptography;` that imports unused types will not trigger NALIX071. This is intentional — unused usings are a style issue, not a security boundary violation.

4. **Indirect crypto usage:** If a helper method in a non-crypto namespace wraps a crypto call, the diagnostic reports on the helper's invocation site, not the original crypto call. This is correct behavior — the boundary is at the assembly level.