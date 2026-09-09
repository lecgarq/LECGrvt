# CRG milestone setup and qualification

Repository baseline: `774354b4d1d745b42ea2b73a73ad17cf68aebf58`.
CRG 2.3.8 was already installed in its own local Python environment. Reused it;
did not invoke `install`, add embeddings extras, set API keys, enable watchers,
or modify AGENTS.md. The generated graph is ignored by Git.

## Configuration

Updated the existing `mcp_servers.code-review-graph` section in
`C:\Users\LECG Arquitectura\.codex\config.toml`, retaining the existing executable.
Both CRG `--tools` and Codex `enabled_tools` restrict it to:

- get_impact_radius_tool
- query_graph_tool
- detect_changes_tool
- get_minimal_context_tool

Environment addition: `PYTHONUTF8=1`. A fresh stdio MCP handshake verified that
exactly these four tools are exposed. An already-connected session can retain
its old tool list until the connection is reloaded. No query routing row was added.
Codex's allowlist setting is documented in its [MCP configuration guide](https://learn.chatgpt.com/docs/extend/mcp?surface=cli).

`.code-review-graphignore` excludes `docs/review/**`, `**/bin/**`, `**/obj/**`.
Initial postprocessed status: 443 files, 3,071 nodes, 18,562 edges. These are
tree-sitter graph records, not a count of compiler-verified execution paths.

## Explicit milestone CLI

```powershell
& 'C:\Users\LECG Arquitectura\AppData\Roaming\uv\tools\code-review-graph\Scripts\python.exe' scripts/crg-close-milestone.py --base HEAD
```

The helper uses CRG's own `GraphStore`, `take_snapshot`, `diff_snapshots` and
snapshot persistence. It updates the graph, runs `detect-changes --brief`, and
prints the graph delta before atomically advancing its local milestone checkpoint.
It does not construct a second call-spine format or run during skill queries.

First checkpoint compares the pre-update graph to the post-update graph, not to
a fictional historical checkpoint. First run: zero nodes/edges added or removed.
Subsequent runs compare with `.code-review-graph/milestone.json`. The graph diff
and source-impact report use different baselines: graph = prior checkpoint;
impact = explicit `--base`. CRG estimates of token savings are not measured usage.
Its incremental update did not index untracked files in this run; zero delta
does not establish coverage of untracked work. Rebuild deliberately when needed.

### Risk-panel limitation also applies at milestones

Milestone-only does not make CRG's risk panel authoritative. The ambiguous and
overloaded target omissions observed in case 13 can also narrow a blast radius
derived from the same graph. Treat `detect_changes` risk scores, affected-caller
lists and test-gap suggestions as incomplete review hints, never proof of low
risk, complete coverage or safe absence of downstream effects. Confirm important
impact claims through exact-signature compiler references before acting on them.

This is a conservative consequence of the demonstrated edge-loss mechanism,
not a separately measured risk-scoring failure; no additional CRG test was run.
Node/edge diffs remain useful as changes in the recorded graph between explicit
checkpoints, not as a complete semantic change map. Additional test and UI files
can improve milestone review context without repairing incorrect caller edges.

## Executed qualification: live targets, not missing-symbol probes

The earlier ICadFamilyBuildService / ExecuteAsync checks never exercised the
intended qualification. They are superseded by the following live targets at
the same repository revision. Source under src/LECG.Core/LECG.Tests/RevitCopilot
had no working-tree diff during this comparison. Both targets belong to
LECG.csproj / net8.0-windows; defines TRACE;DEBUG;NET;NET8_0;NETCOREAPP.

### Case 12 — IProgressReporter

Serena resolves `LECG.Services.Interfaces.IProgressReporter` at
`src/Services/Infrastructure/IProgressReporter.cs:6`. Serena implementations and
CRG `inheritors_of` return exactly the same two classes:

- `LegacyProgressReporter`, `src/Services/Infrastructure/LegacyProgressReporter.cs:11`.
- `RevitCommandProgressReporter`, `src/Services/Infrastructure/RevitCommandProgressReporter.cs:11`.

Neither class is an obvious interface-name substitution. The observed selection
paths are manual construction, not a Bootstrapper registration for this interface.
Serena resolves 15 production command construction sites for RevitCommandProgressReporter
(plus two test sites); e.g. AssignMaterialCommand.cs:56. The legacy overload at
BatchRenameExecutionService.cs:32 constructs LegacyProgressReporter (plus three
test sites). No global default or container-selection inference is claimed.
Verdict: exact implementation-set match for this non-name-matching case only.

### Case 13 — ITransactionService.Run, non-generic overload

Target: `void Run(Document, string, Action<Document>)`, declaration
`src/Services/Infrastructure/ITransactionService.cs:8`.
Serena yields 35 invocation sites in 29 distinct caller declarations. CRG's
fully-qualified `callers_of` query returns 33 records, with no omitted results.
Identity is normalized repository-relative file + declaration start line;
repeated calls are deduplicated, but overloaded declarations remain distinct.

| Set comparison | Count |
|---|---:|
| Shared callers | 25 |
| Serena callers missing from CRG | 4 |
| CRG extras | 8 |

Missing callers: Execute in ResetSlabsCommand.cs:23, CleanSchemasCommand.cs:18,
OffsetElevationsCommand.cs:21 and UpdateContoursCommand.cs:20. These files are
indexed, so this is not file-coverage loss. For example, the ResetSlabs call is
stored as ambiguous `Run` with three possible target nodes, but omitted by the
targeted callers query.

Six extras are callers of the separate generic `Run<T>` overload. The other two
are unrelated name matches: SampleBenchmark.cs:158 calls SetterSweep.Run (also
confirmed by Serena references); SmokeApplication.cs:113 calls Task.Run.
CRG explicitly marks those two candidate targets unresolved; do not misreport
them as confidently resolved edges. They still make this returned caller set
unsafe as authoritative transaction-impact evidence.

Even generously comparing both transaction overloads together fails: Serena has
36 caller declarations; CRG shares 31, misses five (including a collapsed
CreatePBRMaterial overload), and adds the two unrelated candidates.

Verdict: **not a superset; fails promotion**, not merely usable over-breadth.
CRG remains milestone-only and outside EVIDENCE. This verdict concerns the tested
C# caller query, not a claim that every CRG graph relationship is wrong.

## Coverage reconciliation

The current inventory has **199** hashed inputs, not the historical 242.
The graph has 443 file nodes: 195 shared with the curated input set, 248 graph-only,
and four curated-only. Arithmetic: `443 = 195 + 248`; `199 = 195 + 4`.
The four curated-only inputs are two project files and the two knowledge JSON
packs—formats not represented as C# source file nodes.

| Additional graph files | Count |
|---|---:|
| src (mostly UI, models, core, utilities outside curated services/commands) | 114 |
| RevitCopilot (including 23 test files) | 76 |
| LECG.Tests | 37 |
| LECG.Core | 13 |
| tools | 5 |
| scripts | 3 |

All 443 graph files are tracked by Git. Zero paths under `.planning`, `docs/review`,
`bin`, `obj`, `outputs`, temporary/archive output directories; no `.g.cs`,
`.g.i.cs`, `.generated.*` or `.Designer.cs` filename candidates. This checks
paths/names, not whether any hand-maintained source originated in a generator.

Full sets, raw responses, per-file coverage differences and scope are recorded in
[crg-live-comparison.json](../../.codex/skills/lecg-map-codebase/evals/crg-live-comparison.json).
