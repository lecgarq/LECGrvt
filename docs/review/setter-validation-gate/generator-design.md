# Phase 1 setter validation — design for approval

Status: runner gate PASSED; metadata resolved; setter test implementation NOT STARTED.
Only the 70 `same_value_only` operations are eligible. The 82 `missing_fixture`
and 35 `context_rejected` records remain untouched. Ledger remains **618/805**.

## Verified runner, not inferred compatibility

The pinned ricaun.RevitTest.TestAdapter 1.11.1 ran one NUnit 3.13.3 test through
Microsoft.NET.Test.Sdk 17.8.0 / SDK 10.0.400, targeting net10.0-windows, inside
installed Revit 2026.5, executable version 26.5.0.55.
The test asserted actual Revit process identity, injected UIApplication, version
2026, runtime major 10 and zero open documents. Result: 1 passed, 0 failed;
49.5521 seconds total including startup. ricaun closed the fresh process afterward.
No model was opened, transaction started or setter invoked. The test bridge bundle
remains installed in the user Autodesk ApplicationPlugins directory.

The upstream [changelog](https://github.com/ricaun-io/ricaun.RevitTest/blob/master/CHANGELOG.md)
now mentions Revit 2027/.NET 10; that was not accepted as proof for this installation.
See [actual TRX](ricaun-net10-revit2026.trx) and [binary provenance](verification.json).
This gate establishes runner execution only, not model-copy/transaction behavior
or correctness of any setter. Those checks belong in the approved harness.

## Mechanical surface and phase-1 types

ILSpy's ICSharpCode.Decompiler 9.1.0.7988 resolved all 70 property types from the
installed RevitAPI.dll, not from source guesses. All 805 distinct ledger operations
resolve to declared public setters with resolved return types. PublicApiGenerator
11.5.4 generated the public API of their 181 declaring types.

- [Public API baseline](setter-types.publicapi.txt): 306,597 bytes; generation
  refuses to overwrite a differing baseline. Prepared for version control, not committed.
- [Resolved setter surface](setter-surface.json): exact 805-operation scope,
  property types/tokens, 70 phase-1 flags, API XML documentation and hashes.

805 is the existing ledger's mechanically resolved write surface, not a claim that
Autodesk's entire API contains only 805 setters. Optional unrelated assembly
references may be absent; each reported property return type must still resolve.

| Generator family | Count | Validity policy before attempting a write |
|---|---:|---|
| ElementId | 42 | Property-specific eligible IDs from the same test document/API collections, including category IDs where required. Never arbitrary IDs, same-class guesses or undocumented negative sentinels. |
| String | 8 | Suffix only for documented text. Time properties use valid time formatting; size/key properties use their API-defined vocabulary. |
| Double / Int32 | 4 / 3 | Property-specific units, bounds, rounding grid, uniqueness and valid index set. No generic 0.1-percent or fixed-unit perturbation. |
| XYZ | 6 | Distinguish positions from directions; preserve documented plane, endpoint and orientation constraints. No blanket scaling/rotation. |
| Boolean | 4 | Negate only when the property-specific applicability/enablement gate permits it. |
| Enum | 3 | Distinct declared value plus property/context-specific allowed subset; enum membership alone is insufficient. |

Use small type-specific candidate helpers plus a concrete property-policy lookup,
not 70 copied test bodies or a new production abstraction. A generator returns
either a candidate with its constraint evidence and comparison policy, or a
`no_proven_valid_alternative` reason. A candidate must differ before the setter is
called. If no API-proven alternative exists in this fixture, do not invent one.
This is deliberately not a promise that every reachable setter has a second valid
value. Only the completed round trip can establish acceptance in a Revit context.

## API-derived exceptions to simplistic generators

These are extracted from this installation's RevitAPI.xml; exact property entries
are embedded in setter-surface.json, with an XML-file hash.

- StairsRun/landing elevations round to riser-height multiples. Choose a different
  valid riser-height step using the owning stairs, respect base/top/minimum-height
  restrictions and the documented 30,000-foot absolute bound. A tiny offset can
  legitimately round back to the original value.
- PipingSystemType.FluidTemperature is Kelvin and snaps to the nearest available
  temperature setting. Select a different available setting, not a guessed offset.
- OpeningTime/ClosingTime accept time strings such as `16:30`; parse, choose a
  different time and compare canonical times. Do not append arbitrary suffixes.
- WireType.MaxSize must be a conductor-size name or empty. Use the applicable
  size vocabulary, not a fabricated name.
- LoadCase.Number must be unique. Select an unused integer from the documented
  valid domain, checked against existing load cases, with overflow protection.
- ScheduleSheetInstance.SegmentIndex has schedule-specific restrictions; 0 and
  -1 can represent the same unsplit schedule. Do not count that alias as a change.
- Background fill patterns require Drafting targets; category IDs are not
  interchangeable with element IDs. Match each property's actual API requirement.
- CircuitConnectionType depends on its panel and feed-through settings. A valid
  enum value can still be invalid for that circuit.
- ViewSheetSet.SheetOrganizationId is ignored when automatic mode is false.
  Record that precondition; do not silently change another setter to force a pass.
- ReferencePlane endpoints must remain distinct and their connecting vector
  perpendicular to the normal. PathOfTravel endpoints retain view-level elevation.
- ModelCurve.TrussCurveType is documented as applicable only in truss families:
  classify **out-of-contract** without opening a family document.

The remaining property-policy selectors must be derived from the captured API
documentation and available runtime validation/collection methods during harness
implementation. Sparse documentation is not permission to guess a constraint.

## Transaction and evidence contract

1. Freeze the 12 named sample models and hashes, original per-model receipts,
   installed API/runner/test binaries, current revision and relevant working-tree
   source hashes before any setter test. Refuse any unknown/non-test path.
2. Work only on disposable, detached non-workshared project copies under an
   explicit test root. Reject linked/read-only/family documents; never open a
   production model or load a family document to improve the score.
3. Reuse an existing reachable target for a phase-1 operation. No creation of new
   element fixtures for the other 117 operations, nor hidden mutations to enable
   unrelated prerequisites. Record unavailable alternatives with a reason.
4. Snapshot immutable before-values (not mutable Revit object references).
   Verify candidate != before with the property-specific comparator.
5. Start TransactionGroup and Transaction; set the property, regenerate, read
   after, Commit and require TransactionStatus.Committed. Read again after commit
   to catch normalization/failure processing. Require after != before and the
   intended value after documented normalization. A discarded/rolled-back commit
   is never validated. Capture Revit failures; do not silently suppress them.
6. Always RollBack the outer TransactionGroup in finally. Verify rollback status
   and restored value. Close the disposable document without saving. Failure to
   restore aborts that model; do not continue after compromised isolation.

Snapshots/comparators: ordinal text or documented canonical form, exact booleans,
enum numeric identity, ElementId.Value, finite numeric values with property/API
tolerance or discrete grid, XYZ coordinate snapshots with geometric tolerance.
No global epsilon, object-reference inequality or same-value fallback.

## Result shape and reconciliation

Keep aggregate entries in the input's six-field shape: operation, state,
passed_models, context_failures, unsupported_models, same_value_models.
Put before/candidate/after values, stage, exception/failure reasons, rollback
receipt, binary/model hashes and source revision in a separate per-attempt file
keyed by run/model/operation/target. Do not add fake results for unexecuted tests.

Display labels map to aggregate states: validated -> changed_value_tested;
still-same-value -> same_value_only; rejected-with-reason -> context_rejected;
out-of-contract -> out_of_contract. The last is a test-ledger classification,
not a new MCP operation or permission. A generator refusal is explicitly marked
as stage=generator in its sidecar; it is not reported as an API-thrown exception.

Merge on stable model hash + operation, not by adding aggregate counts from
retries. Preserve historical evidence and recompute the six-field aggregates
from original and new named-model receipts. If original receipts cannot be
recovered, stop ledger reconciliation rather than invent per-model overlap.
Out-of-contract operations remain in the 805 denominator, with no successes.
New numerator = distinct operations with accepted changed-value evidence,
counted once; no gate test, generated surface or same-value write increases it.

## Approval boundary

No 70-case setter suite or value generators have been written, no per-setter
result has been claimed, and the ledger hash remains unchanged. Approve this
design before implementation and test-model execution. The original 82/35 groups
remain out of phase 1.

## Reproduce the gate and metadata check

Run from `C:\LECG\RevitAddins\LECG\tools\SetterValidationProbe` (its local
global.json selects SDK 10; the repository root deliberately selects SDK 8):

```powershell
dotnet build SetterValidationProbe.csproj --no-restore -p:SkipRevitDeploy=true
dotnet test SetterValidationProbe.csproj --no-build --no-restore --logger "trx;LogFileName=ricaun-net10-revit2026.trx" --results-directory C:/LECG/RevitAddins/LECG/docs/review/setter-validation-gate
```

The test command starts/closes a fresh Revit process; it is not an offline test.
Preserve the existing TRX before intentionally rerunning to the same result name.
For offline metadata only, run from `C:\LECG\RevitAddins\LECG\tools\SetterSurfaceAudit`:

```powershell
dotnet run --project SetterSurfaceAudit.csproj --no-restore -p:SkipRevitDeploy=true
```

The offline check was repeated successfully with identical public API output.
All recorded binary/input/TRX hashes, 805 unique operation identities and all 70
phase-1 documentation records were independently verified. Root non-deploying
build passed; its two NU1900 warnings concern offline vulnerability metadata.
