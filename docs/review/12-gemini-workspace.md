# Gemini Workspace Guide

## Purpose

This guide defines a practical `.gemini` workspace for high-velocity development with controlled risk.

The structure follows an antigravity-style split:

- Skills: reusable task capabilities.
- Workflows: ordered execution loops.
- Agents: role-specific operating profiles.

## Workspace Layout

```text
.gemini/
  GEMINI.md
  skills/
    revit2026-safety.md
    ci-release.md
    docs-sync.md
  workflows/
    small-diff-loop.md
    feature-slice.md
  agents/
    refactor-agent.md
    review-agent.md
```

## Usage Model

1. Select one agent profile based on intent.
2. Attach one workflow for execution order.
3. Attach one or more skills for guardrails and domain behavior.
4. Execute in small diffs with validation after each step.

## Recommended Defaults

For most plugin work:

- Agent: `refactor-agent`
- Workflow: `small-diff-loop`
- Skills:
  - `revit2026-safety`
  - `docs-sync`

For release and CI work:

- Agent: `review-agent`
- Workflow: `feature-slice`
- Skills:
  - `ci-release`
  - `docs-sync`

## Prompt Starters

Refactor pass:

```text
Use .gemini/agents/refactor-agent.md with
.gemini/workflows/small-diff-loop.md and
.gemini/skills/revit2026-safety.md.
Implement the change in minimal diffs and run build/test gates.
```

Review pass:

```text
Use .gemini/agents/review-agent.md with
.gemini/workflows/feature-slice.md and
.gemini/skills/ci-release.md.
Review for regressions, CI risk, and release readiness.
```

## Maintenance

- Keep prompts/tooling constraints aligned with `docs/review/03-organization.md`.
- When CI or release process changes, update:
  - `.gemini/skills/ci-release.md`
  - this document
  - `docs/review/10-release-checklist.md`
