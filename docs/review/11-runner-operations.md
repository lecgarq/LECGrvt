# Runner Operations Runbook

## Scope

This runbook covers the self-hosted Windows runner used by `plugin-build`:

- Runner name: `lecg-revit2026`
- Labels: `self-hosted`, `Windows`, `X64`, `revit2026`
- Repository: `lecgarq/LECGrvt`

## Service Commands

Run these in an elevated PowerShell in `C:\actions-runner\lecg-revit2026`:

```powershell
Get-Service actions.runner.* | Select-Object Name, Status, StartType
Stop-Service actions.runner.lecgarq-LECGrvt.lecg-revit2026
Start-Service actions.runner.lecgarq-LECGrvt.lecg-revit2026
Restart-Service actions.runner.lecgarq-LECGrvt.lecg-revit2026
```

## Token Rotation and Reconfiguration

Use fresh tokens generated from GitHub API:

```powershell
$gh = "C:\Program Files\GitHub CLI\gh.exe"
Set-Location C:\actions-runner\lecg-revit2026

$removeToken = (& $gh api -X POST repos/lecgarq/LECGrvt/actions/runners/remove-token | ConvertFrom-Json).token
.\config.cmd remove --token $removeToken

$regToken = (& $gh api -X POST repos/lecgarq/LECGrvt/actions/runners/registration-token | ConvertFrom-Json).token
.\config.cmd --unattended --url https://github.com/lecgarq/LECGrvt --token $regToken --labels "revit2026" --name "lecg-revit2026" --replace --runasservice
```

## Common Failures

### Jobs stuck in `queued`

- Check runner inventory:
  - `gh api repos/lecgarq/LECGrvt/actions/runners`
- Ensure runner status is `online`.
- Ensure labels include `revit2026`.

### `running scripts is disabled on this system`

- Cause: PowerShell execution policy blocks temporary `.ps1` scripts.
- Current mitigation: `plugin-build.yml` runs build commands with `shell: cmd`.

### `.NET setup` permission errors on self-hosted runner

- Cause: setup action attempting install to `C:\Program Files\dotnet` without write access.
- Current mitigation: rely on preinstalled .NET and verify with `dotnet --info`.

## Smoke Validation

Trigger a manual smoke run:

```powershell
gh workflow run plugin-build.yml --ref main
gh run list --workflow plugin-build.yml --limit 3
```

Expected result: `plugin-build` completes with `success`.

## Manual Recovery Workflow

For deeper diagnostics and artifact capture, trigger:

```powershell
gh workflow run plugin-diagnostics.yml --ref main
gh run list --workflow plugin-diagnostics.yml --limit 3
```

Expected result: `plugin-diagnostics` completes with `success` and uploads `plugin-diagnostics-build`.
