# Native MCP coordination-read preregistration

- Add exactly two reviewed campaign-v5 read methods: `ExternalFileUtils.GetAllExternalFileReferences` candidate `7082414fa9bd90b3cfb3c9ad` and `Panel.FindHostPanel` candidate `49ff3f43469219d7121ba283`.
- Register `external_files_list` and `panel_host`; never load or execute generated candidate code.
- Denominator after registration: 41 native reads. Re-run all 41 through the production read dispatcher in four named CASA EUCALIPTO contexts.
- Isolation: create a fresh uniquely named detached disposable copy of each source model, unload links, close without saving, delete the copy, and verify the original source SHA-256 after execution.
- No transaction is permitted. Require unchanged element count and a non-modifiable document after every model.
- Retain only status, attempts, runtime target kind and a bounded failure reason. Do not retain returned model values or external paths.
- Each new operation is promotable only with at least one `read_succeeded` context. Other named contexts may remain explicit `missing_fixture`; no `read_failed` result is admissible.
- Evidence is tied to the named model and binary hashes. It does not establish universal applicability or guarantee that every compatible element returns a non-empty relationship.
