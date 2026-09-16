# MEP circuit connection retry

Revision and model hash are frozen by `project-mep-circuit-manifest.json`.
Run only against a fresh detached copy of the CASA EUCALIPTO MEP model;
never save or modify the source model.

The installed Revit 2026 API contract proves only these changed alternatives:

- a circuit currently using `FeedThruLugs` may use `Breaker`;
- a circuit without a base panel must use `NotApplicable`.

Do not infer `Breaker -> FeedThruLugs` from the panel parameter: the earlier
MEP receipt proved that heuristic can disagree with Revit's full validity
rules. If neither documented transition exists, record
`no_proven_valid_alternative` without attempting a setter. Expected ledger
yield is 0-1 validation.
