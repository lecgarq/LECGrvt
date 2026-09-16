# Native MCP relationship-read preregistration

- Add exactly three reviewed campaign-v5 methods behind two native reads: `Room.IsPointInRoom` candidate `c0f89183090ce961dfd436a2`, `Space.IsPointInSpace` candidate `d4094fcbdb6666f262bea8aa`, and `ConnectorManager.Lookup` candidate `4ac2566755135916ee349a4b`.
- Register `spatial_contains_point` and `mep_connectors`; never load or execute generated candidate code.
- Denominator after registration: 39 native reads. Re-run all 39 through the production read dispatcher in four named CASA EUCALIPTO contexts.
- Isolation: create a fresh uniquely named detached disposable copy of each source model, unload links, close without saving, delete the copy, and verify the original source SHA-256 after execution.
- No transaction is permitted. Require unchanged element count and a non-modifiable document after every model.
- Retain only status, attempts, runtime target kind and a bounded failure reason. Do not retain returned model values.
- Each new operation is promotable only with at least one `read_succeeded` context. Other named contexts may remain explicit `missing_fixture`; no `read_failed` result is admissible.
- Evidence is tied to the named model and binary hashes. It does not establish universal applicability or guarantee that every compatible element returns a non-empty relationship.
