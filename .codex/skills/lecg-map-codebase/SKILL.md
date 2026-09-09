---
name: lecg-map-codebase
description: Answer questions about the LECG Revit repository using live, bounded evidence. Use for code locations, capabilities, callers, implementations, impact, validation and recipes. Full setter lists use an explicit report export; never builds an index or changes Revit.
---

# LECG codebase query

Route to the cheapest authoritative source, not the nearest prose answer.
Queries are read-only. Full enumeration is a report, not a larger query packet;
use the explicit export below. Do not build indexes, maps, graphs, embeddings or
documentation snapshots. No map mode.

## First call and budget

From the repository root, the first task tool call is always:

```powershell
node .codex/skills/lecg-map-codebase/scripts/freshness.mjs
```

Run even when the question seems familiar. Report index revision vs HEAD,
recorded and live input checks, rendered-doc revision/distance and Serena state.
The same call evaluates project TFMs and defines using MSBuild property queries
(no build); reuse those live facts instead of spending an orientation-only call.
STALE inventory is inadmissible evidence, not a failed workflow. Continue with
live authoritative sources. Surface the refresh command but never run it in query mode.

Session setup, once: load Serena `initial_instructions {}` and load the pure
`scripts/budget.mjs` module into the emitter runtime. These setup operations are
not repeated or charged to each query; never disguise repository evidence as setup.
Load [observed response sizes](references/response-sizes.md) once for request sizing,
not repository claims. Use the matching measured shape before guessing a limit.

The live Serena connection/workspace/backend probe is inside freshness.mjs.
`reachable` means the matching C# LSP endpoint responds, **not** that a symbol has
resolved. The first evidence-bearing Serena lookup confirms resolution/readiness;
promote to healthy only after success. There is no separate `get_current_config`
call in the query path. Failed/partial resolution stays UNRESOLVED. Never activate,
onboard, install, restart a server, or save memories inside a query.

Track calls and cumulative UTF-8 bytes as evidence arrives, including intermediate
output that will not be quoted. One shell command, MCP invocation, or source-file
read is one call. Wrapping several calls in one orchestration call does not hide
them; internal reads by the bounded scripts are included in that shell invocation.
Skill instruction loading is setup, not repository evidence; do not use it to
smuggle repository reads outside the ledger. Subsequent reference reads count.

- Target seven calls; hard stop at eight. Call 1 is freshness, calls 2–7 gather
  evidence, call 8 is contingency. A run reaching 8/8 is a ceiling hit, not a
  passing efficiency result, even if it fits the byte limit.
- Hard ceiling: 6,144 bytes evidence. Orient in 2–4 lines. Prefer approximately
  1 KB orientation, 3 KB task evidence, 2 KB symbol bodies, leaving room for headers.
- Set explicit `max_answer_chars`/`max_matches` using observed response sizes.
  Reserve remaining bytes for the measured response plus margin before requesting
  a body; if it will not fit, reduce scope or abstain. Character limits are not byte
  limits. A shortened response and its retry both count; do not repeat an undersized
  first request when a measured size exists. Unknown sizes are estimates, not guarantees.
- On either ceiling, emit the packet with the remaining question and the single
  next query in UNRESOLVED. Do not make another call to obtain a cleaner answer.
- The emitter calls `emitPacket(sections, {calls, evidenceBytes})` from budget.mjs
  in its existing runtime after the last evidence result. It assembles COST and
  validates structure, packet bytes and ledger limits without I/O, subprocesses,
  or another tool invocation. Do not run a separate validator command per query.
  Sum raw evidence bytes as each result arrives; the checker cannot reconstruct
  omitted tool outputs. Load the module once during setup, not on the final call.

## Evidence rules

1. Never read `knowledge-index.json` whole or use arbitrary JSON extraction.
   Use only `query-index.mjs` subcommands below. There are no path/raw/expression flags.
2. Never open `docs/review/archify/lecg-knowledge.html` (approximately 820 KB).
   Hand its path to a human wanting the picture; it is not agent evidence.
3. Serena is compiler evidence only after live health **and** symbol resolution.
   Partial, stale, missing or ambiguous resolution goes to UNRESOLVED. Never replace
   a failed lookup with a filename guess or grep-derived call chain.
4. Establish containing project, active TFM and conditional defines before trusting
   semantic results. Use evaluated MSBuild properties without a build when needed:
   `dotnet msbuild <known.csproj> -getProperty:TargetFramework,DefineConstants -p:SkipRevitDeploy=true -nologo`.
   Shared linked files can belong to multiple projects. A file path alone does not
   establish which compilation Serena resolved; keep that uncertainty explicit.
5. If Serena cannot be verified, declare degraded mode. Admit only fresh inventory
   and structural evidence; put capability, implementation and call-chain questions
   in UNRESOLVED rather than silently substituting text searches.
6. Capability starts with `CapabilityCatalog.cs` / `RevitApiCatalog.cs` via Serena,
   cross-checked against `operation` / `mcp-tool`. README statements never establish
   capability. A registration description establishes registration, not every runtime guard.
7. Source presence is not capability. Distinguish service existence (inventory),
   agent exposure (registered-operation), and selected implementation (compiler + DI).
8. Call chains come only from Roslyn-backed Serena resolution. No graph edges,
   filename adjacency, method-name matches or grep-only chains.
9. Resolve interfaces through Serena and `src/Core/Bootstrapper.cs`; do not choose
   a concrete class because its name resembles the interface. A removed interface
   is not a license to silently substitute another symbol.
   Semantic completeness (all implementers, including indirect inheritance) goes
   to the compiler's `find_implementations`. Syntactic shape goes to ast-grep.
10. Query mode writes nothing, including caches. No inventory refresh, builds,
    test execution, deployment, Revit model calls, or graph installation during a query.
11. Prose is rationale/history only. An unresolved source claim stays unresolved,
    even when documentation offers a confident answer.

Use the six tiers in [references/evidence-tiers.md](references/evidence-tiers.md).
Recipe records remain inventory; recorded validation is not a live test result.

## Routing

Let `Q` below mean `node .codex/skills/lecg-map-codebase/scripts/query-index.mjs`.
Names in angle brackets are literal task inputs, quoted as single arguments, not expressions.

| Question | Exact next route after freshness/necessary health check |
|---|---|
| Where is X / domain D | `Q service '<domain>'`; use `Q command '<name>'` for a known command |
| Can the agent do X | Serena `find_symbol` in `RevitCopilot/Agent/CapabilityCatalog.cs` or `RevitApiCatalog.cs`; then `Q operation '<name>'` and/or `Q mcp-tool '<name>'`. For the large All field, resolve it without its body, then use a bounded Serena `search_for_pattern` for the single entry within that resolved field. |
| Operation arguments | `Q operation '<name>'`, then Serena catalog symbol |
| Who calls X / what breaks | Serena `find_referencing_symbols {name_path:"<resolved name>",relative_path:"<known file>",max_answer_chars:1800}` |
| Which implementation runs | Serena `find_symbol {name_path_pattern:"<interface>",include_info:true,include_body:false,max_matches:2,max_answer_chars:1400}`, then scoped Bootstrapper registration lookup |
| Every class implementing interface I | Resolve the exact interface through Serena, then `find_implementations` with its returned name path/file and a bounded result limit. Include indirect implementations; if metadata resolution or completeness is unavailable, emit UNRESOLVED. Never substitute direct base-list matches. |
| Every place shaped like P | `ast-grep run --lang csharp --pattern '<P>' --files-with-matches src` (structural, not semantic completeness) |
| Recipe | `Q recipes '<term>'` or `Q recipes` |
| Validation summary | `Q validation '<state>'` or `Q validation unvalidated-setters`; totals and bounded examples only |
| Full unvalidated-setter list | Explicit report action below; packet cites its returned path and count, not all identities |
| Rationale | `rg -n -m 3 '<literal term>' .planning docs/review -g '*.md'` bounded to remaining evidence allowance; tier doc-assertion |
| None applies | One targeted source excerpt, maximum 80 lines and remaining byte allowance; tag only what it establishes |

Also allow `Q freshness` and `Q counts`. `service`, `command`, `operation` and
`mcp-tool` match exact case-insensitive names. Recipes use a literal substring.
`validation` accepts a recorded state or the fixed `unvalidated-setters` group:
`api.set:` entries whose state is not `changed_value_tested`. It returns a total
and bounded individual records from the existing validation ledger, not new tests.
Every subcommand caps the entire serialized response at 25 entries / 4,096 bytes,
with a truncation notice. Empty results are not proof that a broader capability is absent.

For project-specific caveats read [references/repo-shape.md](references/repo-shape.md).
For worked routes read [references/routing.md](references/routing.md) only when needed.

## Full-list report boundary

For an explicit full-list/report request, run freshness first, then:

`node .codex/skills/lecg-map-codebase/scripts/export-report.mjs unvalidated-setters`

This separate action writes only a new, uniquely named JSON report under
`docs/review/`, never an index or cache. It reuses the validation selector, refuses
stale inputs and never overwrites an existing report. Cite its receipt's absolute
path, count, revision and checksum as inventory evidence. Keep freshness, the
receipt and the packet inside the normal budget; the full file stays outside
context. Do not page or open it to squeeze enumeration into a query. Ordinary
queries never auto-export; if report intent is unclear, ask before writing.

## Output — one packet, no prose outside it

```text
FRESHNESS
  index rev <sha> | HEAD <sha> | inputs <match/stale/unknown>
  rendered docs pinned <sha> | <distance or unknown> | Serena <state + reason>
ORIENTATION
  <2–4 lines: domain, project, TFM, role>
EVIDENCE
  [<tier>] <claim> — <supporting source/symbol>
SYMBOLS
  <fully qualified name> — <file:1-based-line> | <project + TFM>
READ NEXT
  <at most five ranked files, reason each>
UNRESOLVED
  <what is not established + one exact next query; or none>
COST
  <n>/8 tool calls | ~<n> KB evidence
```

Serena locations are zero-based; add one for displayed lines. Do not fabricate
fully qualified names or line numbers for unresolved symbols.
Never open `lecg-knowledge.html` (approximately 820 KB) to fill an evidence gap.
