$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$evidence = Join-Path $root 'docs/review/setter-validation-gate'
$target = Join-Path $evidence 'project-type-presence-manifest.json'
if (Test-Path -LiteralPath $target) { throw 'Project type-presence manifest already frozen.' }

$sourcePath = Join-Path $evidence 'documented-fourth-manifest.json'
$source = Get-Content $sourcePath -Raw | ConvertFrom-Json
$names = @('LECG_RVT_DISEÑO (ANTEPROYECTO).rvt','LECG_RVT_CONTEXTO.rvt','LECG_RVT_ESTRUCTURAL.rvt','LECG_RVT_MEP.rvt')
$models = @($names | ForEach-Object { $name=$_; $match=@($source.models|Where-Object name -eq $name); if($match.Count -ne 1){throw "Fixture is not unique: $name"}; $match[0] })
$inventoryPath = Join-Path $root 'outputs/validation-expansion/setter-inventory.json'
$inventory = Get-Content $inventoryPath -Raw | ConvertFrom-Json
if($inventory.Count -ne 805){throw 'Setter inventory denominator changed.'}
$manifest=[ordered]@{
    prepared_at=[DateTime]::UtcNow.ToString('o');revision=(git -C $root rev-parse HEAD)
    api_sha256=$source.api_sha256;classification_sha256=$source.classification_sha256
    preregistration_sha256=(Get-FileHash (Join-Path $evidence 'project-type-presence-preregistration.md')).Hash
    snapshot_amendment_sha256=$source.snapshot_amendment_sha256
    setter_inventory_sha256=(Get-FileHash $inventoryPath).Hash
    setter_operation_count=$inventory.Count
    declaring_type_count=@($inventory.declaring_type|Sort-Object -Unique).Count
    test_class='ProjectTypePresenceBatch';campaign_prefix='project-type-presence'
    source_manifests=@([ordered]@{path='documented-fourth-manifest.json';sha256=(Get-FileHash $sourcePath).Hash})
    pilot_model=$models[0].name;models=$models
}
$manifest|ConvertTo-Json -Depth 12|Set-Content -LiteralPath $target -Encoding utf8
"Frozen: $($manifest.declaring_type_count) declaring types across $($models.Count) models."
