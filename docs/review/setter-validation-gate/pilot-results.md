# Changed-value pilot: 6 new validations; ledger 624/805

The eight-case pilot is complete. **6 validated, 1 still-same-value, 1 refused
with a generator reason, 0 out-of-contract in this pilot.** This is within the
preregistered **3–6/8** estimate. The full-phase estimate remains **18–34/70**;
this deliberately selected pilot is not a random sample and does not justify
extrapolating a 75% success rate to all 70.

Only these eight policies are implemented. The other 62 phase-1 cases have not
run. No fixtures or tests were added for the 82 missing-fixture or 35
context-rejected operations. No production model or family document was opened.
No MCP contract, production add-in deployment, Git commit or push was performed.

## ElementId classification before implementation

[All 42 classifications](elementid-classification.csv) are checked against the
exact metadata-resolved operation set. The [frozen manifest](pilot-manifest.json)
contains installed Autodesk API XML evidence, hashes and all 12 model identities.

| Eligible-set source | Setters |
|---|---:|
| Dedicated API collection | 14 |
| Class collector with property-specific filtering | 18 |
| Category-constrained | 4 |
| No sufficiently documented selection rule | 6 |

The six planned `no_proven_valid_alternative` refusals are both ContinuousRailType
termination properties, FloorType.StructuralMaterialId, MEPHiddenLineSettings.LineStyle,
TableView.TargetId and View.ViewPositionId. This describes what was established
from the inspected API, not a claim that no alternative API exists. They were
classified, not executed or added as runtime failures to the ledger.

## Per-setter results

Prefix below: `Autodesk.Revit.DB`. All eight used an independently copied,
detached **Snowdon Towers Sample Electrical.rvt**, with links unloaded.

| Property | Outcome | Evidence |
|---|---|---|
| Electrical.CableType.ConductorMaterial | Validated | Different ID from ConductorMaterial.GetConductorMaterialIds accepted and read back |
| Material.CutBackgroundPatternId | Validated | Different drafting FillPatternElement accepted and read back |
| Plumbing.PipingSystemType.FluidTemperature | Validated | Different available temperature from the existing FluidType, exact Kelvin read-back |
| ReferencePlane.BubbleEnd | Validated | Endpoint extended along its existing in-plane vector, geometric read-back |
| Structure.LoadCase.Number | Validated | Unused positive integer accepted and read back |
| TextElement.Text | Validated | Text suffix accepted with exact ordinal read-back |
| ViewSheetSet.IsAutomatic | Still-same-value | Transaction committed; negation did not survive read-back; no validation credit |
| Electrical.ElectricalSystem.CircuitConnectionType | Rejected-with-reason | `no_proven_valid_alternative`; existing circuit/panel prerequisites did not provide an eligible alternate; no setter call |

The enum refusal proves the refusal path, **not** a successful enum-write/rollback
path. That generator family still needs a separately preregistered eligible
context before claiming all generator families are qualified for expansion.

## Execution and isolation evidence

Final run: `20260909T051316-96a20f7cfb034e4fae35e1fa7d1e3531`.
[TRX](changed-value-pilot-20260909T051241-c7607be1284445dab5ffa1915f9e1ebf.trx):
8 NUnit cases passed, 0 failed; **2.5864 minutes** including Revit startup/shutdown.
These are eight successful receipt/isolation checks, not eight setter validations.
ricaun.RevitTest.TestAdapter/Application 1.11.1 executed inside Revit 2026.5,
runtime .NET 10.0.11; test target `net10.0-windows`, x64.

Each attempted write committed inside an outer transaction group, was read after
regeneration and after commit, then rolled back in `finally`. All seven attempted
writes restored the target and the observed snapshot: **13,813 element identities**,
readable parameters and warning identities. Each of the six genuinely changed
setters has both a TransactionCommitted and TransactionGroupRolledBack event.
The unchanged Boolean had no change events; the refused enum had no transaction.

**15 parameter slots have no API definition and are explicitly unobservable.**
Internal/geometry state is not completely captured either. No receipt certifies
complete internal restoration or safe reuse of a document. The approved fallback
was used: **a fresh copy for every operation, closed without saving, never reused**.
All eight cleanup receipts passed; Revit exited, and all 12 original sample hashes
were rechecked unchanged. Ignored on-disk test copies remain for inspection.

The [protocol amendment](pilot-protocol-amendment.md) documents the snapshot limit.
[Attempt 01](pilot-attempt-01.md) crashed before the first setter; the invalid build
retry was cancelled; attempt 03 refused the undefined parameter before a write.
A subsequent completed run found the same 6/1/1 outcomes but captured no events
because document wrappers were compared by reference. The final repeat corrected
that comparison. Earlier runs are retained and **not counted again**.

## Ledger and provenance

[Eight six-field result records](pilot-runs/20260909T051316-96a20f7cfb034e4fae35e1fa7d1e3531/pilot-results.json),
[before/after reconciliation](pilot-runs/20260909T051316-96a20f7cfb034e4fae35e1fa7d1e3531/ledger-reconciliation.json),
and [binary/source provenance](pilot-runs/20260909T051316-96a20f7cfb034e4fae35e1fa7d1e3531/provenance.json)
link the individual receipts. Revision is `774354b4d1d745b42ea2b73a73ad17cf68aebf58`
plus the explicitly hashed, uncommitted test sources—not a claim that this harness
already exists in that commit. The PublicApiGenerator 805-setter baseline from
the preceding compatibility gate is unchanged.

The six successes each replace one same-value model outcome: passed_models 0→1,
same_value_models 12→11. Retries are deduplicated by source-model hash + operation.
The enum's new refusal does not erase its historical same-value evidence; that
refusal remains visible in the pilot report and receipt. No aggregate counter was
blindly incremented. Applied ledger equals the reviewed proposal exactly.

| Ledger setter state | Before | After |
|---|---:|---:|
| changed_value_tested | 618 | 624 |
| same_value_only | 70 | 64 |
| missing_fixture | 82 | 82 |
| context_rejected | 35 | 35 |
| out_of_contract | 0 | 0 |
| Total | 805 | 805 |

`out_of_contract` is preserved by export/reconciliation, displayed in report
counts when present, and cannot coexist with contradictory pass evidence.
Synthetic regression tests exercise it without inventing a family-document run.
The runtime ledger reader already preserves arbitrary state strings and all 2,216
binding records, so no production MCP schema/permission change was necessary.

Applied ledger SHA256:
`E982AF133E2D9B704563E82BF340047BC046169687B6432E9F79E2C29F39A9C7`.
Its source_sha256 identifies the reconciliation report, which links the historical
campaign and final pilot receipts. This is direct API evidence in that named
context, not proof of every MCP execution path or every project.

## Checks and next gate

Repository and pilot builds passed with deployment disabled; the research exporter
build passed. Offline tests: 16 passed, 1 intentionally skipped after ledger editing.
All 17 passed before the ledger change. The skip is the live export integration:
inventory was refreshed and matches the working tree, but the revision freshness
gate correctly remains STALE because the ledger changes are not committed.
Do not bypass that gate or pretend another refresh can settle an uncommitted input.
The generic skill validator could not run because PyYAML is absent; targeted script
tests passed. Root builds warn that offline NuGet vulnerability metadata is unavailable.

Review the pilot/observability limit before writing the remaining 62. The enum
write path remains unqualified; retain fresh-copy isolation and the original
18–34 expected full-phase yield until additional evidence establishes otherwise.

Reproduce the pilot (starts and closes its own Revit; all other Revit instances
must be closed) from `C:\LECG\RevitAddins\LECG\tools\SetterValidationProbe`:

```powershell
.\Run-Pilot.ps1
```

The wrapper builds first, refuses an old binary after a build failure, uses a
unique TRX filename and rejects exit-zero/zero-test runs. It does not apply the
ledger proposal or deploy the add-in. A new ledger baseline requires a separate
reviewed preregistration; the original frozen manifest is never overwritten.
