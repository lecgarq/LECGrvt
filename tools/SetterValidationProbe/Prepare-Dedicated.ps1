$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$out = Join-Path $root 'docs/review/setter-validation-gate'
$path = Join-Path $out 'dedicated-manifest.json'
if (Test-Path $path) { throw 'Dedicated preregistration is already frozen.' }
$old = Get-Content (Join-Path $out 'pilot-manifest.json') -Raw | ConvertFrom-Json
$history = Get-Content $old.baseline_path -Raw | ConvertFrom-Json
if ((Get-FileHash $old.baseline_path).Hash -ne $old.baseline_sha256) { throw 'Historical receipt changed.' }
$ledgerPath = Join-Path $root 'RevitCopilot/Agent/Knowledge/revit2026-validation.json'
$ledger = Get-Content $ledgerPath -Raw | ConvertFrom-Json
$cases = @(Import-Csv (Join-Path $out 'elementid-classification.csv') | Where-Object bucket -eq dedicated_collection | ForEach-Object {
    $operation = 'api.set:Autodesk.Revit.DB.' + $_.property
    $reachable = @($history.operations | Where-Object { $_.operation -eq $operation -and $_.status -eq 'roundtrip_only' } | ForEach-Object model)
    $models = @($old.models | Where-Object {$_.name -in $reachable} | Sort-Object @{Expression={(Get-Item -LiteralPath $_.path).Length}},name | ForEach-Object name)
    if (!$models.Count) { throw "No historical reachable model for $operation" }
    [ordered]@{ property=$_.property; operation=$operation; models=$models }
})
if ($cases.Count -ne 14) { throw 'Expected exactly 14 dedicated cases.' }
foreach($model in $old.models) { if ((Get-FileHash $model.path).Hash -ne $model.sha256) { throw "Model changed: $($model.name)" } }
$manifest = [ordered]@{ prepared_at=[DateTime]::UtcNow.ToString('o'); revision=(git -C $root rev-parse HEAD); ledger_sha256=(Get-FileHash $ledgerPath).Hash;
    baseline_path=$old.baseline_path; baseline_sha256=$old.baseline_sha256; api_sha256=$old.api_sha256; api_xml_sha256=$old.api_xml_sha256;
    classification_sha256=$old.classification_sha256; preregistration_sha256=(Get-FileHash (Join-Path $out 'dedicated-preregistration.md')).Hash;
    models=$old.models; pilot_model=$old.pilot_model; cases=$cases; baseline_entries=@($ledger.entries | Where-Object {$_.operation -in $cases.operation -or $_.operation -eq 'api.set:Autodesk.Revit.DB.ViewSheetSet.IsAutomatic'});
    previous_pilot_run='20260909T051316-96a20f7cfb034e4fae35e1fa7d1e3531'; estimate_before=@(18,34) }
$manifest | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $path -Encoding utf8
"Frozen: $($cases.Count) cases; unchanged estimate 18–34/70."
