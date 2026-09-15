$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$evidence = Join-Path $root 'docs/review/setter-validation-gate'
$target = Join-Path $evidence 'project-architecture-manifest.json'
if (Test-Path -LiteralPath $target) { throw 'Project-architecture manifest already frozen.' }

$sourcePath = Join-Path $evidence 'documented-fourth-manifest.json'
$source = Get-Content $sourcePath -Raw | ConvertFrom-Json
$architecture = @($source.models | Where-Object name -eq 'LECG_RVT_DISEÑO (ANTEPROYECTO).rvt')
if ($architecture.Count -ne 1) { throw 'Architecture fixture is not uniquely defined.' }
$model = $architecture[0]
$orders = [ordered]@{
    'AssemblyInstance.NamingCategoryId' = @($model.name)
    'Architecture.StairsLanding.BaseElevation' = @($model.name)
    'Architecture.StairsRun.BaseElevation' = @($model.name)
    'Architecture.StairsRun.TopElevation' = @($model.name)
}
$ledgerPath = Join-Path $root 'RevitCopilot/Agent/Knowledge/revit2026-validation.json'
$ledger = Get-Content $ledgerPath -Raw | ConvertFrom-Json
$previousPath = Join-Path $evidence 'out-of-contract-runs/20260915T232229/ledger-reconciliation.json'
if ((Get-FileHash $previousPath).Hash -ne $ledger.source_sha256) { throw 'Prior reconciliation differs from the ledger.' }
$cases = @($orders.GetEnumerator() | ForEach-Object {
    $operation = 'api.set:Autodesk.Revit.DB.' + $_.Key
    $entry = $ledger.entries | Where-Object operation -eq $operation
    if (!$entry -or $entry.state -ne 'same_value_only') { throw "Case is not same-value-only: $operation" }
    [ordered]@{ property=$_.Key; operation=$operation; models=@($_.Value) }
})
$manifest = [ordered]@{
    prepared_at=[DateTime]::UtcNow.ToString('o'); revision=(git -C $root rev-parse HEAD)
    ledger_sha256=(Get-FileHash $ledgerPath).Hash; api_sha256=$source.api_sha256
    classification_sha256=$source.classification_sha256
    preregistration_sha256=(Get-FileHash (Join-Path $evidence 'project-architecture-preregistration.md')).Hash
    snapshot_amendment_sha256=$source.snapshot_amendment_sha256
    previous_reconciliation=[ordered]@{ path='out-of-contract-runs/20260915T232229/ledger-reconciliation.json'; sha256=(Get-FileHash $previousPath).Hash }
    expected_yield_min=0; expected_yield_max=4; test_class='ProjectArchitectureBatch'
    campaign_prefix='changed-value-project-architecture'
    source_manifests=@([ordered]@{ path='documented-fourth-manifest.json'; sha256=(Get-FileHash $sourcePath).Hash })
    pilot_model=$model.name; models=@($model); cases=$cases
    baseline_entries=@($ledger.entries | Where-Object operation -in $cases.operation)
}
$manifest | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $target -Encoding utf8
"Frozen: $($cases.Count) project-architecture cases."
