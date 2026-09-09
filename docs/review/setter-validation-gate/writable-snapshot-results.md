# Writable snapshot qualification — 635/805

All 14 dedicated-collection cases completed on the original, unchanged corpus.
**12 validated; 0 still-same-value; 2 rejected-with-reason; 0 out-of-contract.**
ConductorMaterial was already validated: **11 new setter identities**, taking the
repository ledger from **624 to 635/805**. No sample or setter was quarantined.

## Gate and scope

The direct live read returned **IsReadOnly=true** for element 1462965, parameter
-1006490 (SPOT_ELEV_SINGLE_OR_UPPER_VALUE), on Golden Nugget Architecture. This
read used no transaction or setter. Its exact value was again a tiny nonzero double;
no numerical normalization was used or needed to decide the scope.

The previous control established a non-setter-specific mismatch, not model instability.
The read-only result establishes the user-authorized operational boundary: exclude
read-only parameter **values** on non-target elements; retain identities/writability
and element metadata. Keep all readable target-element parameters, including
read-only ones, and independently compare the exact target property.

The same no-write control then passed. A separate live inclusion check verified all
four target/non-target and writable/read-only combinations. No epsilon, parameter-ID
exception, sample exclusion, fixture construction, family-document write or MCP
contract change was introduced. The shared snapshot function implements the rule.

The scope is not a claim that IsReadOnly exposes Revit's native storage mechanisms,
nor complete internal/geometry-state certification. Undefined parameter definitions
remain explicit gaps. For the control, 296,876 read-only non-target values were
excluded and three unreadable slots disclosed. All 29,881 element identities,
scoped values, warnings and the target restored. Every disposable copy was discarded.

## Per-setter results

Names below are prefixed by `api.set:Autodesk.Revit.DB.`. Model labels: GN = Golden
Nugget Architecture; H = Snowdon HVAC; S = Snowdon Structural; E = Snowdon Electrical.
Full model names, SHA256 hashes, before/desired/after values and target identities
are in the machine-readable receipts.

| Setter | Result | Model / explanation |
| --- | --- | --- |
| Analysis.MassLevelData.ConceptualConstructionId | validated | GN; formerly blocked case |
| Architecture.StairsRunType.NosingProfile | validated | S, after H had no alternative |
| Architecture.StairsRunType.RiserProfile | validated | S, after H had no alternative |
| Architecture.StairsRunType.TreadProfile | validated | S, after H had no alternative |
| Electrical.CableType.ConductorMaterial | validated | H; existing positive control, not a new identity |
| Electrical.CableType.InsulationMaterial | validated | H |
| Electrical.CableType.TemperatureRating | validated | H |
| Electrical.ElectricalSystem.CableSize | validated | E |
| Electrical.WireType.Insulation | validated | H |
| Electrical.WireType.TemperatureRating | validated | H |
| Electrical.WireType.WireMaterial | validated | H |
| Part.OriginalCategoryId | rejected-with-reason | no_proven_valid_alternative in all three historical reachable models |
| Structure.FabricArea.TagViewId | validated | GN; only historical reachable fixture retained |
| Structure.StructuralConnectionHandler.ApprovalTypeId | rejected-with-reason | no_proven_valid_alternative in GN, its only historical reachable fixture |

There were **19 operation/model attempts across six distinct source-model hashes**:
12 writes, all with committed changed-value read-back and verified rollback; seven
generator refusals without writes. All 19 cleanup/source/unsaved-copy checks passed.
No setter exception occurred in the 12 actual writes. The 12-model corpus was retained,
but six source models were actually opened under the original stop/order rules; do
not describe this as 14 setters validated across all 12 models.

The two refused setters remain same_value_only in the historical aggregate: the new
generator refusal does not erase a previously established identical-value write.
The batch report explicitly records both refusals. Ledger totals are **635 validated,
53 same-value-only, 82 missing-fixture, 35 context-rejected**, totaling 805. States in
the other two triage groups were not changed. Records retain the six input fields.
Retries merge by source-model hash, never by adding counters.

## Forecast — revised once, after completion and before class-collector work

Original forecast: **18–34/70**. Updated planning range: **30–44/70** total phase-1
validations, including **17 already established**. This is judgment, not a statistical
confidence interval or a guarantee. The dedicated batch established 12/14 property
successes and 12/12 successful writes once an eligible alternative was found; it
does not establish acceptance rates for class-based candidates.

The additional 13–27 estimate comprises 6–10 from the 17 still-unvalidated class-
collector setters, 1–3 from four category-constrained setters, and 6–14 from 22
untested non-ElementId setters. No forecast credit is assigned to the six undocumented
ElementId alternatives or the four already-attempted unresolved cases. The class-
collector bucket has 18 total, including the previously validated Material control.
No class-collector batch or remaining non-ElementId batch was implemented in this turn.

## Evidence and reproduction

Evidence root: `C:\LECG\RevitAddins\LECG\docs\review\setter-validation-gate`.
New manifest: `dedicated-writable-manifest.json`, linked by hash to the original
manifest, `writable-snapshot-amendment.md` and the live IsReadOnly receipt. It retains
all original cases, model hashes, model order and candidate rules unchanged.

- IsReadOnly: `readonly-20260909T060155-4039e5636a4a474fbed46e1e3e8bbb32.trx`;
  `restoration-runs/20260909T060232-b9ce4944bde8496d8533fbf9e6c154c3/`.
- Amended control and four-combination inclusion check: 2/2 passed;
  `control-20260909T060459-d2497570c85c468ca47b2b2115031dd1.trx`;
  `restoration-runs/20260909T060535-2ff3af7f5cfe4f559bc715deb1745c47/`.
- Dedicated run: `dedicated-20260909T060633-e4710c036e9e47d398f8332290ebc1f3.trx`,
  14/14 harness cases completed, 3.71 minutes including startup/shutdown.
  `dedicated-runs/20260909T060711-36d76f76abe047ff9ae49d413631fec1/` contains
  `dedicated-results.json`, `ledger-reconciliation.json`, all receipts and provenance.

Provenance records the base revision 0abda1e plus exact edited C# source hashes and
loaded binary hashes, Revit 2026.5 build 26.5.0.55 and .NET 10.0.11. It does not
pretend those edits were committed before execution. The ledger's source_sha256
identifies the new reconciliation, which links the prior pilot and original corpus.

Harness: `C:\LECG\RevitAddins\LECG\tools\SetterValidationProbe`, net10.0-windows/x64.
From that directory with Revit closed, `./Run-Pilot.ps1 -Batch Dedicated` builds
without deployment and executes the same 14-case plan through ricaun.RevitTest.
Legacy Pilot/Automatic manifests lack this amendment and now refuse in setup;
they must be explicitly re-registered before reuse. Old evidence is preserved.

Root check: `dotnet build -p:SkipRevitDeploy=true --no-restore`.
Offline checks: `node --test tools/SetterValidationProbe/*.test.mjs .codex/skills/lecg-map-codebase/scripts/*.test.mjs`.
Production add-ins were not deployed; the numerator is the repository ledger's count.
