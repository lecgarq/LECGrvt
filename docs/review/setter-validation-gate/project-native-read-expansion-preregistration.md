# Native MCP read expansion preregistration

- Add exactly two reviewed campaign-v5 read methods: `LocationCurve.get_ElementsAtJoin` candidate `e86aa1a1f66f14a32497fead` and `MEPModel.GetAssignedElectricalSystems` candidate `041f324808ec9567d9d3da5b`.
- Register constrained native operations `curve_join_neighbors` and `assigned_electrical_systems`; never load or execute generated candidate code.
- Denominator after registration: 37 native reads. Re-run all 37 through the production read dispatcher in four named CASA EUCALIPTO contexts.
- Isolation: copy each source model into a unique run directory, detach/discard worksets, unload Revit/CAD links, close without saving, delete the copy, then verify the source SHA-256.
- No transaction is permitted. Require unchanged element count and a non-modifiable document after every model.
- Retain only status, attempts, runtime target kind and a bounded failure reason. Do not retain returned model values.
- A new operation is promotable only if it has at least one `read_succeeded` context. Other named contexts may remain explicit `missing_fixture` results.
- Evidence is limited to the named model and binary hashes; it is not universal applicability or a guarantee that every compatible element has a non-empty result.
