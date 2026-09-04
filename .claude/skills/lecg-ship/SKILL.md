---
name: lecg-ship
description: Ship a release of the add-in — version bump, changelog, validation, Revit smoke test, tag, and GitHub release. Use when the user says "ship it", "cut a release", "release v1.2", "publish a release", "tag a version", or types /lecg-ship. Not for publishing Revit models to the cloud — that is a runtime feature. The smoke test is a hard gate, not a formality.
---

# Ship

A Revit add-in that compiles and ships broken is worse than one that never shipped — it lands in `%APPDATA%\Autodesk\Revit\Addins\2026\` and breaks Revit startup for whoever installed it. The smoke test is the gate.

## 1. Preflight

Stop and report if any of these fail. Do not proceed and fix things silently.

- Working tree clean (`git status --porcelain` empty)
- `git branch --show-current` is `main`, or a branch whose PR into `main` is open and green
- `dotnet build -p:SkipRevitDeploy=true` — 0 errors **and 0 warnings**; this repo builds clean, so a new warning is a finding, not noise
- `dotnet test -c Debug -p:SkipRevitDeploy=true` — all pass. The flag is mandatory: `dotnet test` builds the add-in and the deploy step fails with MSB3027 while Revit is open
- CI green on `main` for the commit you are about to tag: `gh run list --workflow ci --branch main --limit 1` shows `success`. `release.yml` rebuilds and re-tests on a hosted runner before it publishes — a red `ci` means the tag will produce no release
- Self-hosted runner online: `gh api repos/lecgarq/LECGrvt/actions/runners --jq '.runners[] | "\(.name) \(.status)"'` shows `lecg-revit2026 online`. `main` is protected and requires both `core-tests` and `plugin-build`; `plugin-build` only runs on that runner, so an offline runner means the version-bump PR can never merge
- Coverage gate holds — that `ci` run enforces 90 % line coverage on `LECG.Core`; if the last push only touched docs, the previous run's verdict still stands

A failing test blocks the release. Do not ship around it.

## 2. Version

Read the current `VersionPrefix` from `Directory.Build.props` (line ~4). That is the single source of truth — nothing else holds a version.

Pick the bump from what actually changed since the last tag (`git log $(git describe --tags --abbrev=0)..HEAD --oneline`):

- **patch** — bug fixes, no new command, no signature change
- **minor** — new command, new ribbon button, new option
- **major** — a Revit version target changed, or existing behavior breaks

Propose the number and what it is based on. Let the user confirm before writing it. If the user already named a version, still say what the log supports; if it disagrees, say so once, then use theirs.

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

Without `-p:SkipRevitDeploy=true` — here you *want* the deploy. **Revit must be closed first**: with it open the copy fails on locked DLLs (MSB3027) or leaves a stale mix. It copies dll/pdb/deps.json to `%APPDATA%\Autodesk\Revit\Addins\2026\LECG\`. It does not touch the `.addin` manifest; that is installed manually beside that folder and already in place.

Then open Revit and, before anything visual, confirm it loaded **the build you just deployed** — a stray second manifest (e.g. under `C:\ProgramData\Autodesk\Revit\Addins\2026\`) makes Revit pick one silently:

```bash
rg -n "Starting External Application: LECG|Duplicate addins|assembly: .*LECG\.dll" "$(ls -t "$LOCALAPPDATA/Autodesk/Revit/Autodesk Revit 2026/Journals/"journal.*.txt | head -1)"
```

`Assembly Version` must match `VersionPrefix`, and the `assembly:` path must be the `%APPDATA%` folder. If `Duplicate addins:` appears, stop and remove the stray manifest: the smoke test would validate the wrong binary.

Then run `docs/ai/revit-smoke-test.md` in Revit: startup, ribbon loads, a representative command, empty-selection edge case, transaction behavior, a modeless flow.

If the `mcp-server-for-revit` tools are connected, run the queryable parts through them (document state, service paths, element results) and record which steps they covered. The visual steps — ribbon, dialogs, undo — remain the user's; the gate is not passed until those are confirmed too.

**This is the gate.** If Revit was not opened, the release does not go out. Say `Revit smoke test: not executed — release blocked` and stop. Never tag on a build result alone.

## 5. Tag and release

`main` is protected — the bump goes in through a PR, the tag goes on the merge commit:

```bash
git checkout -b chore/release-vX.Y.Z
git commit -am "chore(release): vX.Y.Z"
git push -u origin chore/release-vX.Y.Z
gh pr create --fill --base main
gh pr checks --watch          # core-tests and plugin-build must both pass
gh pr merge --squash --delete-branch
git checkout main && git pull
git tag vX.Y.Z && git push origin vX.Y.Z
```

Tags are `vX.Y.Z`, pre-releases `vX.Y.Z-rcN`. Pushing the tag triggers `.github/workflows/release.yml`, which creates the GitHub Release with generated notes.

Confirm it actually happened — a tag with no release is a failed ship, not a finished one:

```bash
gh run list --workflow release --limit 1     # must be success
gh release view vX.Y.Z                       # notes generated
```

`release.yml` attaches no files — the installable build is the local `dotnet build -c Release` output, not a release asset. If someone expects a download on the release page, that is a workflow change, not a ship step.

## 6. Report

Version, what changed, smoke-test result, release URL.

## Rollback

If it shipped broken: mark the GitHub Release as draft, fix forward on `main`, ship a new patch tag. Never delete or move a published tag — anyone who pulled it now has a different build under the same name.

## Rules

- Never tag without a passing Revit smoke test.
- Never ship with failing tests.
- `Directory.Build.props` is the only version source. If you find a version hardcoded elsewhere, that is a bug — report it.
- Never edit `.addin` manifests as part of a release.
