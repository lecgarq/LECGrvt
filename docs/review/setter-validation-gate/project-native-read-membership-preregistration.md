# Native MCP membership-read preregistration

- Add exactly four reviewed Revit 2026 relationship reads: `Group.GetMemberIds`, `FamilyInstance.GetSubComponentIds`, `AssemblyInstance.GetMemberIds`, and `MEPSystem.Elements`.
- Register `group_members`, `family_subcomponents`, `assembly_members`, and `mep_system_members`; do not change the 3,000-reference denominator because these exact members are not present in the harvested reference pack.
- Denominator after registration: 49 native reads. Re-run all 49 through the production read dispatcher in four named CASA EUCALIPTO contexts (196 total contexts).
- Isolation: create a fresh uniquely named detached disposable copy of each source model, unload links, close without saving, delete the copy, and verify the original source SHA-256 after execution.
- No transaction is permitted. Require unchanged element count and a non-modifiable document after every model.
- Retain only status, attempts, runtime target kind and a bounded failure reason. Do not retain returned member identities, names or model values.
- Each new operation is promotable only with at least one `read_succeeded` context. Other named contexts may remain explicit `missing_fixture`; no `read_failed` result is admissible.
- Evidence is tied to the named model and binary hashes. It does not establish universal applicability or guarantee that every compatible element returns a non-empty relationship.
