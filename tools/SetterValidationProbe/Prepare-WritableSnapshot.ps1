$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$evidence = Join-Path $root 'docs/review/setter-validation-gate'
$target = Join-Path $evidence 'dedicated-writable-manifest.json'
if (Test-Path $target) { throw 'Writable snapshot manifest already frozen.' }
$original = Join-Path $evidence 'dedicated-manifest.json'
$probe = Join-Path $evidence 'restoration-runs/20260909T060232-b9ce4944bde8496d8533fbf9e6c154c3/Analysis.MassLevelData.ConceptualConstructionId/BIM_Projekt_Golden_Nugget-Architektur_und_Ingenieurbau/receipt.json'
$receipt = Get-Content $probe -Raw | ConvertFrom-Json
if (!$receipt.parameter_probe.IsReadOnly -or $receipt.parameter_probe.element_id -ne 1462965 -or
    $receipt.parameter_probe.parameter_id -ne -1006490 -or !$receipt.cleanup_verified -or $receipt.setter_attempted) {
    throw 'Conditional amendment not established by the live read-only probe.'
}
$manifest = Get-Content $original -Raw | ConvertFrom-Json -AsHashtable
if ((Get-FileHash (Join-Path $root 'RevitCopilot/Agent/Knowledge/revit2026-validation.json')).Hash -ne $manifest.ledger_sha256) { throw 'Ledger changed.' }
foreach ($model in $manifest.models) { if ((Get-FileHash $model.path).Hash -ne $model.sha256) { throw 'Source model changed.' } }
$manifest.prepared_at = [DateTime]::UtcNow.ToString('o')
$manifest.revision = git -C $root rev-parse HEAD
$manifest.original_manifest_sha256 = (Get-FileHash $original).Hash
$manifest.snapshot_amendment_sha256 = (Get-FileHash (Join-Path $evidence 'writable-snapshot-amendment.md')).Hash
$manifest.readonly_probe = @{ path=$probe; sha256=(Get-FileHash $probe).Hash }
$manifest | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $target -Encoding utf8
'Frozen all 14 cases, unchanged model order and estimate; writable snapshot amendment only.'
