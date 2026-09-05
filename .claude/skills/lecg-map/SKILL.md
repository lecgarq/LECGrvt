---
name: lecg-map
description: Build or refresh the codebase map — the structure, edges, and blast radius index at .planning/codebase/MAP.md. Use when starting work on an unfamiliar area, when asked "what does X touch", "what breaks if I change Y", "map the codebase", before a risky refactor, or via /lecg-map. Refresh is incremental and cheap; a full rebuild is rare.
---

# Map

Grepping the repo every session is the single most expensive habit there is. Map once, store the **edges**, query many times. Refresh only what changed.

The map is `.planning/codebase/MAP.md`. Deep-dive docs beside it (`ARCHITECTURE.md`, `CONCERNS.md`, `TESTING.md`, …) are references — read them on demand, never wholesale.

## Mode: refresh (default)

Almost always what you want.

1. Read the `Generated at` commit SHA in `MAP.md`.
2. `git diff --name-only <sha>..HEAD` — the changed set.
3. If nothing changed, say so and stop. Do not rebuild.
4. Re-read only the changed files and anything `MAP.md` lists as depending on them.
5. Patch the affected rows. Update the SHA. Leave the rest untouched.

A refresh touches a handful of files. If you are reading dozens, you are rebuilding — say so and ask first.

## Mode: query

Answer from `MAP.md` alone when it can. Only open source files when the map is genuinely insufficient, and when you do, patch the map with what you learned.

**Blast radius** — "what breaks if I change X":
1. Direct callers of X.
2. Their callers (keep going until it stops growing).
3. Tests covering any of them.
4. For Revit surfaces: the ribbon buttons and views that reach X.

Report the file set, not a narrative.

## Mode: rebuild

Only when `MAP.md` is missing or a large refactor invalidated it. Say you are doing it and why.

Walk `src/`, `LECG.Core/`, `LECG.Tests/`. Record **relationships**, not descriptions — a description is re-derivable from the file, an edge is not:

- `Commands/*` → which `RevitCommand` / `ExternalEventCommand<T>` base, which services, which view
- `Services/*` → interface, implementation, registration site in `Bootstrapper`, callers
- `ViewModels/*` ↔ `Views/*` pairing, DI registration
- Anything writing to the document → the `ITransactionService` call site
- Tests → what they cover

## Format

Keep it terse. It is an index, not prose.

```markdown
# Codebase Map

Generated at: <full commit SHA>
Refreshed: <YYYY-MM-DD>

## Commands
| Command | Base | Services | View | Tests |
|---------|------|----------|------|-------|
| AlignEdges | RevitCommand | IGeometryService, ITransactionService | AlignEdgesView | AlignEdgesTests |

## Services
| Service | Interface | Registered | Used by | Writes doc |
|---------|-----------|------------|---------|------------|
| TransactionService | ITransactionService | Bootstrapper.ConfigureServices | 71 sites | yes |

## Hot spots
<files with many inbound edges — changing these is expensive>

## Untested
<code paths with no covering test>
```

## Rules

- Edges over prose. If a sentence describes what a file obviously does, delete it.
- Never let the map assert what the code contradicts. Code wins; fix the map in the same turn.
- Record the SHA on every write, or the next refresh cannot diff.
- Do not duplicate `docs/ai/repo-context.md`. That holds conventions and gotchas; this holds structure and edges.
