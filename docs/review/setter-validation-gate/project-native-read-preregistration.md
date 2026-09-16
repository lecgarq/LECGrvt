# CASA native-read validation

Revision, all 35 native read operations, the production `ToolExecutor` binary,
and all four CASA EUCALIPTO model hashes are frozen by
`project-native-read-manifest.json`.

Open only a fresh detached disposable copy of each model. Invoke every native
read through the production `AgentRead` path with deterministic arguments and at
most 12 contextual candidates. Record only status, attempt count, target API
kind, timing, and bounded failure reasons. Never retain returned model values.
Start no transaction and invoke no change operation.

Each result is `read_succeeded`, `read_failed`, or `missing_fixture`. These
outcomes establish only the named model and supplied context, not universal
applicability.

Every copy must close unsaved, be deleted, and leave its source hash unchanged.
