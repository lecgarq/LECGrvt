---
name: lecg-milestone-new
description: Open a new milestone — turn a stated goal into a requirements list and a phased roadmap. Use when the user says "new milestone", "start v2", "what's the next milestone", "plan the next chunk of work", or types /lecg-milestone-new. Not for "next phase" — that is /lecg-phase.
---

# New milestone

A milestone is a version's worth of work: a goal, requirements that serve it, and phases that deliver them. Nothing more.

Refuse to open a new one while the current milestone is unfinished — run `/lecg-milestone-close` first, or say explicitly that this replaces it.

## 1. Source the goal

From `.planning/CANDIDATES.md` (shaped candidates), `.planning/codebase/CONCERNS.md`, or what the user just said. If it is vague, one round of questions — what does done look like, what is out of scope. Then stop. When a candidate drives the milestone, delete its entry from `CANDIDATES.md`.

State the goal in one sentence a stranger could act on. If you cannot, it is not a milestone yet — keep talking with the user until it is.

## 2. Requirements

Numbered, each testable. A requirement you cannot verify is a wish.

```markdown
- R1: Every command surfaces failures to the user instead of failing silently
```

Not `R1: Improve error handling`. Group them if there are more than a dozen. Anything the goal does not need is out of scope — write it down as such.

## 3. Roadmap

Break into phases. Each phase:

- Delivers something observable on its own
- Has an explicit requirement mapping (`covers: R1, R4, R7`)
- Depends only on phases before it

Sequence by dependency, then by risk — the phase that could invalidate the plan goes early, not last.

Every requirement must be covered by at least one phase. Show the count (`32/32 mapped`). An uncovered requirement is a planning bug; fix it before writing the file.

Prefer few phases. Eleven small phases and five real ones deliver the same thing; the five are cheaper to run.

## 4. Write

`.planning/REQUIREMENTS.md`:
```markdown
# Requirements — <milestone>

**Goal:** <one sentence>

## In scope
- R1: <testable statement>

## Out of scope
- <what this milestone does not do>
```

`.planning/ROADMAP.md`:
```markdown
# Roadmap — <milestone>

## Phase 1: <name>
**Goal:** <observable outcome>
**Covers:** R1, R4
**Depends on:** none
**Done when:** <verifiable condition>
```

`.planning/STATE.md` — reset to the new milestone. Keep the frontmatter shape; the next session reads it before the prose:

```yaml
---
state_version: 1.0
milestone: <slug>
milestone_name: <name>
status: planning
stopped_at: Phase 1 not started
last_updated: "<YYYY-MM-DD>"
last_activity: <YYYY-MM-DD> — milestone opened
progress:
  total_phases: <N>
  completed_phases: 0
---
```

## 5. Report

The goal, the phase count, the coverage count, and the first phase. Then offer `/lecg-discuss 1` or `/lecg-phase 1`.

## Rules

- Requirements are testable statements, never adjectives.
- Every requirement mapped, or the roadmap is not done.
- Do not research the ecosystem unless the user asks. This repo is a known quantity — read it instead.
- Fewer, larger phases beat many small ones.
