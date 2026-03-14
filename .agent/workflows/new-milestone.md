---
description: Create a new milestone with phases
---

# /new-milestone Workflow

<objective>
Define a new milestone with goal, phases, and success criteria.
</objective>

<process>

## 1. Validate SPEC Exists

**PowerShell:**
```powershell
if (-not (Test-Path ".gsd/SPEC.md")) {
    Write-Error "SPEC.md required. Run /new-project first."
}
```

**Bash:**
```bash
if [ ! -f ".gsd/SPEC.md" ]; then
    echo "Error: SPEC.md required. Run /new-project first." >&2
fi
```

---

## 2. Gather Milestone Information

Ask for:
- **Name** â€” Milestone identifier (e.g., "v1.0", "MVP", "Beta")
- **Goal** â€” What does this milestone achieve?
- **Must-haves** â€” Non-negotiable deliverables
- **Nice-to-haves** â€” Optional if time permits

---

## 3. Generate Phase Breakdown

Based on goal and must-haves, suggest phases:

```markdown
## Suggested Phases

Phase 1: {Foundation/Setup}
Phase 2: {Core Feature A}
Phase 3: {Core Feature B}
Phase 4: {Integration/Polish}
Phase 5: {Verification/Launch}
```

Ask user to confirm or modify.

---

## 4. Update ROADMAP.md

```markdown
# ROADMAP.md

> **Current Milestone**: {name}
> **Goal**: {goal}

## Must-Haves
- [ ] {must-have 1}
- [ ] {must-have 2}

## Phases

### Phase 1: {name}
**Status**: â¬œ Not Started
**Objective**: {description}

### Phase 2: {name}
**Status**: â¬œ Not Started
**Objective**: {description}

...
```

---

## 5. Update STATE.md

```markdown
## Current Position
- **Milestone**: {name}
- **Phase**: Not started
- **Status**: Milestone planned
```

---

## 6. Commit

```bash
git add .gsd/ROADMAP.md .gsd/STATE.md
git commit -m "docs: create milestone {name}"
```

---

## 7. Offer Next Steps

```
â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”
 GSD â–º MILESTONE CREATED âœ“
â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”

Milestone: {name}
Phases: {N}

â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

â–¶ NEXT

/plan 1 â€” Create Phase 1 execution plans

â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
```

</process>
