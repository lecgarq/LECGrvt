param([ValidateSet('Pilot','Dedicated','ClassCollectors','NonElement','ProjectCorpus','Documented','DocumentedRetry','DocumentedNext','DocumentedNextRetry','DocumentedThird','DocumentedFourth','ProjectArchitecture','ProjectMepWire','Automatic','Restoration','Control','ReadOnly')][string]$Batch = 'Pilot')
$ErrorActionPreference = 'Stop'
$previousDotnetRootX64 = $env:DOTNET_ROOT_X64
$previousProjectFixtureRoot = $env:LECG_PROJECT_FIXTURE_ROOT
Push-Location $PSScriptRoot
try {
    if (Get-Process Revit -ErrorAction SilentlyContinue) { throw 'Close all Revit instances before this isolated pilot.' }
    $env:LECG_SETTER_REPO_ROOT = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
    if ($Batch -in @('ProjectCorpus','Documented','DocumentedRetry','DocumentedNext','DocumentedNextRetry','DocumentedThird','DocumentedFourth','ProjectArchitecture','ProjectMepWire') -and !$env:LECG_PROJECT_FIXTURE_ROOT) {
        $env:LECG_PROJECT_FIXTURE_ROOT = 'C:\Users\LECG Arquitectura\DC\ACCDocs\Arq. Luis Eduardo Cortés\CASA EUCALIPTO\Project Files'
    }
    $dotnet = if ($env:LECG_SETTER_DOTNET) { $env:LECG_SETTER_DOTNET } else { 'dotnet' }
    $sdkWorkDirectory = if ($env:LECG_SETTER_SDK_WORKDIR) { $env:LECG_SETTER_SDK_WORKDIR } else { $PSScriptRoot }
    if (-not (Test-Path -LiteralPath $sdkWorkDirectory -PathType Container)) {
        throw "SDK working directory does not exist: $sdkWorkDirectory"
    }
    $project = Join-Path $PSScriptRoot 'SetterValidationProbe.csproj'
    $runtimeRoot = if ($env:LECG_SETTER_RUNTIME_ROOT) { $env:LECG_SETTER_RUNTIME_ROOT } else { 'C:\Program Files\dotnet' }
    if (-not (Test-Path -LiteralPath (Join-Path $runtimeRoot 'shared\Microsoft.NETCore.App'))) {
        throw "Runtime root lacks Microsoft.NETCore.App: $runtimeRoot"
    }
    $env:DOTNET_ROOT_X64 = $runtimeRoot
    Push-Location $sdkWorkDirectory
    try {
        & $dotnet build $project --no-restore -p:SkipRevitDeploy=true
        if ($LASTEXITCODE -ne 0) { throw 'Build failed; refusing to run an older binary.' }
        $out = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../docs/review/setter-validation-gate'))
        $name = $Batch.ToLowerInvariant() + '-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmss') + '.trx'
        if (Test-Path -LiteralPath (Join-Path $out $name)) { throw "TRX already exists; refusing to merge runs: $name" }
        $filter = switch($Batch) { 'Pilot' {'FullyQualifiedName~ChangedValuePilot'} 'Dedicated' {'FullyQualifiedName~DedicatedCollectionBatch.RunDedicated'} 'ClassCollectors' {'FullyQualifiedName~ClassCollectorBatch.RunClassCollector'} 'NonElement' {'FullyQualifiedName~NonElementBatch.RunNonElement'} 'ProjectCorpus' {'FullyQualifiedName~ProjectCorpusBatch.RunProjectCorpus'} 'Documented' {'FullyQualifiedName~DocumentedConstraintBatch.RunDocumented'} 'DocumentedRetry' {'FullyQualifiedName~DocumentedRetryBatch.RunDocumentedRetry'} 'DocumentedNext' {'FullyQualifiedName~DocumentedNextBatch.RunDocumentedNext'} 'DocumentedNextRetry' {'FullyQualifiedName~DocumentedNextRetryBatch.RunDocumentedNextRetry'} 'DocumentedThird' {'FullyQualifiedName~DocumentedThirdBatch.RunDocumentedThird'} 'DocumentedFourth' {'FullyQualifiedName~DocumentedFourthBatch.RunDocumentedFourth'} 'ProjectArchitecture' {'FullyQualifiedName~ProjectArchitectureBatch.RunProjectArchitecture'} 'ProjectMepWire' {'FullyQualifiedName~ProjectMepWireBatch.RunProjectMepWire'} 'Automatic' {'FullyQualifiedName~AutomaticPersistenceProbe.ProbePersistence'} 'Restoration' {'FullyQualifiedName~RestorationDiagnostic.InspectFailedRestoration'} 'Control' {'FullyQualifiedName~RestorationDiagnostic.InspectNoWriteRestoration'} }
        $expected = switch($Batch) { 'Pilot' {8} 'Dedicated' {14} 'ClassCollectors' {18} 'NonElement' {14} 'ProjectCorpus' {11} 'Documented' {4} 'DocumentedRetry' {3} 'DocumentedNext' {5} 'DocumentedNextRetry' {5} 'DocumentedThird' {6} 'DocumentedFourth' {6} 'ProjectArchitecture' {4} 'ProjectMepWire' {1} 'Automatic' {1} 'Restoration' {1} 'Control' {1} }
        if ($Batch -eq 'ReadOnly') { $filter = 'FullyQualifiedName~RestorationDiagnostic.InspectParameterReadOnly'; $expected = 1 }
        if ($Batch -eq 'Control') { $filter += '|FullyQualifiedName~CompatibilityProbe.SnapshotIncludesAllTargetValuesAndOnlyWritableNonTargetValues'; $expected = 2 }
        & $dotnet test $project --no-build --no-restore --filter $filter --logger "trx;LogFileName=$name" --logger 'console;verbosity=normal' --results-directory $out
        if ($LASTEXITCODE -ne 0) { throw "Pilot failed: $name" }
    } finally {
        Pop-Location
    }
    $trx = [xml](Get-Content (Join-Path $out $name) -Raw)
    $counts = $trx.TestRun.ResultSummary.Counters
    if ([int]$counts.total -ne $expected -or [int]$counts.passed -ne $expected) { throw 'Runner did not complete all cases. Exit code zero alone is not success.' }
    Write-Output "$expected harness cases completed. Inspect the receipts for actual setter yield: $name"
} finally {
    Remove-Item Env:LECG_SETTER_REPO_ROOT -ErrorAction SilentlyContinue
    if ($null -eq $previousProjectFixtureRoot) { Remove-Item Env:LECG_PROJECT_FIXTURE_ROOT -ErrorAction SilentlyContinue }
    else { $env:LECG_PROJECT_FIXTURE_ROOT = $previousProjectFixtureRoot }
    if ($null -eq $previousDotnetRootX64) { Remove-Item Env:DOTNET_ROOT_X64 -ErrorAction SilentlyContinue }
    else { $env:DOTNET_ROOT_X64 = $previousDotnetRootX64 }
    Pop-Location
}
