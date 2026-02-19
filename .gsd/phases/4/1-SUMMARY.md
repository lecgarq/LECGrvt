# Plan 4.1 Summary: Logic Tests & Verification

## Objective
Empirically verify the logic of new features via unit tests.

## Completed Tasks
- [x] Test Family Conversion Naming
  - Refactored `FamilyConversionNamingService` to use `LECG.Core.Naming.FamilyNamePolicy`.
  - Created `FamilyNamePolicyTests.cs` to verify collision handling.
- [x] Test Purge Pass Sequence
  - Refactored `PurgePassSequenceService` to use `LECG.Core.Purge.PurgeSequence`.
  - Created `PurgeSequenceTests.cs` to verify sequence generation.
- [x] Test Selection Filter Logic
  - Refactored `FamilyInstanceFilter` to use `LECG.Core.Naming.FamilySelectionPolicy`.
  - Created `FamilySelectionPolicyTests.cs` to verify safety logic.

## Verification
- Ran `dotnet test`.
- All 18 tests passed (including new ones).

## Commit
`feat(phase-4): implement unit tests for v1.1 logic`
