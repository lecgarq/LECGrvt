$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$evidence = Join-Path $root 'docs/review/setter-validation-gate'
$target = Join-Path $evidence 'project-gaps-manifest.json'
if (Test-Path -LiteralPath $target) { throw 'Project-gaps manifest already frozen.' }

$sourcePath = Join-Path $evidence 'documented-fourth-manifest.json'
$source = Get-Content $sourcePath -Raw | ConvertFrom-Json
$names = @('LECG_RVT_DISEÑO (ANTEPROYECTO).rvt','LECG_RVT_CONTEXTO.rvt','LECG_RVT_ESTRUCTURAL.rvt','LECG_RVT_MEP.rvt')
$models = @($source.models | Where-Object name -in $names)
if ($models.Count -ne 4) { throw 'The four project fixtures are not uniquely defined.' }
$byName = @{}; $models | ForEach-Object { $byName[$_.name] = $_ }
$a=$names[0]; $t=$names[1]; $s=$names[2]; $m=$names[3]
$orders = [ordered]@{
    'FloorType.StructuralMaterialId' = @($a,$t,$m)
    'Mechanical.MEPHiddenLineSettings.LineStyle' = @($a,$t,$s)
    'View.ViewPositionId' = @($a,$t)
}
$ledgerPath = Join-Path $root 'RevitCopilot/Agent/Knowledge/revit2026-validation.json'
$ledger = Get-Content $ledgerPath -Raw | ConvertFrom-Json
$previousPath = Join-Path $evidence 'project-mep-wire-runs/20260915T233831/ledger-reconciliation.json'
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
    preregistration_sha256=(Get-FileHash (Join-Path $evidence 'project-gaps-preregistration.md')).Hash
    snapshot_amendment_sha256=$source.snapshot_amendment_sha256
    previous_reconciliation=[ordered]@{ path='project-mep-wire-runs/20260915T233831/ledger-reconciliation.json'; sha256=(Get-FileHash $previousPath).Hash }
    expected_yield_min=0; expected_yield_max=3; test_class='ProjectGapBatch'
    campaign_prefix='changed-value-project-gaps'
    source_manifests=@([ordered]@{ path='documented-fourth-manifest.json'; sha256=(Get-FileHash $sourcePath).Hash })
    pilot_model=$a; models=$models; cases=$cases
    baseline_entries=@($ledger.entries | Where-Object operation -in $cases.operation)
}
$manifest | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $target -Encoding utf8
"Frozen: $($cases.Count) project-gap cases with 8 planned attempts."
