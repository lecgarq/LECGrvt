# Setter evidence archive path relocation — 2026-09-11

The archived run directories were shortened from `<timestamp>-<run-guid>` to
`<timestamp>`. This fixes checkout failures on Windows Git clients whose workspace
prefix pushes the original tracked paths beyond `MAX_PATH`.

No receipt, checkpoint, reconciliation, TRX, manifest, or provenance file was
edited as part of the relocation. The original runtime paths embedded in those
immutable records continue to describe where each run was produced. The adjacent
`archive-path-map.json` maps each original run name to its repository archive name
and records a deterministic SHA-256 tree digest over the unchanged files.

With the self-hosted runner prefix
`C:\actions-runner\lecg-revit2026\_work\LECGrvt\LECGrvt\`, the longest tracked
path is now 250 characters. It was 283 characters before the relocation.
