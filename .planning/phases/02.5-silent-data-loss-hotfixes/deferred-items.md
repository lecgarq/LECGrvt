# Deferred Items — Phase 02.5

## Pre-existing build error (out of scope for 02.5-01)

**File:** `src/Commands/CategoryChangerCommand.cs` (line 189)
**Error:** `CS0103: The name 'GetUnsupportedReason' does not exist in the current context`
**Confirmed pre-existing:** Clean build passes on last commit (ca21850); error appears only in
unstaged working-tree modifications to `CategoryChangerCommand.cs` that pre-date this phase.
**Action:** Investigate and fix in a separate task or phase. Not introduced by 02.5-01 changes.
