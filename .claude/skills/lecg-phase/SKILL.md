---
name: lecg-phase
description: Plan and execute a roadmap phase end to end — evidence-grounded plan, surgical edits, honest validation, atomic commits, state and memory updated. Use when the user says "do phase N", "next phase", "let's build it", "execute the plan", or types /lecg-phase. This is the main build loop.
---

# Phase

Plan, build, validate, commit. One skill, because splitting it just made the handoffs expensive.

Argument: a phase number, or nothing — then read `.planning/STATE.md` and use the current phase.

## 1. Ground

Read, in this order, stopping when you have enough:

- `.planning/STATE.md` — where the project is
- `.planning/ROADMAP.md` — this phase's goal and requirements
- `.planning/phases/<NN-slug>/CONTEXT.md` — if `/lecg-discuss` ran
- `docs/ai/repo-context.md` — conventions, commands, gotchas
- `.planning/codebase/MAP.md` — structure and blast radius
- **The actual files this touches.** Never plan from documents alone.

If the change touches the Revit API or deployment, also read `docs/ai/revit-protocol.md`. If it touches WPF views, styles, or resources, read `docs/ai/ui-guide.md` — its scoping rule is not optional.

## 2. Plan

Write `.planning/phases/<NN-slug>/PLAN.md`. Short. Every claim cites `file:line`:

```markdown
# Phase <N> Plan

## Goal
<what this delivers — one or two sentences>

## Evidence
- <file:line> — <what it shows that shapes the approach>

## Files changing
<explicit list. Anything outside it is a deviation.>

## Steps
1. <ordered, each independently verifiable>

## Risks
<what could break, and how the plan avoids it. Revit rows from revit-protocol.md if applicable.>

## Validation
<the exact commands that will prove this works>
```

Then check it backwards: *if every step passes, is the goal achieved?* If not, the plan is wrong — fix it before writing code. Show the plan and get a go-ahead before executing.

## 3. Execute

One step at a time.

- Smallest diff that achieves the step. Reuse the patterns already in the repo.
- No drive-by refactors, renames, or reformatting outside the plan.
- No `dynamic`, casts, or suppressions to silence the compiler.
- No new dependencies unless the plan justified one and nothing installed covers it.
- Commit each step atomically: `<type>(<scope>): <what>` — build must pass at every commit.

**Deviating from the plan** is allowed when the code proves the plan wrong. Say so, say why, update `PLAN.md`, continue. Silent deviation is the failure mode, not deviation.

**Blocked** — stop, report what blocked you and what you tried. Do not invent a workaround around a wall.

## 4. Validate

Run what the plan said. Report per level, never blurred:

- `dotnet build -p:SkipRevitDeploy=true` — **always this flag.** A plain build overwrites the live Revit 2026 add-in folder.
- `dotnet test`
- Revit runtime: only claimable if Revit was actually opened. If the `mcp-server-for-revit` tools are connected (Revit open with the MCP plugin service on), use them — query the document and execute the changed path in the live session; that counts as runtime validation *for what it exercised*. Ribbon presence, dialog binding, and undo grouping still need eyes on Revit. Otherwise write `Revit runtime validation: not executed` and list the pending smoke-test steps from `docs/ai/revit-smoke-test.md`.

Paste real failing output. Never paraphrase a failure away. A phase with failing tests is not done — say it plainly.

## 5. Record

- `.planning/STATE.md` — phase status, what's next, date.
- `docs/ai/repo-context.md` — only **stable** knowledge: a convention confirmed, a command that works, a verified risk. Skip transient detail. Correct anything the code proved wrong.
- `docs/ai/revit-protocol.md` — append to *Gotchas already paid for* if this phase cost real debugging time.
- `.planning/codebase/MAP.md` — patch the edges you changed.

## 6. Report

```
## Done
<outcome, plain language>

## Files
<list>

## Validation
<per level, actual results, including failures and "not run">

## Next
<the next phase, or what is blocking>
```

Nothing else. No summary of the summary.

## Rules

- Evidence before plan, plan before code. The order is the whole method.
- Never claim validation you did not run.
- Never claim Revit runtime behavior without opening Revit.
- A step that cannot be verified is not a step — rewrite it.
