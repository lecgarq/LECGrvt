# Native MCP preview validation preregistration

- Denominator: the 17 `change` operations registered in `CapabilityCatalog.All` from the frozen Copilot binary.
- Corpus: the four named CASA EUCALIPTO architecture, topography, structure and MEP models with manifest SHA-256 hashes.
- Isolation: copy each source model into a unique run directory, detach/discard worksets, unload Revit/CAD links, close without saving, delete the copy, then verify the source hash.
- Execution: call the exact production `PreviewChange` path. Never call `ApplyChange`, never request local confirmation and never retain returned model values.
- Candidate policy: use documented operation-specific fixtures and stop after the first success or 12 bounded attempts.
- Restoration: after every attempt require a non-modifiable document, unchanged element count and unchanged `Document.IsModified`; after all previews require identical stored writable-parameter state and warnings.
- Outcomes: `preview_succeeded`, `preview_failed`, or `missing_fixture`. A test-runner pass alone does not promote a failed operation.
- Evidence scope: a success validates preview rollback in that named model context only; it is not universal applicability or committed-change evidence.
