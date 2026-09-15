$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$evidence = Join-Path $root 'docs/review/setter-validation-gate'
$target = Join-Path $evidence 'documented-retry-manifest.json'
if (Test-Path -LiteralPath $target) { throw 'Documented-retry manifest already frozen.' }
$source = Get-Content (Join-Path $evidence 'documented-constraint-manifest.json') -Raw | ConvertFrom-Json
$ledgerPath = Join-Path $root 'RevitCopilot/Agent/Knowledge/revit2026-validation.json'
$ledger = Get-Content $ledgerPath -Raw | ConvertFrom-Json
$bim = 'BIM_Projekt_Golden_Nugget-Architektur_und_Ingenieurbau.rvt'
$structural = @('Snowdon Towers Sample Structural.rvt','サンプル構造.rvt',$bim)
$orders = [ordered]@{
    'Analysis.EnergyAnalysisDetailModel.ExportCategory' = @($bim)
    'Analysis.EnergyDataSettings.ExportCategory' = @($bim)
    'Structure.LoadCase.SubcategoryId' = $structural
}
$cases = @($orders.GetEnumerator() | ForEach-Object {
    $operation = 'api.set:Autodesk.Revit.DB.' + $_.Key
    $entry = $ledger.entries | Where-Object operation -eq $operation
    if (!$entry -or $entry.state -ne 'same_value_only') { throw "Case is not same-value-only: $operation" }
    [ordered]@{ property=$_.Key; operation=$operation; models=@($_.Value) }
})
$failedRun = Join-Path $evidence 'documented-constraint-runs/20260915T212311'
$failedTrx = Join-Path $evidence 'documented-20260915T212228.trx'
if (!(Test-Path $failedRun) -or !(Test-Path $failedTrx)) { throw 'Failed source run is missing.' }
$manifest = [ordered]@{
    prepared_at=[DateTime]::UtcNow.ToString('o'); revision=(git -C $root rev-parse HEAD)
    ledger_sha256=(Get-FileHash $ledgerPath).Hash; api_sha256=$source.api_sha256
    classification_sha256=$source.classification_sha256
    preregistration_sha256=(Get-FileHash (Join-Path $evidence 'documented-retry-preregistration.md')).Hash
    snapshot_amendment_sha256=$source.snapshot_amendment_sha256
    parent_manifest_sha256=(Get-FileHash (Join-Path $evidence 'documented-constraint-manifest.json')).Hash
    failed_run='20260915T212311'; failed_trx_sha256=(Get-FileHash $failedTrx).Hash
    pilot_model=$bim; models=$source.models; cases=$cases
    baseline_entries=@($ledger.entries | Where-Object operation -in $cases.operation)
}
$manifest | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $target -Encoding utf8
"Frozen: $($cases.Count) retry cases."
