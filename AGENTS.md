# Nalix — Agent Instructions

Apply all rules in this file and `./CLAUDE.md`.

## Structured Logging Rules

All `_logger.Log*` / `s_logger.Log*` calls must follow these rules:

1. **No `$""` in any Log call** — Use message templates with placeholders.
2. **Exception is always the first parameter** — Pass exception properties as structured template properties.
3. **Method calls must be extracted before logging** — Use local variables.
4. **Ternary must be extracted before logging** — Use local variables.
5. **Format specifiers must be extracted** — Compute values before logging.
6. **No multi-line concat in log templates** — Merge into a single template.
7. **No `nameof()` in log templates** — Use hardcoded bracket prefixes (e.g. `"[NW.Connection:Disconnect]"`).

These rules also apply to `ThrottledError` calls.