# Technical Documentation

## Role

Guidelines, quality standards, and formatting rules for creating, modifying, and maintaining the technical documentation of the Nalix ecosystem (including API references, guides, concepts, and packaging information) to ensure a warning-free `mkdocs build`.

**Dependencies:** `docs/`, `mkdocs.yml`

## Directory Structure

```
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

## Key Documentation Rules

### Core Rules (R1 - R10) — Mandatory for Code & Structure Integrity

*   **Rule 1: Direct Source Reference Validation:** Every class, interface, struct, method, property, and enum described in the documentation **MUST** be verified directly against its actual C# source file. Never assume or guess signatures.
*   **Rule 2: Accurate Signature Matching:** All C# code signatures (including parameters, types, generic constraints, and return types) must exactly match the implementation code.
*   **Rule 3: MkDocs Material Admonitions:** Highlight important callouts, tips, and warnings using the proper MkDocs admonition syntax (e.g., `!!! note`, `!!! warning`, `!!! tip`, `!!! danger`).
*   **Rule 4: Explicit Spacing before Lists:** Always leave a blank line immediately before any bulleted (`*`, `-`) or numbered (`1.`) list. Without this blank line, MkDocs will collapse list items into a single inline paragraph.
*   **Rule 5: Correct Relative Cross-Link Paths:** Use relative paths (e.g., `[Registry](../network/connection-registry.md)`) for internal linking. Never use absolute paths or external URLs for internal pages.
*   **Rule 6: Clean Link Formatting:** Do not wrap link texts in backticks inside the brackets. Use `[IConnection](connection.md)` instead of `[`IConnection`](connection.md)` to ensure correct theme rendering.
*   **Rule 7: Code Block Language Specifiers:** Every code block must specify its language (e.g., ` ```csharp `, ` ```yaml `, ` ```json `) to enable proper syntax highlighting.
*   **Rule 8: Sync `mkdocs.yml` Navigation:** Whenever a documentation file is added, moved, or deleted, you must immediately update the `nav` tree in `mkdocs.yml` to maintain alignment.
*   **Rule 9: Package & Namespace Context:** Always declare the full C# namespace (e.g., `Nalix.Network.Connections`) and its containing assembly/package (e.g., `Nalix.Network`) at the top of the API document.
*   **Rule 10: Standard Page Hierarchy:** Every page must follow a clean structure: Main Title (`# ClassName`), Brief Overview, Architecture/Source mapping, Configuration Options, Code Snippet/Usage Example, and API Reference.

### Additional Rules (R11 - R20) — Best Practices for Documentation Quality

*   **Rule 11: Real-world Usage Examples:** Avoid trivial examples. Every major class must include at least one complete, copy-pasteable, and realistic usage example in C#.
*   **Rule 12: Lifecycle & Disposal Details:** Explain the initialization and disposal lifecycle of the component (e.g., if it implements `IDisposable` or `IAsyncDisposable`, and how it should be cleaned up).
*   **Rule 13: Document Exception Behavior:** Clearly specify what exceptions are thrown by methods, under what conditions, and how callers should handle them.
*   **Rule 14: Thread Safety Warnings:** Explicitly state whether the class or method is thread-safe, thread-local, or requires external synchronization.
*   **Rule 15: Memory & Allocation Guidance:** Document performance characteristics and memory footprints (e.g., whether the component uses object pooling like `IPooledConnectContextPool` to reduce GC overhead).
*   **Rule 16: Deprecation & Migration Notices:** If an API is deprecated, mark it clearly using a `!!! danger "Deprecated"` admonition, explaining the reason and what to use instead.
*   **Rule 17: Configuration Mapping:** When a component is configured via options, link directly to its corresponding Options documentation page (e.g., linking a listener to `ConnectionQuotaOptions`).
*   **Rule 18: Professional Technical Tone:** Maintain a formal, precise, and objective technical tone, and write strictly in **English**.
*   **Rule 19: Visual Diagrams:** For complex protocol flows, packet sequences, or architecture boundaries, use Mermaid.js diagrams to explain relationships visually.
*   **Rule 20: Keep Unaffected Content Intact:** When editing existing files, do not delete or omit unrelated sections, tables, or notes.

## Anti-Patterns

*   **Do NOT Guess Signatures:** Never document APIs or classes without validating their actual code in `src/`.
*   **Do NOT Collapse Lists:** Never start a list immediately below a paragraph without a blank line space.
*   **Do NOT Use Absolute Links:** Never link to internal pages using absolute paths (e.g., `http://.../api/...`) or hardcoded repository paths.
*   **Do NOT Skip mkdocs.yml Sync:** Never add or remove files without reflecting the exact change in the `mkdocs.yml` `nav` structure.
*   **Do NOT Use Backticked Link Anchors:** Never use backticks inside relative markdown link brackets, e.g. `[`IConnection`](connection.md)` which breaks the renderer.

---

## Reusable Prompt Template

```markdown
# Prompt: Update Documentation for Nalix Project

## Task
Update or create the documentation at `{{DOC_PATH}}` based on the following C# files:
{{SOURCE_FILES}}

## Rules
1. Verify namespace, class names, method signatures, properties, and comments against the C# source code. Do not invent features or assume signatures.
2. Format C# code blocks using ```csharp.
3. Use MkDocs Material styling (use admonitions like `!!! note`, `!!! warning`, correct header hierarchy, and relative cross-links).
4. Ensure there is a blank line before any list block (ordered or unordered) to prevent list rendering bugs where items collapse inline.
5. Do not wrap link texts in backticks inside brackets (e.g. use `[Name](path)` instead of `[`Name`](path)`).
6. Always declare the full C# namespace and package name at the top of the file.
7. Provide at least one realistic, non-trivial usage snippet for each main component.
8. Specify thread-safety behavior, exception conditions, and memory optimization details (e.g., pooling).
9. If modifying an existing file, do not delete unaffected sections.
```
