$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$evidence = Join-Path $root 'docs/review/setter-validation-gate'
$target = Join-Path $evidence 'documented-fourth-manifest.json'
if (Test-Path -LiteralPath $target) { throw 'Documented-fourth manifest already frozen.' }

$sourcePath = Join-Path $evidence 'documented-third-manifest.json'
$source = Get-Content $sourcePath -Raw | ConvertFrom-Json
$models = @($source.models)
$a = @($models.name)
$orders = [ordered]@{
    'View.AnalysisDisplayStyleId' = @($a[8],$a[10],$a[0],$a[14])
    'View.ViewPositionId' = @($a[0],$a[2],$a[8],$a[1],$a[14],$a[15])
    'ViewSheet.SheetCollectionId' = @($a[0],$a[2],$a[9],$a[14])
    'ViewSheetSet.IsAutomatic' = @($a[0],$a[2],$a[3],$a[1],$a[15])
    'ViewSheetSet.SheetOrganizationId' = @($a[0],$a[2],$a[3],$a[1],$a[15])
    'ViewSheetSet.ViewOrganizationId' = @($a[0],$a[2],$a[3],$a[1],$a[15])
}
$ledgerPath = Join-Path $root 'RevitCopilot/Agent/Knowledge/revit2026-validation.json'
$ledger = Get-Content $ledgerPath -Raw | ConvertFrom-Json
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
    preregistration_sha256=(Get-FileHash (Join-Path $evidence 'documented-fourth-preregistration.md')).Hash
    snapshot_amendment_sha256=$source.snapshot_amendment_sha256
    previous_reconciliation_sha256=$ledger.source_sha256
    expected_yield_min=1; expected_yield_max=6
    source_manifests=@([ordered]@{ path='documented-third-manifest.json'; sha256=(Get-FileHash $sourcePath).Hash })
    pilot_model=$a[0]; models=$models; cases=$cases
    baseline_entries=@($ledger.entries | Where-Object operation -in $cases.operation)
}
$manifest | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $target -Encoding utf8
"Frozen: $($cases.Count) documented-fourth cases."
