# LECG Plugin Deployment Guide

## Installation

### 1. Copy Plugin Files

Copy the compiled plugin DLL and dependencies to a local directory:
```
C:\LECG\RevitAddins\LECG\bin\Debug\net8.0-windows\
```

Or for production:
```
C:\Program Files\LECG\Revit 2026\
```

### 2. Create .addin Manifest

1. Copy `LECG.addin.template` from this directory
2. Rename to `LECG.addin`
3. Replace `YOUR-GUID-HERE` with a unique GUID (use Visual Studio > Tools > Create GUID)
4. Update the `<Assembly>` path to point to your LECG.dll location:
   ```xml
   <Assembly>C:\LECG\RevitAddins\LECG\bin\Debug\net8.0-windows\LECG.dll</Assembly>
   ```

### 3. Deploy .addin File

Copy `LECG.addin` to the Revit addins directory:
```
%AppData%\Autodesk\Revit\Addins\2026\
```

Full path example:
```
C:\Users\YourUsername\AppData\Roaming\Autodesk\Revit\Addins\2026\LECG.addin
```

### 4. Restart Revit

The LECG ribbon tab should appear in Revit after restart.

---

## Troubleshooting

### Plugin doesn't load
- Check Revit's Add-In Manager (File > Options > Add-Ins)
- Look for error messages in the Revit Journal file: `%AppData%\Autodesk\Revit\Journals\`

### Missing dependencies
- Ensure all DLLs from the build output are in the same directory as LECG.dll
- Check that .NET 8.0 Desktop Runtime is installed

### AssemblyResolve conflicts
- The plugin includes an AssemblyResolve handler in `App.cs` to handle version conflicts
- If you see assembly load errors, check the Revit Journal file for details

---

## Build Configurations

### Debug Build
```bash
dotnet build -c Debug
```
- Located at: `bin\Debug\net8.0-windows\`
- Includes debug symbols (.pdb files)
- Enables `#if DEBUG` code paths

### Release Build
```bash
dotnet build -c Release
```
- Located at: `bin\Release\net8.0-windows\`
- Optimized for performance
- Code analysis warnings treated as errors (CI enforcement)

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

To target multiple Revit versions, update `<Assembly>` path in the .addin file per version.
