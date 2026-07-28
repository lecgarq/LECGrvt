# LECG Plugin Deployment Guide

> Updated 2026-07-04 to match the verified installed setup (live manifest + Revit journal + `LECG.csproj` deploy target). Previous revisions described a per-user `%AppData%` install that does not exist on the working machine.

## How Deployment Actually Works

Deployment has two independent pieces:

### 1. DLLs — deployed automatically by the build

The `DeployToRevit` MSBuild target in `LECG.csproj` copies `*.dll`, `*.pdb`, and `*.deps.json` from the build output to:

```
C:\ProgramData\Autodesk\Revit\Addins\2026\LECG\
```

after **every** build, unless skipped. The build target does **not** generate, copy, or touch any `.addin` manifest.

**Build commands:**

- Validation / development build (safe, no deploy) — the default for agents and CI:
  ```bash
  dotnet build -p:SkipRevitDeploy=true
  ```
- Deploying build:
  ```bash
  dotnet build
  ```
  ⚠️ **Warning:** plain `dotnet build` overwrites the live add-in that Revit loads. Close Revit first — with Revit open the copy fails on locked files or leaves a mixed-version folder. Only run it when deployment is the intent.

CI (`GITHUB_ACTIONS`/`CI` env vars) sets `SkipRevitDeploy=true` automatically (`LECG.csproj`).

### 2. `.addin` Manifest — installed manually, once

The manifest is **not** produced by the build. It lives machine-wide at:

```
C:\ProgramData\Autodesk\Revit\Addins\2026\LECG.addin
```

and points Revit at `C:\ProgramData\Autodesk\Revit\Addins\2026\LECG\LECG.dll` (`Type="Application"`, `FullClassName=LECG.App`). Verified via Revit journal: Revit 2026 loads LECG from exactly this path. No per-user (`%AppData%`) manifest exists or is needed.

**Do not manually edit the live manifest** unless you are intentionally installing or updating the add-in.

**Installing on a new machine:**

1. Copy `LECG.addin.template` from this directory and rename it to `LECG.addin`.
2. Set a unique GUID and update `<Assembly>` to the deployed DLL path (`C:\ProgramData\Autodesk\Revit\Addins\2026\LECG\LECG.dll`).
3. Place the file in `C:\ProgramData\Autodesk\Revit\Addins\2026\` (machine-wide — the canonical setup for this project). Revit also supports per-user `%AppData%\Autodesk\Revit\Addins\2026\`, but do not create both.
4. Run a deploying build (`dotnet build`) with Revit closed, then start Revit — the LECG ribbon tab should appear.

**Known quirk:** the working live manifest uses `<ClientId>` for its GUID element while the template uses `<AddInId>`. The live file demonstrably works; do not unify the two without testing in Revit.

---

## Troubleshooting

### Plugin doesn't load
- Check Revit's Add-In Manager (File > Options > Add-Ins)
- Look for error messages in the Revit Journal file: `%LocalAppData%\Autodesk\Revit\Autodesk Revit 2026\Journals\`

### Missing dependencies
- Ensure all DLLs from the build output are in the same directory as LECG.dll
- Check that .NET 8.0 Desktop Runtime is installed

### Assembly version conflicts
- There is no AssemblyResolve handler in `App.cs` — version resolution is left to Revit's default load order (first-loaded version wins in the shared AppDomain)
- Revit journals may log `API_ERROR { Assembly version conflict ... }` at LECG load time when other installed add-ins preload different versions of shared assemblies (observed: Clipper2Lib, Microsoft.Extensions.DependencyInjection.Abstractions). The add-in still loads, but check the journal when debugging behavior that only reproduces inside Revit.

---

## Build Configurations

### Debug Build
```bash
dotnet build -c Debug -p:SkipRevitDeploy=true
```
- Located at: `bin\x64\Debug\net8.0-windows\`
- Includes debug symbols (.pdb files)
- Enables `#if DEBUG` code paths

### Release Build
```bash
dotnet build -c Release -p:SkipRevitDeploy=true
```
- Located at: `bin\x64\Release\net8.0-windows\`
- Optimized for performance
- Code analysis warnings treated as errors (CI enforcement)

(Omit `-p:SkipRevitDeploy=true` only when you intend to deploy to the live Revit addins folder.)

---

## CI/CD Notes

### Current CI Pipeline
- Runs on: GitHub Actions (windows-latest)
- Builds: LECG.Core in Release mode
- Tests: 27 xUnit tests with 90% coverage threshold
- Analysis: Roslyn analyzers enabled (dead code detection)

### Self-Hosted Runner
- Plugin build workflow runs on `self-hosted revit2026` runner
- Full plugin build (not just LECG.Core)
- No integration tests yet (would require Revit API mocking)

---

## Version Compatibility

| Revit Version | .NET Version | Status |
|--------------|--------------|--------|
| Revit 2026   | .NET 8.0     | ✅ Active |
| Revit 2025   | .NET 8.0     | ⚠️ Untested |
| Revit 2024   | .NET 8.0     | ⚠️ Untested |

To target multiple Revit versions, install a `.addin` manifest per version and update its `<Assembly>` path.
