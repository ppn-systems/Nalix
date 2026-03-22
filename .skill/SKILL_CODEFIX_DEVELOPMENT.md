# 🛠️ Nalix AI Skill — CodeFix Provider Development

This skill covers the development of Roslyn `CodeFixProvider`s for the Nalix framework, enabling the IDE to automatically correct common coding errors.

---

## 🏗️ CodeFix Architecture

Each CodeFix is typically a specialized provider targeting one or more `NALIX-XXX` diagnostics.

- **`FixableDiagnosticIds`**: The set of IDs this provider can resolve.
- **`RegisterCodeFixesAsync`**: The entry point where the provider offers a fix to the user.
- **`GetFixAllProvider`**: Usually returns `WellKnownFixAllProviders.BatchFixer`.

---

## 📜 Common Fix Patterns

### 1. Attribute Addition/Modification
Used for adding missing `[PacketOpcode]` or fixing incorrect `[SerializeOrder]`.
- **Logic:** Find the node (Method/Property), create a new attribute syntax, and use `DocumentEditor` to replace the node.

### 2. Interface Implementation
Used for adding `ResetForPool()` when `IPoolable` is detected (NALIX037).
- **Logic:** Add the missing method member to the class definition.

### 3. Type Correction
Used for fixing `PacketBase<T>` where `T` is not the class itself.
- **Logic:** Find the base type syntax and replace the generic argument with the class name.

---

## 🛠️ Implementation Best Practices

- **Minimal Perturbation:** Only change the specific lines required for the fix. Preserve whitespace and comments using `SyntaxTrivia`.
- **Semantic Verification:** Ensure the fix doesn't introduce new compile errors. Use the `SemanticModel` to verify types before applying changes.
- **Batching:** Ensure your fixer works correctly with "Fix All in Solution".

---

## 🧪 Testing CodeFixes

- **Location:** `tests/Nalix.Analyzers.Tests`.
- **Helper:** Use `VerifyCS.VerifyCodeFixAsync(source, fixedSource)`.
- **Strategy:** Provide a "Before" and "After" snippet. The test will run the analyzer, apply the fix, and compare the result.

---

## 🛡️ Common Pitfalls

- **Incorrect Scoping:** Ensure the fix is only offered in contexts where it makes sense (e.g., inside a `[PacketController]`).
- **Syntax Crashing:** Accessing `parent.parent` without null checks can crash the IDE. Always verify the syntax tree structure.
- **Missing Usings:** Adding a new attribute might require adding a `using Nalix.Framework;`. Use the `ImportAdder` or manually add the using directive.
