# Evidence tiers

| Tier | Establishes | Does not establish |
|---|---|---|
| compiler | Live Roslyn-resolved symbol identity, references, implementation relationships in an established compilation | Runtime success, permissions, or another project's compilation |
| registered-operation | An operation/tool actually registered in the resolved catalog; its explicit declared contract | Context restrictions absent from that catalog; runtime success |
| source-implementation | Behavior explicitly implemented in a resolved body, such as an execution guard | Agent exposure without registration; behavior of an unresolved implementation |
| inventory | Current indexed presence/counts, recipe records, recorded validation states | Runtime behavior, call chains, universal validation |
| structural | A syntactic pattern in source | Semantic interface closure, DI selection, call chains |
| doc-assertion | What documentation says; recorded rationale/history | Capability or behavior by itself |

An inventory mismatch disqualifies inventory evidence; it does not erase live
compiler or catalog evidence. Label stale cross-checks explicitly rather than
promoting them. Empty search results and truncated outputs never prove absence.

Serena must report the expected workspace, ready LSP backend, and a successfully
resolved symbol. Establish the actual C# project, TFM and defines. Linked files
need explicit compilation disambiguation; workspace identity alone is insufficient.
In degraded mode, implementation and capability answers remain unresolved.

A catalog entry and an executor guard are separate claims with separate tiers.
At the inspected revision, `rename_elements` is registered in CapabilityCatalog,
but the family-document rejection is implemented in ToolExecutor.Changes.cs.
Do not attribute that rejection to a catalog that does not contain it. The user
approved citing the actual guard as source-implementation while the catalog
establishes registration. Do not move production guards just to pass an eval.
