---
name: lecg-ship
description: Ship a release of the add-in — version bump, changelog, validation, Revit smoke test, tag, and GitHub release. Use when the user says "ship it", "cut a release", "release v1.2", "publish", "tag a version", or types /lecg-ship. The smoke test is a hard gate, not a formality.
---

# Ship

A Revit add-in that compiles and ships broken is worse than one that never shipped — it lands in `C:\ProgramData\...\Addins\2026\` and breaks Revit startup for whoever installed it. The smoke test is the gate.

## 1. Preflight

Stop and report if any of these fail. Do not proceed and fix things silently.

- Working tree clean (`git status --porcelain` empty)
- On `main`, or a branch that is about to merge to it
- `dotnet build -p:SkipRevitDeploy=true` — 0 errors
- `dotnet test` — all pass

A failing test blocks the release. Do not ship around it.

## 2. Version

Read the current `VersionPrefix` from `Directory.Build.props` (line ~4). That is the single source of truth — nothing else holds a version.

Pick the bump from what actually changed since the last tag (`git log $(git describe --tags --abbrev=0)..HEAD --oneline`):

- **patch** — bug fixes, no new command, no signature change
- **minor** — new command, new ribbon button, new option
- **major** — a Revit version target changed, or existing behavior breaks

Propose the number and what it is based on. Let the user confirm before writing it.

## 3. Changelog

Add a section to `CHANGELOG.md`, newest first:

```markdown
## [X.Y.Z] - YYYY-MM-DD

### Added
- <new command or capability, in user terms>

### Fixed
- <what was broken, described by symptom not by patch>
```

Write for the person installing it, not for the person who wrote the diff. "Align Edges no longer offsets linked geometry" beats "fix transform in AlignEdgesService".

## 4. Deploy locally and smoke test

```bash
dotnet build -c Release
```

Without `-p:SkipRevitDeploy=true` — here you *want* the deploy. It copies dll/pdb/deps.json to `C:\ProgramData\Autodesk\Revit\Addins\2026\LECG\`. It does not touch the `.addin` manifest; that is installed manually and already in place.

Then run `docs/ai/revit-smoke-test.md` in Revit: startup, ribbon loads, a representative command, empty-selection edge case, transaction behavior, a modeless flow.

If the `mcp-server-for-revit` tools are connected, run the queryable parts through them (document state, service paths, element results) and record which steps they covered. The visual steps — ribbon, dialogs, undo — remain the user's; the gate is not passed until those are confirmed too.

**This is the gate.** If Revit was not opened, the release does not go out. Say `Revit smoke test: not executed — release blocked` and stop. Never tag on a build result alone.

## 5. Tag and release

```bash
git commit -am "chore(release): vX.Y.Z"
git tag vX.Y.Z
git push origin main --tags
```

Tags are `vX.Y.Z`, pre-releases `vX.Y.Z-rcN`. Pushing the tag triggers `.github/workflows/release.yml`, which creates the GitHub Release with generated notes.

Confirm the release actually appeared and the artifacts attached. A tag with no release is a failed ship, not a finished one.

## 6. Report

Version, what changed, smoke-test result, release URL.

## Rollback

If it shipped broken: mark the GitHub Release as draft, fix forward on `main`, ship a new patch tag. Never delete or move a published tag — anyone who pulled it now has a different build under the same name.

## Rules

- Never tag without a passing Revit smoke test.
- Never ship with failing tests.
- `Directory.Build.props` is the only version source. If you find a version hardcoded elsewhere, that is a bug — report it.
- Never edit `.addin` manifests as part of a release.
