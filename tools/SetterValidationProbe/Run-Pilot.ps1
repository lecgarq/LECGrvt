$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    if (Get-Process Revit -ErrorAction SilentlyContinue) { throw 'Close all Revit instances before this isolated pilot.' }
    dotnet build SetterValidationProbe.csproj --no-restore -p:SkipRevitDeploy=true
    if ($LASTEXITCODE -ne 0) { throw 'Build failed; refusing to run an older binary.' }
    $out = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../docs/review/setter-validation-gate'))
    $name = 'changed-value-pilot-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmss') + '-' + [guid]::NewGuid().ToString('N') + '.trx'
    dotnet test SetterValidationProbe.csproj --no-build --no-restore --filter 'FullyQualifiedName~ChangedValuePilot' --logger "trx;LogFileName=$name" --logger 'console;verbosity=normal' --results-directory $out
    if ($LASTEXITCODE -ne 0) { throw "Pilot failed: $name" }
    $trx = [xml](Get-Content (Join-Path $out $name) -Raw)
    $counts = $trx.TestRun.ResultSummary.Counters
    if ([int]$counts.total -ne 8 -or [int]$counts.passed -ne 8) { throw 'Runner did not complete eight cases. Exit code zero alone is not success.' }
    Write-Output "Eight harness cases completed. Inspect the receipts for actual setter yield: $name"
} finally { Pop-Location }
