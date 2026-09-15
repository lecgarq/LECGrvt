$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$evidence = Join-Path $root 'docs/review/setter-validation-gate'
$target = Join-Path $evidence 'documented-next-retry-manifest.json'
if (Test-Path -LiteralPath $target) { throw 'Documented-next retry manifest already frozen.' }

$sourcePath = Join-Path $evidence 'documented-next-manifest.json'
$source = Get-Content $sourcePath -Raw | ConvertFrom-Json
$architectureProject = 'LECG_RVT_DISEÑO (ANTEPROYECTO).rvt'
$cases = @($source.cases | ForEach-Object {
    $models = if ($_.property -eq 'Electrical.WireType.MaxSize') { @($_.models) }
        else { @($_.models | Where-Object { $_ -ne $architectureProject }) }
    [ordered]@{ property=$_.property; operation=$_.operation; models=$models }
})
$manifest = [ordered]@{
    prepared_at=[DateTime]::UtcNow.ToString('o'); revision=(git -C $root rev-parse HEAD)
    ledger_sha256=$source.ledger_sha256; api_sha256=$source.api_sha256
    classification_sha256=$source.classification_sha256
    preregistration_sha256=(Get-FileHash (Join-Path $evidence 'documented-next-retry-preregistration.md')).Hash
    snapshot_amendment_sha256=$source.snapshot_amendment_sha256
    previous_reconciliation_sha256=$source.previous_reconciliation_sha256
    source_manifests=@([ordered]@{ path='documented-next-manifest.json'; sha256=(Get-FileHash $sourcePath).Hash })
    pilot_model=$source.pilot_model; models=$source.models; cases=$cases
    baseline_entries=$source.baseline_entries
}
$manifest | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $target -Encoding utf8
"Frozen: $($cases.Count) documented-next retry cases."
