$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$evidence = Join-Path $root 'docs/review/setter-validation-gate'
$target = Join-Path $evidence 'documented-constraint-manifest.json'
if (Test-Path -LiteralPath $target) { throw 'Documented-constraint manifest already frozen.' }

$autodesk = Get-Content (Join-Path $evidence 'non-element-manifest.json') -Raw | ConvertFrom-Json
$project = Get-Content (Join-Path $evidence 'project-corpus-manifest.json') -Raw | ConvertFrom-Json
$models = @($autodesk.models) + @($project.models)
if ($models.Count -ne 16 -or @($models.sha256 | Sort-Object -Unique).Count -ne 16) {
    throw 'Expected 16 distinct frozen source models.'
}
$a = @($autodesk.models.name)
$p = @($project.models.name)
$orders = [ordered]@{
    'Analysis.EnergyAnalysisDetailModel.ExportCategory' = @($a[0],$a[2],$a[8],$a[9],$a[1],$a[3],$a[4],$a[5],$a[6],$a[7],$a[10],$a[11],$p[0],$p[1],$p[2],$p[3])
    'Analysis.EnergyDataSettings.ExportCategory' = @($a[0],$a[2],$a[8],$a[9],$a[1],$a[3],$a[4],$a[5],$a[6],$a[7],$a[10],$a[11],$p[0],$p[1],$p[2],$p[3])
    'AssemblyInstance.NamingCategoryId' = @($a[8],$a[10],$a[0],$p[2],$a[2],$a[9],$a[1],$a[3],$a[4],$a[5],$a[6],$a[7],$a[11],$p[0],$p[1],$p[3])
    'Structure.LoadCase.SubcategoryId' = @($a[8],$a[10],$a[0],$p[2],$a[2],$a[9],$a[1],$a[3],$a[4],$a[5],$a[6],$a[7],$a[11],$p[0],$p[1],$p[3])
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
    ledger_sha256=(Get-FileHash $ledgerPath).Hash; api_sha256=$project.api_sha256
    classification_sha256=$project.classification_sha256
    preregistration_sha256=(Get-FileHash (Join-Path $evidence 'documented-constraint-preregistration.md')).Hash
    snapshot_amendment_sha256=$project.snapshot_amendment_sha256
    previous_reconciliation_sha256=$ledger.source_sha256
    source_manifests=@(
        [ordered]@{ path='non-element-manifest.json'; sha256=(Get-FileHash (Join-Path $evidence 'non-element-manifest.json')).Hash },
        [ordered]@{ path='project-corpus-manifest.json'; sha256=(Get-FileHash (Join-Path $evidence 'project-corpus-manifest.json')).Hash }
    )
    pilot_model=$a[0]; models=$models; cases=$cases
    baseline_entries=@($ledger.entries | Where-Object operation -in $cases.operation)
}
$manifest | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $target -Encoding utf8
"Frozen: $($cases.Count) documented cases, $($models.Count) source models."
