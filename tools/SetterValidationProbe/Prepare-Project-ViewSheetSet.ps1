$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$evidence = Join-Path $root 'docs/review/setter-validation-gate'
$target = Join-Path $evidence 'project-view-sheet-set-manifest.json'
if (Test-Path -LiteralPath $target) { throw 'Project ViewSheetSet manifest already frozen.' }

$sourcePath = Join-Path $evidence 'documented-fourth-manifest.json'
$source = Get-Content $sourcePath -Raw | ConvertFrom-Json
$architecture = @($source.models | Where-Object name -eq 'LECG_RVT_DISEÑO (ANTEPROYECTO).rvt')
if ($architecture.Count -ne 1) { throw 'Architecture fixture is not uniquely defined.' }
$model = $architecture[0]
$properties = @('ViewSheetSet.IsAutomatic','ViewSheetSet.SheetOrganizationId','ViewSheetSet.ViewOrganizationId')
$ledgerPath = Join-Path $root 'RevitCopilot/Agent/Knowledge/revit2026-validation.json'
$ledger = Get-Content $ledgerPath -Raw | ConvertFrom-Json
$cases = @($properties | ForEach-Object {
    $operation = 'api.set:Autodesk.Revit.DB.' + $_
    $entry = $ledger.entries | Where-Object operation -eq $operation
    if (!$entry -or $entry.state -ne 'same_value_only') { throw "Case is not same-value-only: $operation" }
    [ordered]@{ property=$_; operation=$operation; models=@($model.name) }
})
$manifest = [ordered]@{
    prepared_at=[DateTime]::UtcNow.ToString('o'); revision=(git -C $root rev-parse HEAD)
    ledger_sha256=(Get-FileHash $ledgerPath).Hash; api_sha256=$source.api_sha256
    classification_sha256=$source.classification_sha256
    preregistration_sha256=(Get-FileHash (Join-Path $evidence 'project-view-sheet-set-preregistration.md')).Hash
    snapshot_amendment_sha256=$source.snapshot_amendment_sha256
    expected_yield_min=0; expected_yield_max=0; test_class='ProjectViewSheetSetBatch'
    campaign_prefix='classify-project-view-sheet-set-persistence'
    source_manifests=@([ordered]@{ path='documented-fourth-manifest.json'; sha256=(Get-FileHash $sourcePath).Hash })
    pilot_model=$model.name; models=@($model); cases=$cases
    baseline_entries=@($ledger.entries | Where-Object operation -in $cases.operation)
}
$manifest | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $target -Encoding utf8
"Frozen: $($cases.Count) ViewSheetSet persistence-classification cases."
