# Query/report boundary and routing corrections

Live revision: `774354b`, 2026-09-08. This supersedes the prior sweep's pagination
suggestion and its classification of case 4 as merely a structural limitation.
Historical packets and CRG comparisons are preserved unchanged.

## Applied

- Added the explicit `export-report.mjs unvalidated-setters` command. It reuses
  the existing selector, checks freshness before and after preparing records,
  writes a unique JSON file under docs/review with exclusive creation, and returns
  count/path/revision/checksum. No pagination, cache or additional dependency.
  Ordinary summary queries remain read-only and do not auto-export.
- Semantic completeness now routes to Serena declaration resolution followed by
  find_implementations. Ast-grep is only for syntactic shapes, not direct-base-list
  substitutes for all implementations.
- Recorded observed response sizes in the skill's response-sizes reference,
  loaded once for first-request planning. These are measurements, not live source
  evidence or universal response bounds. No per-query cache writes.
- Removed the active discovery rule, routing row, worked example, case 11 and
  inferred tier. Six evidence tiers remain. No global tool/plugin uninstallation.
- Kept cases 6 and 10 as abstention checks; real-target comparisons remain in
  existing cases 12 and 13. Updated case 8's superseded stale baseline.

## Live retests

| Case | Result | Calls | Evidence bytes |
|---|---|---:|---:|
| 2 | Same bounded command-to-service path, no undersized request/retry | 4 | 5,722 |
| 3 | All 187 setter records exported; packet contains receipt, not identities | 2 | 1,962 |
| 4 | Correct compiler route; blocked at external metadata declaration | 3 | 1,994 |

Case 2 improves from 5 calls / 5,996 bytes to 4 calls / 5,722 bytes. Headroom is
422 bytes rather than 148: better, still narrow. No cold-run or token-saving claim.

Case 4: Serena resolves the known source usage container, then declaration lookup
for IExternalCommand returns an outside-configured-workspaces error for its
MetadataAsSource file. There is no valid interface declaration returned to supply
to find_implementations. The implementation query therefore did not run and
completeness remains unqualified. No interface copy, workspace expansion, base-class
substitution or ast-grep fallback was attempted.

For a future Roslyn provider qualification, after exact caller counts/sets check
external-interface implementations (including indirect inheritance), then missing
symbol and ambiguous/underspecified signature abstention. Distinguish missing
from ambiguous structured errors. No new provider was installed or qualified here.

## Deliverables and execution

- [Full 187-setter report](unvalidated-setters-12c46a90-96ae-46dc-b14d-01bf45655121.json) — 46646 bytes; SHA-256 `1c835fefca84d2af5c28e5621dfd185f6c76fde93010addae3bf2bcdd7883c32`.
- [Raw retest evidence and packets](lecg-query-followup-live.json).

From `C:\LECG\RevitAddins\LECG`:

```powershell
node .codex/skills/lecg-map-codebase/scripts/export-report.mjs unvalidated-setters
```

This creates another report without overwriting the delivered one. Node scripts
use the standard library; production target frameworks and source are unchanged.
Build checks use `dotnet build -p:SkipRevitDeploy=true --no-restore`; no deployment
or Revit model operation is part of this work.
