# Technical Documentation

## Role

Guidelines, quality standards, and formatting rules for creating, modifying, and maintaining
the technical documentation of the Nalix ecosystem (including API references, guides, concepts,
and packaging information) to ensure a warning-free `mkdocs build`.

**Dependencies:** `docs/`, `mkdocs.yml`

---

## Directory Structure

```text
docs/
├── api/                 # Reference documentation for C# namespaces & APIs
│   ├── connection/      # Connection lifecycle and hubs
│   ├── network/         # TCP/UDP/WebSocket listeners & transports
│   ├── options/         # Client & Server configuration options
│   └── security/        # Cryptography, handshake, and access control
├── concepts/            # Architectural overviews, performance & security models
├── guides/              # Getting started, blueprints, deployment, and tools
├── packages/            # Package descriptions and API entry-point references
└── mkdocs.yml           # Global navigation & MkDocs Material configuration
```

---

## Key Documentation Rules

### Core Rules (R1–R10) — Mandatory for Code & Structure Integrity

- **R1 — Direct Source Reference Validation:** Every class, interface, struct, method, property,
  and enum **MUST** be verified directly against its actual C# source file. Never assume or guess
  signatures.
- **R2 — Accurate Signature Matching:** All C# code signatures (parameters, types, generic
  constraints, return types) must exactly match the implementation code.
- **R3 — MkDocs Material Admonitions:** Use proper admonition syntax for callouts:
  `!!! note`, `!!! warning`, `!!! tip`, `!!! danger`.
- **R4 — Explicit Spacing before Lists:** Always leave a blank line immediately before any
  bulleted (`-`) or numbered (`1.`) list. Without it, MkDocs collapses list items into a single
  inline paragraph.
- **R5 — Correct Relative Cross-Link Paths:** Use relative paths for internal links, e.g.
  `[Registry](../network/connection-registry.md)`. Never use absolute paths or external URLs
  for internal pages.
- **R6 — Clean Link Formatting:** Do not wrap link text in backticks inside brackets.
  Use `[IConnection](connection.md)`, not `` [`IConnection`](connection.md) ``.
- **R7 — Code Block Language Specifiers:** Every fenced code block must declare its language,
  e.g. ` ```csharp `, ` ```yaml `, ` ```json `.
- **R8 — Sync `mkdocs.yml` Navigation:** When a file is added, moved, or deleted, update the
  `nav` tree in `mkdocs.yml` immediately.
- **R9 — Package & Namespace Context:** Declare the full C# namespace (e.g.
  `Nalix.Network.Connections`) and its assembly/package (e.g. `Nalix.Network`) at the top of
  every API document.
- **R10 — Standard Page Hierarchy:** Every page must follow: Main Title (`# ClassName`) →
  Brief Overview → Architecture/Source mapping → Configuration Options →
  Code Snippet/Usage Example → API Reference.

### Additional Rules (R11–R20) — Best Practices for Documentation Quality

- **R11 — Real-world Usage Examples:** Every major class must include at least one complete,
  copy-pasteable, realistic C# usage example.
- **R12 — Lifecycle & Disposal Details:** Explain initialization and disposal lifecycle,
  including `IDisposable` / `IAsyncDisposable` behavior.
- **R13 — Document Exception Behavior:** Specify what exceptions are thrown, under what
  conditions, and how callers should handle them.
- **R14 — Thread Safety Warnings:** Explicitly state whether the class or method is thread-safe,
  thread-local, or requires external synchronization.
- **R15 — Memory & Allocation Guidance:** Document performance characteristics and memory
  footprint (e.g. object pooling via `IPooledConnectContextPool` to reduce GC pressure).
- **R16 — Deprecation & Migration Notices:** Mark deprecated APIs with a
  `!!! danger "Deprecated"` admonition explaining the reason and the recommended replacement.
- **R17 — Configuration Mapping:** Link components to their corresponding Options documentation
  page (e.g. link a listener to `ConnectionQuotaOptions`).
- **R18 — Professional Technical Tone:** Maintain a formal, precise, and objective tone.
  Write strictly in **English**.
- **R19 — Visual Diagrams:** Use Mermaid.js diagrams for complex protocol flows, packet
  sequences, or architecture boundaries.
- **R20 — Keep Unaffected Content Intact:** When editing existing files, do not delete or omit
  unrelated sections, tables, or notes.

---

## Anti-Patterns

- **Do NOT Guess Signatures:** Never document APIs without validating actual code in `src/`.
- **Do NOT Collapse Lists:** Never start a list immediately below a paragraph without a blank line.
- **Do NOT Use Absolute Links:** Never use absolute paths (e.g. `http://.../api/...`) for
  internal pages.
- **Do NOT Skip mkdocs.yml Sync:** Never add or remove files without reflecting the change
  in the `mkdocs.yml` `nav` structure.
- **Do NOT Use Backticked Link Anchors:** Never place backticks inside link brackets —
  `` [`IConnection`](connection.md) `` breaks the renderer.

---

## Reusable Prompt Template

````markdown
# Prompt: Update Documentation for Nalix Project

## Task

Update or create the documentation at `{{DOC_PATH}}` based on the following C# files:
{{SOURCE_FILES}}

## Rules

1. Verify namespace, class names, method signatures, properties, and comments against the
   C# source. Do not invent features or assume signatures.
2. Format C# code blocks using ` ```csharp `.
3. Use MkDocs Material styling: admonitions (`!!! note`, `!!! warning`), correct header
   hierarchy, and relative cross-links.
4. Always leave a blank line before any list block (ordered or unordered).
5. Do not wrap link text in backticks inside brackets — use `[Name](path)`.
6. Declare the full C# namespace and package name at the top of the file.
7. Provide at least one realistic, non-trivial usage snippet for each main component.
8. Specify thread-safety behavior, exception conditions, and memory optimization details.
9. If modifying an existing file, do not delete unaffected sections.
````

---

## Documentation Audit Workflow

When auditing existing documentation against source code:

- Scan all Markdown files under `docs/**`.
- Process exactly one Markdown file per batch.
- Build a deterministic alphabetical tree of docs files.
- Create and maintain `.docs-audit-progress.md`.
- Before editing any docs file, check `.docs-audit-progress.md` — skip files already marked
  `Checked` or `Updated`.
- For each docs file, use `rg` to locate all referenced source symbols in `src/`.
- Source scan ignores: `bin/`, `obj/`, `Generated/`, `*.g.cs`.
- Verify direct source references first, then related source files.
- Only modify documentation files — never modify source code.
- If unsure, record the issue under `Needs Review`.
- Run `mkdocs build` after meaningful documentation changes or large batches.

---

## Audit Tracking File

Use `.docs-audit-progress.md` with this format:

| Docs File | Source Files Checked | Status | Notes | Validation |
|-----------|----------------------|--------|-------|------------|

Allowed statuses:

- `Pending`
- `Checked`
- `Updated`
- `Needs Review`

If `.docs-audit-progress.md` does not exist:

1. Build a deterministic alphabetical tree of all `docs/**/*.md`.
2. Create `.docs-audit-progress.md` .
3. Insert every documentation file with initial status `Pending`.
4. Start processing from the first pending file.

---

## Symbol Resolution Rules

When multiple source matches are found, use this priority order:

1. Exact namespace + type match
2. Same assembly/package
3. Same feature directory
4. Referenced type usage
5. Global search fallback

Never assume the first `rg` result is correct.

---

## Generated Documentation Protection

Do not rewrite:

- Changelog pages
- Release notes
- Generated API indexes
- Benchmark output files
- Telemetry snapshots

Only update factual references if they are clearly incorrect.

---

## Example Validation

When updating code examples, verify:

- Namespaces
- Type names
- Option names
- Method signatures
- Removal of obsolete APIs (do not invent new ones)

Examples must compile logically against current source.
