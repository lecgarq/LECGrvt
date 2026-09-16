# Native MCP association-read preregistration

- Add exactly two reviewed campaign-v5 read methods: `Stairs.GetAssociatedRailings` candidate `29802d8c0186b950dec8c775` and `Group.GetAvailableAttachedDetailGroupTypeIds` candidate `ffdc7e1ebf434480ba699ae0`.
- Register `stairs_associated_railings` and `group_attached_detail_types`; never load or execute generated candidate code.
- Denominator after registration: 43 native reads. Re-run all 43 through the production read dispatcher in four named CASA EUCALIPTO contexts.
- Isolation: create a fresh uniquely named detached disposable copy of each source model, unload links, close without saving, delete the copy, and verify the original source SHA-256 after execution.
- No transaction is permitted. Require unchanged element count and a non-modifiable document after every model.
- Retain only status, attempts, runtime target kind and a bounded failure reason. Do not retain returned model values.
- Each new operation is promotable only with at least one `read_succeeded` context. Other named contexts may remain explicit `missing_fixture`; no `read_failed` result is admissible.
- Evidence is tied to the named model and binary hashes. It does not establish universal applicability or guarantee that every compatible element returns a non-empty relationship.
