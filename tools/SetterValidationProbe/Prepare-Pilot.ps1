$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$out = Join-Path $root 'docs/review/setter-validation-gate'
$surface = Get-Content (Join-Path $out 'setter-surface.json') -Raw | ConvertFrom-Json
$rows = @(Import-Csv (Join-Path $out 'elementid-classification.csv'))
$expected = @($surface.entries | Where-Object { $_.phase1 -and $_.property_type -eq 'Autodesk.Revit.DB.ElementId' } | ForEach-Object operation | Sort-Object)
$actual = @($rows | ForEach-Object { 'api.set:Autodesk.Revit.DB.' + $_.property } | Sort-Object)
if ($rows.Count -ne 42 -or (Compare-Object $actual $expected)) { throw 'Classification is not the exact 42-operation set.' }
$xmlPath = 'C:\Program Files\Autodesk\Revit 2026\RevitAPI.xml'
if ((Get-FileHash $xmlPath).Hash -ne $surface.api_xml_sha256) { throw 'API documentation changed.' }
$xml = [xml](Get-Content $xmlPath -Raw)
$evidence = @(foreach ($row in $rows) {
    $members = @($xml.doc.members.member | Where-Object { $_.name -eq $row.evidence_member -or $_.name.StartsWith($row.evidence_member + '(') })
    if (!$members.Count) { throw "Missing API evidence: $($row.evidence_member)" }
    [ordered]@{ operation = 'api.set:Autodesk.Revit.DB.' + $row.property; bucket = $row.bucket; selection_rule = $row.selection_rule; evidence = @($members | ForEach-Object OuterXml) }
})
$baseline = Join-Path $root 'RevitCopilot/Tests/bin/x64/Release/net10.0-windows/benchmarks/20260905_203238/benchmark-results.json'
$ledgerPath = Join-Path $root 'RevitCopilot/Agent/Knowledge/revit2026-validation.json'
$ledger = Get-Content $ledgerPath -Raw | ConvertFrom-Json
if ((Get-FileHash $baseline).Hash -ne $ledger.source_sha256) { throw 'Historical receipt does not match ledger.' }
$history = Get-Content $baseline -Raw | ConvertFrom-Json
if ($history.status -ne 'completed' -or $history.error) { throw 'Historical corpus incomplete.' }
$models = @(foreach ($model in $history.source_hashes.PSObject.Properties) {
    $path = Join-Path 'C:\Program Files\Autodesk\Revit 2026\Samples' $model.Name
    if ((Get-FileHash $path).Hash -ne $model.Value) { throw "Sample changed: $path" }
    [ordered]@{ name = $model.Name; path = $path; sha256 = $model.Value }
})
if ($models.Count -ne 12) { throw 'Expected 12 named models.' }
$manifest = [ordered]@{ prepared_at = [DateTime]::UtcNow.ToString('o'); revision = (git -C $root rev-parse HEAD); ledger_sha256 = (Get-FileHash $ledgerPath).Hash; baseline_path = $baseline; baseline_sha256 = (Get-FileHash $baseline).Hash; api_sha256 = $surface.api_sha256; api_xml_sha256 = $surface.api_xml_sha256; classification_sha256 = (Get-FileHash (Join-Path $out 'elementid-classification.csv')).Hash; preregistration_sha256 = (Get-FileHash (Join-Path $out 'pilot-preregistration.md')).Hash; models = $models; pilot_model = 'Snowdon Towers Sample Electrical.rvt'; classification_counts = @($rows | Group-Object bucket | Select-Object Name,Count); classifications = $evidence }
$path = Join-Path $out 'pilot-manifest.json'
if (Test-Path $path) { throw 'Preregistration already frozen; do not overwrite it.' }
$manifest | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $path -Encoding utf8
$manifest.classification_counts | Format-Table
