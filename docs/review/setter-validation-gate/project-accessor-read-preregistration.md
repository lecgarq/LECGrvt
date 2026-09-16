# CASA accessor read validation

Revision, the 1,411 read operations, production `ToolExecutor` assembly, and all
four CASA EUCALIPTO model hashes are frozen by
`project-accessor-read-manifest.json`.

Open only a fresh detached disposable copy of each model. For every bound getter,
try at most 12 compatible elements through the production API-read path and record
only status, target/attempt counts, representative element ID/type, timing, and
bounded failure reasons. Never retain property values. Start no transaction and
invoke no setter.

Each result is one of `read_succeeded`, `context_unsupported`, `read_failed`, or
`missing_fixture`. These outcomes establish only the named model and candidate
contexts, not universal applicability.

Every copy must close unsaved, be deleted, and leave its source hash unchanged.
