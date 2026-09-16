$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$evidence = Join-Path $root 'docs/review/setter-validation-gate'
$target = Join-Path $evidence 'project-accessor-read-manifest.json'
if (Test-Path -LiteralPath $target) { throw 'Project accessor-read manifest already frozen.' }

$sourcePath = Join-Path $evidence 'project-accessor-presence-manifest.json'
$source = Get-Content $sourcePath -Raw | ConvertFrom-Json
$inventoryPath = Join-Path $root 'outputs/validation-expansion/accessor-inventory.json'
$copilotPath = Join-Path $root 'RevitCopilot/bin/x64/Debug/net10.0-windows/RevitCopilot.dll'
$inventory = Get-Content $inventoryPath -Raw | ConvertFrom-Json
$reads = @($inventory | Where-Object operation -Like 'api.get:*')
if($inventory.Count -ne 2216 -or $reads.Count -ne 1411 -or !(Test-Path -LiteralPath $copilotPath)){throw 'Accessor inventory or Copilot binary is unavailable.'}
$manifest=[ordered]@{
    prepared_at=[DateTime]::UtcNow.ToString('o');revision=(git -C $root rev-parse HEAD)
    api_sha256=$source.api_sha256;classification_sha256=$source.classification_sha256
    preregistration_sha256=(Get-FileHash (Join-Path $evidence 'project-accessor-read-preregistration.md')).Hash
    snapshot_amendment_sha256=$source.snapshot_amendment_sha256
    inventory_path='outputs/validation-expansion/accessor-inventory.json';inventory_sha256=(Get-FileHash $inventoryPath).Hash
    copilot_assembly_sha256=(Get-FileHash $copilotPath).Hash
    read_operation_count=$reads.Count;test_class='ProjectAccessorReadBatch';campaign_prefix='project-accessor-read'
    source_manifests=@([ordered]@{path='project-accessor-presence-manifest.json';sha256=(Get-FileHash $sourcePath).Hash})
    pilot_model=$source.pilot_model;models=$source.models
}
$manifest|ConvertTo-Json -Depth 12|Set-Content -LiteralPath $target -Encoding utf8
"Frozen: $($manifest.read_operation_count) getters across $($manifest.models.Count) models."
