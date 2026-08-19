# Roadmap — Warnings Review

Coverage: 8/8 requirements mapped.

## Phase 1: Warnings command end to end
**Goal:** The Warnings button exists and, in a project document, opens a view listing all warnings grouped by description with working select/show/isolate actions — everything except live-Revit proof.
**Covers:** R1, R2, R3, R4, R5, R6, R7
**Depends on:** none
**Done when:** build green with the button registered in the Health panel; `LECG.Core` grouping tests pass; service/ViewModel tests cover the skip-and-continue path (R7) and the no-transaction constraint (R6) is visible in the dependency graph.
**Open decision for /lecg-discuss:** modal vs modeless view. Select/isolate need valid API context; a modal `LecgDialog` keeps everything synchronous (no ExternalEvent), but blocks comparing warnings against the model while browsing. Modeless requires the `ExternalEventCommand` pattern — which still lacks a reentrancy guard (accepted debt). Decide before planning.

## Phase 2: Live Revit validation
**Goal:** The command is proven against a real model with the user at the keyboard.
**Covers:** R8
**Depends on:** Phase 1
**Done when:** group counts match Revit's Review Warnings dialog on the test model, select/isolate verified on correct elements, isolation confirmed temporary, and the run is recorded in `docs/ai/revit-smoke-test.md` Run History.
