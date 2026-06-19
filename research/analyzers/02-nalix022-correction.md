# NALIX022 Correction Report

> **Date:** 2026-06-19
> **Branch:** `feature/refactor-and-fixes`

---

## What Was Wrong

The previous NALIX022 implementation compared `SerializeOrder` integer values against `PacketHeaderOffset.Region` (12), treating `SerializeOrder` as a byte offset:

```csharp
else if (isPacketBaseType && finalOrder.Value < symbols.PacketHeaderRegionOffset)
{
    Report(context, DiagnosticDescriptors.PacketMemberOverlapsHeaderRegion, ...);
}
```

This was incorrect because **`SerializeOrder` is an ordering/index value, not a byte offset**. `SerializeOrder(0)` is the first payload member and is completely valid. Comparing it against 12 produced false positives for all `SerializeOrder` values 0–11.

The original audit report's claim that "SerializeOrder values below 12 overlap the packet header region" was based on a misunderstanding of the serialization model.

---

## Correct Serializer Semantics

- `SerializeOrder` defines the ordering/index of payload members. It is not a byte offset.
- `SerializeOrder(0)` is valid and common — it means "first payload member."
- `SerializeHeader(N)` marks a member as part of the header section with explicit header order `N`.
- On `PacketBase<TSelf>`-derived types, `SerializeHeader(0)` is **reserved by Nalix internals** (the `FrameBase._header` field uses it for the standard packet header).
- User-defined members on PacketBase types must not use `SerializeHeader(0)`.
- `SerializeHeader(1)` and higher are valid for user-defined header members.
- Non-PacketBase types can use `SerializeHeader(0)` freely.

---

## Changes Made

### DiagnosticDescriptor (`DiagnosticDescriptors.cs`)

Renamed `PacketMemberOverlapsHeaderRegion` → `ReservedPacketHeaderSlot`:

```csharp
public static readonly DiagnosticDescriptor ReservedPacketHeaderSlot = new(
    id: "NALIX022",
    title: "SerializeHeader(0) is reserved on PacketBase types",
    messageFormat: "Member '{0}' uses [SerializeHeader(0)] on a PacketBase-derived type, but header slot 0 is reserved by Nalix packet internals",
    category: "Serialization",
    defaultSeverity: DiagnosticSeverity.Warning,
    isEnabledByDefault: true,
    description: "On PacketBase<TSelf>-derived types, SerializeHeader(0) is reserved by the Nalix packet header. Use a non-zero header order or SerializeOrder for user-defined members.");
```

### Analyzer logic (`NalixUsageAnalyzer.cs`)

Replaced the `finalOrder < PacketHeaderRegionOffset` check with:

```csharp
if (isPacketBaseType
    && headerOrder.HasValue && headerOrder.Value == 0
    && SymbolEqualityComparer.Default.Equals(member.ContainingType, typeSymbol))
{
    Report(context, DiagnosticDescriptors.ReservedPacketHeaderSlot, member, member.Name);
}
```

Key design decisions in the condition:

| Condition | Purpose |
|---|---|
| `isPacketBaseType` | Only applies to `PacketBase<TSelf>`-derived types |
| `headerOrder.HasValue && headerOrder.Value == 0` | Only flags `SerializeHeader(0)`, not `SerializeOrder` |
| `member.ContainingType.Equals(typeSymbol)` | Skips inherited members from framework base classes (e.g., `FrameBase._header`) |

### SupportedDiagnostics

Updated reference from `PacketMemberOverlapsHeaderRegion` to `ReservedPacketHeaderSlot`.

---

## Tests Changed

| Test | Before | After |
|---|---|---|
| `SerializeOrderStartingFromZero_ReportsNalix022` | Expected NALIX022 × 2 | **Renamed** to `SerializeOrderZeroOnPacketBase_DoesNotReportNalix022`, expects no diagnostics |
| `PacketBaseWithSerializeOrderInHeaderRegion_ReportsNalix022` | Expected NALIX022 for `SerializeOrder(5)` | **Renamed** to `PacketBaseWithSerializeOrderBelow12_DoesNotReportNalix022`, expects no diagnostics |
| `PacketBaseWithSerializeOrderAtHeaderOffset_DoesNotReportNalix022` | Expected no diagnostics for `SerializeOrder(12)` | **Removed** (redundant with the above) |
| `PacketBaseWithSerializeHeaderInHeaderRegion_ReportsNalix022` | Expected NALIX022 for `SerializeHeader(5)` | **Renamed** to `SerializeHeaderNonZeroOnPacketBase_DoesNotReportNalix022`, expects no diagnostics |
| *(new)* `SerializeHeaderZeroOnPacketBase_ReportsNalix022` | — | `SerializeHeader(0)` on PacketBase → reports NALIX022 |
| *(new)* `SerializeHeaderZeroOnNonPacketBase_DoesNotReportNalix022` | — | `SerializeHeader(0)` on plain type → no report |
| *(new)* `PacketBaseWithSerializeOrderBelow12_DoesNotReportNalix022` | — | `SerializeOrder(5)` and `SerializeOrder(11)` on PacketBase → no report |

Final test count: **110/110 passing** (unchanged total; replaced 5 NALIX022 tests with 5 corrected ones).

---

## Commands Run and Results

| Command | Result |
|---|---|
| `dotnet build analyzers/Nalix.Analyzers/Nalix.Analyzers.csproj` | ✅ 0 warnings, 0 errors |
| `dotnet test tests/Nalix.Analyzers.Tests/Nalix.Analyzers.Tests.csproj` | ✅ 110 passed, 0 failed |
| `dotnet build src/Nalix.sln` | ✅ 0 NALIX022 warnings, 0 errors |

---

## Remaining Limitations

1. **Inherited `SerializeHeader(0)` from framework internals** is silently skipped by the `ContainingType` check. This is intentional — `FrameBase._header` legitimately uses slot 0. If a user creates their own intermediate base class with `SerializeHeader(0)` and inherits it into a PacketBase type, the diagnostic will NOT fire for the inherited member. This is an acceptable trade-off to avoid false positives on framework internals.

2. **`SerializeHeader` with non-integer constructors** (e.g., `SerializeHeader(PacketHeaderOffset.OpCode)`) resolves to `SerializeHeader(4)` via the enum-to-int conversion. The analyzer correctly handles this because `GetSerializeOrder` reads the constructor argument as an int.

3. **No test for the `FrameBase` inheritance chain** in the analyzer test harness because the test Prelude does not replicate the `FrameBase` → `PacketBase<TSelf>` hierarchy. The `ContainingType` check is validated indirectly by the full solution build producing zero NALIX022 warnings.