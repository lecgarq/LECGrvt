$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$evidence = Join-Path $root 'docs/review/setter-validation-gate'
$target = Join-Path $evidence 'project-native-read-expansion-manifest.json'
if (Test-Path -LiteralPath $target) { throw 'Project native-read expansion manifest already frozen.' }
$sourcePath = Join-Path $evidence 'project-native-read-manifest.json'
$source = Get-Content $sourcePath -Raw | ConvertFrom-Json
$copilotPath = Join-Path $root 'RevitCopilot/bin/x64/Debug/net10.0-windows/RevitCopilot.dll'
if (!(Test-Path -LiteralPath $copilotPath)) { throw 'Copilot binary is unavailable.' }
$manifest = [ordered]@{
    prepared_at=[DateTime]::UtcNow.ToString('o');revision=(git -C $root rev-parse HEAD)
    api_sha256=$source.api_sha256;classification_sha256=$source.classification_sha256
    preregistration_sha256=(Get-FileHash (Join-Path $evidence 'project-native-read-expansion-preregistration.md')).Hash
    snapshot_amendment_sha256=$source.snapshot_amendment_sha256
    copilot_assembly_sha256=(Get-FileHash $copilotPath).Hash;read_operation_count=37
    test_class='ProjectNativeReadBatch';campaign_prefix='project-native-read-expansion'
    source_manifests=@([ordered]@{path='project-native-read-manifest.json';sha256=(Get-FileHash $sourcePath).Hash})
    pilot_model=$source.pilot_model;models=$source.models
}
$manifest | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $target -Encoding utf8
"Frozen: $($manifest.read_operation_count) native reads across $($manifest.models.Count) models."
