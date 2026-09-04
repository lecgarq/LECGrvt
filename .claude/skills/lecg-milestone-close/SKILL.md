---
name: lecg-milestone-close
description: Close a milestone — audit what actually shipped against what was promised, archive the phase artifacts, and harvest the lessons. Use when the user says "close the milestone", "we're done with v1", "wrap up the milestone", "audit the milestone", "is v2 done", or types /lecg-milestone-close. Not for finishing a single phase or task.
---

# Close milestone

Audit first, archive second. Archiving an unaudited milestone buries whatever was quietly skipped.

## 1. Audit

For every requirement in `.planning/REQUIREMENTS.md`, find the evidence in the **code**, not in the phase summaries. A phase that claims a requirement is done and left no trace in the repo did not do it.

| Requirement | Evidence | Verdict |
|---|---|---|
| R1 | `src/Core/RevitCommand.cs:52` | met |
| R4 | none found | **not met** |
| R7 | `src/Services/X.cs:20` — partial, no error path | **partial** |

Then check validation honesty across the milestone:
- Does the test suite pass right now? Run it — do not trust a summary.
- Which Revit-runtime behavior was claimed but never actually tested in Revit?

Report unmet and partial items plainly. Do not round up to success.

## 2. Decide

Present the gaps and let the user choose:

- **Close as-is** — gaps become known debt, recorded in `.planning/codebase/CONCERNS.md`
- **Close the gaps first** — the unmet items become phases in the current milestone; run `/lecg-phase` on them
- **Reduce scope** — the requirement was wrong; drop it and say why

Do not decide this yourself. Do not archive until they have chosen.

## 3. Harvest

Before the artifacts get archived, mine them once — this is the only moment the whole milestone is visible at once.

Into `docs/ai/repo-context.md`: conventions that proved themselves, commands that work, risks now verified.

Into `docs/ai/revit-protocol.md` under *Gotchas already paid for*: anything that cost real debugging time. This is the highest-value output of the whole milestone — a gotcha recorded here is one you never pay for twice.

Into `.planning/codebase/CONCERNS.md`: accepted debt, with why it was accepted.

Be strict: a lesson that is obvious from reading the code is not a lesson. Only what a future session would otherwise get wrong.

## 4. Archive

```
.planning/archive/<milestone>/
  REQUIREMENTS.md
  ROADMAP.md
  AUDIT.md          <- the table from step 1, plus the decision
  phases/
```

Move, don't copy. `.planning/` holds the current milestone only — that is what keeps it cheap to load.

Reset `.planning/STATE.md` to `between milestones`. Refresh `.planning/codebase/MAP.md` via `/lecg-map` so the next milestone starts from current structure.

Commit: `chore(planning): archive <milestone>`.

## 5. Report

What shipped, what did not, what was learned, where it went. Then offer `/lecg-milestone-new`.

## Rules

- Evidence from code, never from a phase's own summary of itself.
- Run the tests. Do not trust a claim that they passed.
- Never archive before the user decides on the gaps.
- A milestone closed with known gaps is fine. A milestone closed with hidden gaps is not.
