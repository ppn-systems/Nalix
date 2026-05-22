# Nalix.Analyzers.CodeFixes

## Triggers
- Adding a code fix for a new `NAL0xxx` diagnostic
- An existing code fix is not appearing in the IDE
- Modifying how the IDE auto-corrects a Nalix pattern violation

---

## Rules

### One Fix Per Diagnostic
Each code fix provider fixes one specific diagnostic ID. Do not fix multiple unrelated diagnostics in a single provider — keep fixes scoped.

### MEF Discovery Is Mandatory
Every provider must have `[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MyCodeFixProvider))]` and `[Shared]` (from `System.Composition`). Without these attributes, the IDE never discovers the fix — it won't appear in the lightbulb menu.

### Trivia Preservation
When using `SyntaxFactory` to create or modify nodes, always preserve existing whitespace and comment trivia. Losing trivia silently reformats the user's code in unexpected ways.

### Single-Document Scope
Code fixes must not modify multiple documents in a single fix action. If the fix requires cross-file changes (e.g., adding a class in another file), split into separate fix actions.

---

## Checklists

### Add a code fix for a new diagnostic
1. Create `MyDiagnosticCodeFixProvider.cs` in `Nalix.Analyzers.CodeFixes/`
2. Inherit `CodeFixProvider`
3. Override `FixableDiagnosticIds` → return `ImmutableArray.Create("NAL0xxx")`
4. Implement `RegisterCodeFixesAsync` — call `context.RegisterCodeFix(...)` with a `CodeAction`
5. In the `CodeAction` delegate: use `SyntaxFactory` to transform the node, preserving trivia
6. Add `[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MyDiagnosticCodeFixProvider))]`
7. Add `[Shared]` attribute — required for MEF composition

### Debug a code fix not appearing
1. Verify `[ExportCodeFixProvider]` and `[Shared]` are both present
2. Verify `FixableDiagnosticIds` returns the exact diagnostic ID (case-sensitive)
3. Confirm the diagnostic is actually firing — no fix appears if there's no diagnostic
4. Rebuild solution — MEF composition is cached; stale cache can prevent new providers from loading

---

## Gotchas

- **Missing `[Shared]` = fix never discovered**: `[ExportCodeFixProvider]` alone is not enough. The MEF container also needs `[Shared]` (from `System.Composition`) to properly compose the provider. Forgetting `[Shared]` produces no error — the fix simply never appears.

- **Trivia loss reformats user code**: `SyntaxFactory.ParseStatement(...)` creates nodes with minimal trivia. If you replace a node without copying the original's leading/trailing trivia, the fix will reformat the user's code (e.g., collapse indentation) — this feels invasive and unexpected.

- **`RegisterCodeFix` equivalence key**: The `equivalenceKey` parameter in `CodeAction.Create(...)` is used to batch-apply the same fix. If two fixes have the same equivalence key, "fix all occurrences" will apply them together. Choose distinct keys for logically different fixes.

- **Code fix target framework is `netstandard2.0`**: The Roslyn Workspaces API requires `netstandard2.0`. Do not add APIs from `net10.0`-only assemblies here — they will fail at runtime in the IDE extension host.
