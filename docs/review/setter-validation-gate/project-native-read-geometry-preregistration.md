# Native MCP geometry-read preregistration

- Add exactly two reviewed campaign-v5 read methods: `BasePoint.GetProjectBasePoint` candidate `4d2491374a2d57c261855471` and `FaceSplitter.GetBoundaries` candidate `3c567e7f95e186ae02c6c02a`.
- Register `base_points` and `face_split_boundaries`; never load or execute generated candidate code.
- Denominator after registration: 45 native reads. Re-run all 45 through the production read dispatcher in four named CASA EUCALIPTO contexts.
- Isolation: create a fresh uniquely named detached disposable copy of each source model, unload links, close without saving, delete the copy, and verify the original source SHA-256 after execution.
- No transaction is permitted. Require unchanged element count and a non-modifiable document after every model.
- Retain only status, attempts, runtime target kind and a bounded failure reason. Do not retain returned coordinates, geometry or model values.
- Each new operation is promotable only with at least one `read_succeeded` context. Other named contexts may remain explicit `missing_fixture`; no `read_failed` result is admissible.
- Evidence is tied to the named model and binary hashes. It does not establish universal applicability or guarantee that every compatible element returns a non-empty relationship.
