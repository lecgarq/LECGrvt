# Eight-setter pilot — preregistered before implementation/execution

Revision: 774354b4d1d745b42ea2b73a73ad17cf68aebf58. Baseline: 618/805.
This is an estimate and selection plan, not test evidence. Do not revise the
predictions after seeing results; record deviations in a separate results file.

## Expected yield

Expected phase-1 improvement: **18–34 of 70** distinct setters, giving **636–652/805**
if all 70 are later approved and executed on the frozen 12-model corpus.
This is a judgment range, not a statistical confidence interval or guarantee.

| Family | Expected newly validated / available |
|---|---:|
| ElementId | 10–20 / 42 |
| String | 2–4 / 8 |
| Double + Int32 | 2–3 / 7 |
| XYZ | 2–3 / 6 |
| Boolean | 2–3 / 4 |
| Enum | 0–1 / 3 |

The ElementId classification is complete before any generator is implemented:
14 dedicated collections, 18 class collectors with property filters, 4 category
constraints, **6 without a proven eligible set**. The latter receive a planned
`no_proven_valid_alternative` refusal, not a claim that Autodesk has no such API.
Collection membership supplies a candidate, not a guarantee of context acceptance.
Some documented sets will be empty/singleton in the samples. Rebar dimension
subtypes still have sparse exception docs. Riser grids, available temperatures,
conductor vocabularies and circuit prerequisites further limit yield.
One truss-family-only enum is out-of-contract and cannot increase the numerator.

## Fixed pilot: expected 3–6 validated out of 8

Use **Snowdon Towers Sample Electrical.rvt** from the installed Autodesk Samples
directory for all eight. The historical receipt confirms each operation had a
same-value attempt there; this does not guarantee an alternative now exists.
No substitutions after seeing results. No fixture creation. Select the first
eligible existing target by ascending ElementId, with one candidate and one write.
Scan existing targets for documented eligibility, not by repeatedly trying writes.

| Property (Autodesk.Revit.DB prefix omitted) | Candidate / comparator |
|---|---|
| Electrical.CableType.ConductorMaterial | First distinct ID from ConductorMaterial.GetConductorMaterialIds; exact ID.Value |
| Material.CutBackgroundPatternId | First distinct drafting FillPatternElement; exact ID.Value |
| TextElement.Text | Existing TextNote plain text plus deterministic suffix; ordinal text, documented paragraph terminator normalization only |
| Plumbing.PipingSystemType.FluidTemperature | Different temperature from the current FluidType iterator; exact finite Kelvin value from that vocabulary |
| Structure.LoadCase.Number | Smallest unused positive Int32 across all LoadCases; exact integer |
| ReferencePlane.BubbleEnd | Extend the existing endpoint vector away from FreeEnd by its own length, preserving its plane; XYZ.IsAlmostEqualTo |
| ViewSheetSet.IsAutomatic | Boolean negation; exact bool; ignored changes count as still-same-value |
| Electrical.ElectricalSystem.CircuitConnectionType | Breaker to FeedThruLugs only with existing panel feed-through flag enabled; FeedThruLugs to Breaker; never NotApplicable; exact enum |

## Isolation and stopping rule

Each operation starts from a **separate disposable copy**, detached with worksets
discarded and Revit/CAD links unloaded before opening. Only the fixed sample hash
is accepted. This conservative fallback is enabled from the start: reuse requires
stronger evidence than a property read-back. Never save the opened test document.

Still exercise committed inner Transaction + regeneration + outer TransactionGroup
rollback in finally. Require Committed, different/equivalent-to-request read-back,
RolledBack, restored target value, unchanged complete element-ID set, and unchanged
per-element parameter snapshot plus warning identities. Record DocumentChanged
added/modified/deleted IDs. These observable checks **cannot prove all internal
Revit state restored**, nor can Element.VersionGuid (not updated per transaction).
Record this limit even on success; discard the copy regardless. A restoration or
cleanup failure fails the harness and aborts the remaining pilot, without credit.
Never suppress setter failures/warnings to force a pass. Opening warnings may be
recorded and acknowledged on the disposable copy only; errors abort opening.

Refusal is a measured result, not a test infrastructure failure. The eight NUnit
cases test receipt/isolation invariants; their pass count is not the setter yield.
Do not implement the remaining 62 after this pilot without reporting its result.

`out_of_contract` must remain visible in state counts and the 805 denominator.
Merge receipts by source-model SHA256 + operation, preserving historical receipts
and distinguishing original sample context from any historical added fixtures.
