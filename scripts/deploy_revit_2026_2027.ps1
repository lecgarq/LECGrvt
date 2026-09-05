$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir
$projectPath = Join-Path $projectRoot 'LECG.csproj'

$dotnetCandidates = @(
    (Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe'),
    (Join-Path $env:USERPROFILE '.local\dotnet\dotnet.exe'),
    'C:\Program Files\dotnet\dotnet.exe'
)

$dotnetPath = $dotnetCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $dotnetPath) {
    throw 'No dotnet executable was found. Install a .NET SDK or update scripts\deploy_revit_2026_2027.ps1.'
}

Write-Host "Using dotnet: $dotnetPath"
Write-Host 'Building and deploying LECG for Revit 2026 and 2027...'

& $dotnetPath msbuild $projectPath `
    -t:BuildAndDeploySupportedRevitVersions `
    -p:Configuration=Debug `
    -nologo

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
