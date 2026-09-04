---
name: lecg-discuss
description: Gather context for a roadmap phase before planning it — surface the decisions, unknowns, and assumptions that would otherwise be guessed. Use before /lecg-phase on anything non-obvious, when the user says "let's talk about phase N", "what do you need to know before phase N", "assumptions for phase N", or types /lecg-discuss. Skip for phases whose approach is already unambiguous.
---

# Discuss

Planning a phase you don't understand produces a plan that looks right and is wrong. This is the cheap step that prevents that.

Skip it when the phase is genuinely obvious — say so and go straight to `/lecg-phase`. Most phases in a well-shaped roadmap are obvious. This exists for the ones that aren't.

## Steps

**1. Load the phase.** `.planning/ROADMAP.md` for the phase goal and its requirements. `.planning/STATE.md` for where the project actually is.

**2. Look before asking.** Read the code the phase touches — use `/lecg-map` for blast radius rather than grepping. Every question you can answer from the repo is a question you must not ask. This is most of them.

**3. State your assumptions.** Before questioning, list what you are currently assuming about the approach. Be specific enough to be wrong:

> - Error handling goes in a shared helper in `LECG.Core`, not per-command
> - Existing commands get migrated in this phase, not a later one
> - `ITransactionService` stays untouched

Wrong assumptions get corrected here for free. This is the highest-value step — do not skip it.

**4. Ask only what's left.** Genuine forks where the repo doesn't decide it and the choice changes the work. Offer concrete options, not open questions:

> Shared error helper — static utility, or injected service?
> - Static: simplest, no DI churn, harder to test in isolation
> - Injected: testable, consistent with the service layer, more wiring

Follow their answer wherever it goes. Stop as soon as you could write the plan. Three good questions beat ten.

**5. Write** `.planning/phases/<NN-slug>/CONTEXT.md`:

```markdown
# Phase <N> Context

## Goal
<from roadmap>

## Decisions
- <question> -> <what was decided, and why>

## Assumptions confirmed
- <the ones that survived step 3>

## Constraints
<existing patterns this must not break — cite file:line>

## Out of scope
<what this phase does not do>

## Open
<anything still unresolved, and what would resolve it>
```

Then offer `/lecg-phase <N>`.

## Rules

- **Never ask what the repo answers.** Read first. Every time.
- Two to four questions. If you have ten, you skipped step 2.
- Never ask about the user's technical experience.
- When they want to explain freely, stop offering options and let them talk.
- If nothing genuinely needs deciding, write the CONTEXT.md from what you read and say the phase was already clear.
