# Compacting Styles

## Intent

`Compacting Styles` is a normalization and consolidation command family for polluted project settings. It is not a blind purge tool.

Workflow:

1. Scan the selected style category.
2. Detect duplicates by actual graphic behavior, not by name.
3. Create one new canonical definition per duplicate group.
4. Rewire reachable references from the old definitions to the new canonical one.
5. Delete redundant originals when possible.
6. Report what was compacted, swapped, deleted, and blocked.

Positioning:

- Normalize
- Consolidate
- Replace
- Purge residuals

## Command Family

Planned scopes inside the same family:

- `Line Patterns`
- `Line Styles`
- `Fill Patterns`
- `Text Types`

The first implementation is intentionally limited to `Line Patterns`.

## Current Implementation: Line Patterns

Implemented command:

- `LECG.Commands.CompactingStylesCommand`

Implemented service:

- `LECG.Services.LinePatternCompactionService`

Current behavior:

- Collect all non-solid `LinePatternElement` definitions.
- Group duplicates by dash-space segment structure.
- Create a new canonical pattern named with the `LECG-LP-###` prefix.
- Rewire reachable references found through:
  - category and subcategory line-pattern assignments
  - element and element-type parameters that store the old line-pattern `ElementId`
  - view category overrides
  - view filter overrides
- Attempt deletion of each original duplicate after rewiring.
- Log blocked deletions instead of failing silently.

## Current Boundaries

Implemented now:

- line-pattern normalization only
- canonical replacement instead of preserving one existing pattern
- structured log reporting inside the command run

Deferred to future scopes:

- line-style consolidation by weight, color, and pattern
- fill-pattern consolidation by drafting/model class
- text-type consolidation by formatting definition
- richer user-facing reporting UI beyond the log window

## Notes

- The existing `Purge` command remains separate and keeps its current meaning: delete unused content.
- `Compacting Styles` is the standards-focused command that actively consolidates duplicated definitions into new controlled office-standard definitions.
