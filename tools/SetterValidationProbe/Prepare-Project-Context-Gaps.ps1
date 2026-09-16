$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$evidence = Join-Path $root 'docs/review/setter-validation-gate'
$target = Join-Path $evidence 'project-context-gaps-manifest.json'
if (Test-Path -LiteralPath $target) { throw 'Project context-gaps manifest already frozen.' }

$sourcePath = Join-Path $evidence 'documented-fourth-manifest.json'
$source = Get-Content $sourcePath -Raw | ConvertFrom-Json
$names = @('LECG_RVT_DISEÑO (ANTEPROYECTO).rvt','LECG_RVT_CONTEXTO.rvt','LECG_RVT_ESTRUCTURAL.rvt','LECG_RVT_MEP.rvt')
$models = @($names | ForEach-Object { $name=$_; $match=@($source.models|Where-Object name -eq $name); if($match.Count -ne 1){throw "Fixture is not unique: $name"}; $match[0] })
$properties = @('DisplacementPath.AncestorIdx','TableView.TargetId')
$ledgerPath = Join-Path $root 'RevitCopilot/Agent/Knowledge/revit2026-validation.json'
$ledger = Get-Content $ledgerPath -Raw | ConvertFrom-Json
$cases = @($properties | ForEach-Object {
    $operation='api.set:Autodesk.Revit.DB.'+$_
    $entry=$ledger.entries|Where-Object operation -eq $operation
    if(!$entry -or $entry.state -ne 'same_value_only'){throw "Case is not same-value-only: $operation"}
    [ordered]@{property=$_;operation=$operation;models=@($names)}
})
$previousPath = Join-Path $evidence 'project-mep-circuit-runs/20260916T001624/ledger-reconciliation.json'
if((Get-FileHash $previousPath).Hash -ne $ledger.source_sha256){throw 'Prior reconciliation differs from the ledger.'}
$manifest=[ordered]@{
    prepared_at=[DateTime]::UtcNow.ToString('o');revision=(git -C $root rev-parse HEAD)
    ledger_sha256=(Get-FileHash $ledgerPath).Hash;api_sha256=$source.api_sha256
    classification_sha256=$source.classification_sha256
    preregistration_sha256=(Get-FileHash (Join-Path $evidence 'project-context-gaps-preregistration.md')).Hash
    snapshot_amendment_sha256=$source.snapshot_amendment_sha256
    previous_reconciliation=[ordered]@{path='project-mep-circuit-runs/20260916T001624/ledger-reconciliation.json';sha256=(Get-FileHash $previousPath).Hash}
    expected_yield_min=0;expected_yield_max=0;test_class='ProjectContextGapBatch'
    campaign_prefix='classify-project-context-gaps'
    source_manifests=@([ordered]@{path='documented-fourth-manifest.json';sha256=(Get-FileHash $sourcePath).Hash})
    pilot_model=$models[0].name;models=$models;cases=$cases
    baseline_entries=@($ledger.entries|Where-Object operation -in $cases.operation)
}
$manifest|ConvertTo-Json -Depth 12|Set-Content -LiteralPath $target -Encoding utf8
"Frozen: $($cases.Count) context-gap cases across $($models.Count) models."
