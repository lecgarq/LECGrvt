param([ValidateSet('Pilot','Dedicated','Automatic','Restoration','Control','ReadOnly')][string]$Batch = 'Pilot')
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    if (Get-Process Revit -ErrorAction SilentlyContinue) { throw 'Close all Revit instances before this isolated pilot.' }
    dotnet build SetterValidationProbe.csproj --no-restore -p:SkipRevitDeploy=true
    if ($LASTEXITCODE -ne 0) { throw 'Build failed; refusing to run an older binary.' }
    $out = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../docs/review/setter-validation-gate'))
    $name = $Batch.ToLowerInvariant() + '-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmss') + '-' + [guid]::NewGuid().ToString('N') + '.trx'
    $filter = switch($Batch) { 'Pilot' {'FullyQualifiedName~ChangedValuePilot'} 'Dedicated' {'FullyQualifiedName~DedicatedCollectionBatch.RunDedicated'} 'Automatic' {'FullyQualifiedName~AutomaticPersistenceProbe.ProbePersistence'} 'Restoration' {'FullyQualifiedName~RestorationDiagnostic.InspectFailedRestoration'} 'Control' {'FullyQualifiedName~RestorationDiagnostic.InspectNoWriteRestoration'} }
    $expected = switch($Batch) { 'Pilot' {8} 'Dedicated' {14} 'Automatic' {1} 'Restoration' {1} 'Control' {1} }
    if ($Batch -eq 'ReadOnly') { $filter = 'FullyQualifiedName~RestorationDiagnostic.InspectParameterReadOnly'; $expected = 1 }
    if ($Batch -eq 'Control') { $filter += '|FullyQualifiedName~CompatibilityProbe.SnapshotIncludesAllTargetValuesAndOnlyWritableNonTargetValues'; $expected = 2 }
    dotnet test SetterValidationProbe.csproj --no-build --no-restore --filter $filter --logger "trx;LogFileName=$name" --logger 'console;verbosity=normal' --results-directory $out
    if ($LASTEXITCODE -ne 0) { throw "Pilot failed: $name" }
    $trx = [xml](Get-Content (Join-Path $out $name) -Raw)
    $counts = $trx.TestRun.ResultSummary.Counters
    if ([int]$counts.total -ne $expected -or [int]$counts.passed -ne $expected) { throw 'Runner did not complete all cases. Exit code zero alone is not success.' }
    Write-Output "$expected harness cases completed. Inspect the receipts for actual setter yield: $name"
} finally { Pop-Location }
