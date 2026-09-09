# Observed response sizes — request planning only

Warm live sample, 2026-09-08, HEAD 774354b. Source: case ledgers in
`docs/review/lecg-query-live-acceptance.json`. These sizes guide request limits;
they are neither repository evidence nor universal bounds. Load once at setup.

| Route / response shape | Observed chars / UTF-8 bytes | First max_answer_chars |
|---|---:|---:|
| freshness (integrated probe/project properties) | 1,438 / 1,438 | script-controlled |
| AssignMaterialCommand.Execute body | 1,888 / 1,888 | 2,100 |
| MaterialAssignmentExecutionService.AssignMaterialsToElements body | 1,950 / 1,950 | 2,100 |
| References to that assignment method | 446 / 446 | 600 |
| CapabilityCatalog.All, identity only | 283 / 283 | 400 |
| Exact registration pattern, rename | 322 / 322 | 450 |
| CapabilityCatalog.Require body | 572 / 572 | 700 |
| ValidateChangeDocument body | 525 / 525 | 650 |
| References to that guard | 953 / 953 | 1,100 |
| Q service Materials | 2,034 / 2,034 | script-controlled |

Case 2's 1,800-character service-body cap returned a 274-byte shortened response,
then required the 1,950-byte retry: 5 calls / 5,996 bytes. Sizing that first request
at 2,100 avoids the known under-sizing; growth can still exhaust the budget.
Live retest at the same revision: 4 calls / 5,722 bytes, no retry, 422 bytes spare.
Evidence: docs/review/lecg-query-followup-live.json, case 2.
Do not treat an empty lookup's two-byte `[]` as a useful sizing baseline.
Record new sizes in maintenance/evaluation artifacts, never a per-query cache.
No blanket implementation-set size is established; completeness must be checked.
