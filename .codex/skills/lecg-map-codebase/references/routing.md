# Worked query routes

Each example starts with freshness.mjs, including its live Serena connection probe.
Initial instructions are once-per-session setup. Packet validation runs inside
the emitter and spends no query call. Stop at eight calls or 6 KB; reaching eight
is a ceiling hit, not an efficiency pass.

## Family-document rename

Classify as capability. Establish Serena health and the catalog compilation;
resolve CapabilityCatalog, then its rename registration. Cross-check with
`query-index.mjs operation rename_elements`. Do not read a README for the answer.
Current source separates registration from document eligibility: the restriction
is in ToolExecutor.Changes.cs. With the user's approved tier correction, resolve
ToolExecutor/ValidateChangeDocument and its references through Serena. Report the
registration as registered-operation and the family-document rejection as
source-implementation. If either resolution is missing, state that gap in
UNRESOLVED. Never retag a guard as registration evidence.

## Material services

`query-index.mjs service Materials` provides paths, not executable capabilities.
If inventory is stale, put the requested authoritative inventory answer in
UNRESOLVED; stale paths may guide READ NEXT only. Do not automatically regenerate
the index or crawl all source files to hide the staleness.

## Setters lacking changed-value validation

`query-index.mjs validation unvalidated-setters` uses a fixed predicate over the
existing ledger: operation starts `api.set:` and state differs from
`changed_value_tested`. At revision 774354b4 the ledger has 187 such records:
82 missing_fixture, 70 same_value_only, 35 context_rejected. These are baseline
observations, never a substitute for live freshness. The returned total survives
entry truncation; do not count just the displayed entries. A request for the full
list goes directly to `export-report.mjs unvalidated-setters` after freshness,
not through repeated inventory queries. Return its path and count in the packet.

## Callers / implementation

Resolve the actual requested declaration first. Then use
`find_referencing_symbols {name_path:"<resolved path>",relative_path:"<file>",max_answer_chars:1800}`.
Use references plus resolved method bodies to establish calls; a reference need
not itself be an invocation. For DI, inspect Bootstrapper registrations through
Serena. If ICadFamilyBuildService is absent, report absence of resolution; do not
substitute CadFamilyBuildService by name.

## Semantic implementation completeness

Resolve the exact interface, then call Serena `find_implementations` with its
returned declaration path. Direct syntactic base lists cannot answer "every class
implementing IExternalCommand" because indirect inheritance and aliases count.
An external metadata declaration rejected as outside the workspace is a provider
blocker, not an excuse to copy that interface into source or substitute a base class.

## Syntactic shape

Use ast-grep for an actual shape question such as every call matching
`$OBJ.Set($VALUE)`. Report structural matches, not resolved overloads or call chains.
