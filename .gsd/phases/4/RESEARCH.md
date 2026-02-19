# Phase 4 Research: Verification Strategy

## Objective
Define how to verify the new features (Naming, Purge, Selection) without a live Revit environment during unit testing, and how to update the massive service catalog.

## Findings

### 1. Unit Testing Strategy
- **Renaming**: The `RenameRuleEngine` is already well-tested. We need to verify that `SearchReplaceService` and `BatchRenameExecutionService` correctly pipe the new categories (Material, Object Style, etc.).
- **Triple Purge**: We can verify the `PurgePassSequenceService` returns the correct numbers. Testing the actual purge depends on the `Document` and `PerformanceAdviser`.
- **Selection Safety**: We can mock `FamilyInstance` and `Family` to check the `IsWorkPlaneBased` property logic.

### 2. Documentation Challenge
The project has undergone a massive decomposition (from 16 services to 130+). 
- **Pattern**: COORDINATOR -> SUB-SERVICES.
- **Example**: `FamilyConversionService` coordinates `Naming`, `Logging`, `SourceDocument`, `TargetDocument`, `TemplatePath`, `ParameterSetup`, `GeometryCopy`, `Finalize`, `Cleanup`.
- **Action**: Update `02-components.md` to describe these "Service Families" rather than individual files.

## Verification Plan
1. Implement logic tests in `LECG.Tests`.
2. Manual audit of `ROADMAP.md` must-haves.
3. Update `01-architecture.md` and `02-components.md`.
