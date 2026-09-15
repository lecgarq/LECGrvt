$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$evidence = Join-Path $root 'docs/review/setter-validation-gate'
$target = Join-Path $evidence 'project-mep-wire-manifest.json'
if (Test-Path -LiteralPath $target) { throw 'Project-MEP-wire manifest already frozen.' }

$sourcePath = Join-Path $evidence 'documented-fourth-manifest.json'
$source = Get-Content $sourcePath -Raw | ConvertFrom-Json
$mep = @($source.models | Where-Object name -eq 'LECG_RVT_MEP.rvt')
if ($mep.Count -ne 1) { throw 'MEP fixture is not uniquely defined.' }
$model = $mep[0]
$ledgerPath = Join-Path $root 'RevitCopilot/Agent/Knowledge/revit2026-validation.json'
$ledger = Get-Content $ledgerPath -Raw | ConvertFrom-Json
$operation = 'api.set:Autodesk.Revit.DB.Electrical.WireType.MaxSize'
$entry = $ledger.entries | Where-Object operation -eq $operation
if (!$entry -or $entry.state -ne 'same_value_only') { throw 'WireType.MaxSize is not same-value-only.' }
$previousPath = Join-Path $evidence 'project-architecture-runs/20260915T232928/ledger-reconciliation.json'
if ((Get-FileHash $previousPath).Hash -ne $ledger.source_sha256) { throw 'Prior reconciliation differs from the ledger.' }
$manifest = [ordered]@{
    prepared_at=[DateTime]::UtcNow.ToString('o'); revision=(git -C $root rev-parse HEAD)
    ledger_sha256=(Get-FileHash $ledgerPath).Hash; api_sha256=$source.api_sha256
    classification_sha256=$source.classification_sha256
    preregistration_sha256=(Get-FileHash (Join-Path $evidence 'project-mep-wire-preregistration.md')).Hash
    snapshot_amendment_sha256=$source.snapshot_amendment_sha256
    previous_reconciliation=[ordered]@{ path='project-architecture-runs/20260915T232928/ledger-reconciliation.json'; sha256=(Get-FileHash $previousPath).Hash }
    expected_yield_min=0; expected_yield_max=1; test_class='ProjectMepWireBatch'
    campaign_prefix='changed-value-project-mep-wire'
    source_manifests=@([ordered]@{ path='documented-fourth-manifest.json'; sha256=(Get-FileHash $sourcePath).Hash })
    pilot_model=$model.name; models=@($model)
    cases=@([ordered]@{ property='Electrical.WireType.MaxSize'; operation=$operation; models=@($model.name) })
    baseline_entries=@($entry)
}
$manifest | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $target -Encoding utf8
'Frozen: 1 project-MEP-wire case.'
