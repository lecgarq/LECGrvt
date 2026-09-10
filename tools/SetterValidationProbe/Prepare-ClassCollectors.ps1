$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$evidence = Join-Path $root 'docs/review/setter-validation-gate'
$target = Join-Path $evidence 'class-collector-manifest.json'
if (Test-Path -LiteralPath $target) { throw 'Class-collector manifest already frozen.' }

$baseline = Get-Content (Join-Path $evidence 'dedicated-writable-manifest.json') -Raw | ConvertFrom-Json
$classificationPath = Join-Path $evidence 'elementid-classification.csv'
$ledgerPath = Join-Path $root 'RevitCopilot/Agent/Knowledge/revit2026-validation.json'
$preregistrationPath = Join-Path $evidence 'class-collector-preregistration.md'
$cases = @(Import-Csv $classificationPath | Where-Object bucket -eq 'collector_by_class' | ForEach-Object {
    [ordered]@{
        property = $_.property
        selection_rule = $_.selection_rule
        models = @($baseline.models | ForEach-Object name)
    }
})
if ($cases.Count -ne 18) { throw "Expected exactly 18 class-collector cases; found $($cases.Count)." }

$manifest = [ordered]@{
    prepared_at = [DateTime]::UtcNow.ToString('o')
    revision = (git -C $root rev-parse HEAD)
    ledger_sha256 = (Get-FileHash -LiteralPath $ledgerPath).Hash
    api_sha256 = (Get-FileHash -LiteralPath 'C:\Program Files\Autodesk\Revit 2026\RevitAPI.dll').Hash
    classification_sha256 = (Get-FileHash -LiteralPath $classificationPath).Hash
    preregistration_sha256 = (Get-FileHash -LiteralPath $preregistrationPath).Hash
    snapshot_amendment_sha256 = (Get-FileHash -LiteralPath (Join-Path $evidence 'writable-snapshot-amendment.md')).Hash
    pilot_model = $baseline.models[0].name
    models = $baseline.models
    cases = $cases
}
$manifest | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $target -Encoding utf8
Write-Output "Frozen $($cases.Count) class-collector cases at revision $($manifest.revision): $target"
