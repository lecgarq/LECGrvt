# Installs existing, tested Release outputs only. Never builds or changes source/UI files.
param([switch]$CopilotOnly)
$ErrorActionPreference = 'Stop'
if (Get-Process Revit -ErrorAction SilentlyContinue) { throw 'Save and close Revit before installing.' }
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$nativeRoot = 'C:\ProgramData\Autodesk\Revit\Addins\2026\LECG'
$addinRoot = Join-Path $env:APPDATA 'Autodesk\Revit\Addins\2026'
$copilotRoot = Join-Path $addinRoot 'RevitCopilot'
$legacyRoot = Join-Path $env:LOCALAPPDATA 'RevitCopilot\mcp'
$nativeSource = Join-Path $workspace 'bin\x64\Release\net8.0-windows'
$copilotSource = Join-Path $workspace 'RevitCopilot\bin\x64\Release\net10.0-windows'
$nativeManifest = 'C:\ProgramData\Autodesk\Revit\Addins\2026\LECG.addin'
[xml]$registered = Get-Content -LiteralPath $nativeManifest -Raw
if ($registered.RevitAddIns.AddIn.Assembly -ne (Join-Path $nativeRoot 'LECG.dll')) { throw 'Native manifest changed; inspect the registered installation before proceeding.' }
foreach ($required in @((Join-Path $nativeSource 'LECG.dll'), (Join-Path $copilotSource 'RevitCopilot.dll'), (Join-Path $copilotSource 'mcp\RevitCopilot.McpServer.dll'))) {
    if (!(Test-Path -LiteralPath $required -PathType Leaf)) { throw "Missing tested build: $required" }
}
$backup = Join-Path $workspace ('outputs\deployment-backups\combined-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $backup | Out-Null
$items = [Collections.Generic.List[object]]::new()
function Add-File([string]$source, [string]$destination, [string]$allowedRoot) {
    $full = [IO.Path]::GetFullPath($destination)
    $prefix = [IO.Path]::GetFullPath($allowedRoot).TrimEnd('\') + '\'
    if (!$full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw "Unsafe destination: $full" }
    if ((Get-Item -LiteralPath $source -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Reparse-point source rejected.' }
    if ((Test-Path -LiteralPath $full -PathType Leaf) -and (Get-FileHash -LiteralPath $full).Hash -eq (Get-FileHash -LiteralPath $source).Hash) { return }
    $items.Add([pscustomobject]@{ source=$source; destination=$full; source_sha256=(Get-FileHash -LiteralPath $source).Hash;
        existed=(Test-Path -LiteralPath $full); backup=(Join-Path $backup ($items.Count.ToString('D4') + '-' + [IO.Path]::GetFileName($full))) })
}
function Get-RelativeChildPath([string]$root, [string]$path) {
    $fullRoot = [IO.Path]::GetFullPath($root).TrimEnd('\') + '\'
    $fullPath = [IO.Path]::GetFullPath($path)
    if (!$fullPath.StartsWith($fullRoot, [StringComparison]::OrdinalIgnoreCase)) { throw "Path is outside root: $fullPath" }
    return $fullPath.Substring($fullRoot.Length)
}
$sourcePairs = [Collections.Generic.List[object]]::new()
$sourcePairs.Add(@($copilotSource,$copilotRoot))
if (!$CopilotOnly) { $sourcePairs.Add(@($nativeSource,$nativeRoot)) }
foreach ($pair in $sourcePairs) {
    Get-ChildItem -LiteralPath $pair[0] -File | Where-Object { $_.Name -match '\.(dll|pdb)$|\.(deps|runtimeconfig)\.json$' } | ForEach-Object {
        Add-File $_.FullName (Join-Path $pair[1] $_.Name) $pair[1]
    }
}
$mcpSource = Join-Path $copilotSource 'mcp'
Get-ChildItem -LiteralPath $mcpSource -File -Recurse | ForEach-Object {
    $relative = Get-RelativeChildPath $mcpSource $_.FullName
    Add-File $_.FullName (Join-Path (Join-Path $copilotRoot 'mcp') $relative) $copilotRoot
    if (Test-Path -LiteralPath $legacyRoot -PathType Container) { Add-File $_.FullName (Join-Path $legacyRoot $relative) $legacyRoot }
}
Add-File (Join-Path $workspace 'RevitCopilot\RevitCopilot.addin') (Join-Path $addinRoot 'RevitCopilot.addin') $addinRoot
Copy-Item -LiteralPath $nativeManifest -Destination (Join-Path $backup 'native-manifest-unchanged.addin')
foreach ($item in $items) {
    if ($item.existed -and ((Get-Item -LiteralPath $item.destination -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "Reparse-point destination file rejected: $($item.destination)" }
    $parent = Split-Path -Parent $item.destination
    while (Test-Path -LiteralPath $parent) {
        if ((Get-Item -LiteralPath $parent -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Reparse-point destination rejected: $parent" }
        $next = Split-Path -Parent $parent
        if (!$next -or $next -eq $parent) { break }
        $parent = $next
    }
    if ($item.existed) {
        $probe = [IO.File]::Open($item.destination, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::ReadWrite)
        $probe.Dispose()
        Copy-Item -LiteralPath $item.destination -Destination $item.backup
    }
}
$items | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $backup 'file-map.json')
$attempted = [Collections.Generic.List[object]]::new()
try {
    if (Get-Process Revit -ErrorAction SilentlyContinue) { throw 'Revit opened during preflight; install cancelled.' }
    foreach ($item in $items) {
        New-Item -ItemType Directory -Path (Split-Path -Parent $item.destination) -Force | Out-Null
        $attempted.Add($item)
        Copy-Item -LiteralPath $item.source -Destination $item.destination -Force
        if ((Get-FileHash -LiteralPath $item.destination).Hash -ne $item.source_sha256) { throw "Installed checksum mismatch: $($item.destination)" }
    }
    [ordered]@{ status='installed_verified'; timestamp_utc=[DateTimeOffset]::UtcNow.ToString('o'); files=$items.Count; backup=$backup;
        native_dll=(Join-Path $nativeRoot 'LECG.dll'); native_updated=(!$CopilotOnly); copilot_dll=(Join-Path $copilotRoot 'RevitCopilot.dll'); manifests_preserved=$nativeManifest } |
        ConvertTo-Json | Tee-Object -FilePath (Join-Path $backup 'receipt.json')
} catch {
    $failure = $_
    $rollbackErrors = @()
    for ($i=$attempted.Count-1; $i -ge 0; $i--) {
        $item = $attempted[$i]
        try {
            if ($item.existed) {
                if (!(Test-Path -LiteralPath $item.destination) -or (Get-FileHash -LiteralPath $item.destination).Hash -ne (Get-FileHash -LiteralPath $item.backup).Hash) {
                    Copy-Item -LiteralPath $item.backup -Destination $item.destination -Force
                }
            }
            elseif (Test-Path -LiteralPath $item.destination -PathType Leaf) { Remove-Item -LiteralPath $item.destination }
        } catch { $rollbackErrors += $_.Exception.Message }
    }
    throw "Installation failed: $failure. Backup: $backup. Rollback errors: $($rollbackErrors -join '; ')"
}
