$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$evidence = Join-Path $root 'docs/review/setter-validation-gate'
$target = Join-Path $evidence 'project-corpus-manifest.json'
if (Test-Path -LiteralPath $target) { throw 'Project corpus manifest already frozen.' }
$fixtureRoot = 'C:\Users\LECG Arquitectura\DC\ACCDocs\Arq. Luis Eduardo Cortés\CASA EUCALIPTO\Project Files'
$spec = @(
    @{ discipline='architecture'; name='LECG_RVT_DISEÑO (ANTEPROYECTO).rvt'; relative_path='01-WIP\01-ARQ\LECG_RVT_DISEÑO (ANTEPROYECTO).rvt' },
    @{ discipline='topography'; name='LECG_RVT_CONTEXTO.rvt'; relative_path='01-WIP\00-CIV\LECG_RVT_CONTEXTO.rvt' },
    @{ discipline='structure'; name='LECG_RVT_ESTRUCTURAL.rvt'; relative_path='02-SHARED\02-EST\LECG_RVT_ESTRUCTURAL.rvt' },
    @{ discipline='mep'; name='LECG_RVT_MEP.rvt'; relative_path='02-SHARED\03-MEP\LECG_RVT_MEP.rvt' }
)
$models = @($spec | ForEach-Object {
    $path = [IO.Path]::GetFullPath((Join-Path $fixtureRoot $_.relative_path))
    if (!(Test-Path -LiteralPath $path -PathType Leaf) -or [IO.Path]::GetExtension($path) -ne '.rvt') { throw "Missing RVT: $path" }
    $file = Get-Item -LiteralPath $path
    [ordered]@{ discipline=$_.discipline; name=$_.name; relative_path=$_.relative_path;
        sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash; bytes=$file.Length;
        modified_utc=$file.LastWriteTimeUtc.ToString('o') }
})
if ($models.Count -ne 4 -or @($models.sha256 | Sort-Object -Unique).Count -ne 4) { throw 'Require four distinct project fixtures.' }
$byDiscipline = @{}; foreach ($model in $models) { $byDiscipline[$model.discipline] = $model.name }
$orders = [ordered]@{
    'Analysis.MassLevelData.ConceptualConstructionIsByEnergyData'=@('architecture','topography','structure','mep')
    'ImageInstance.EnableSnaps'=@('architecture','topography','structure','mep')
    'Electrical.CircuitNamingSchemeSettings.CircuitNamingSchemeId'=@('mep','architecture','structure','topography')
    'Electrical.ElectricalSystem.CircuitConnectionType'=@('mep','architecture','structure','topography')
    'Part.OriginalCategoryId'=@('architecture','topography','structure','mep')
    'Structure.StructuralConnectionHandler.ApprovalTypeId'=@('structure','architecture','mep','topography')
    'View.AnalysisDisplayStyleId'=@('architecture','topography','structure','mep')
    'ViewSheet.SheetCollectionId'=@('architecture','structure','mep','topography')
    'ViewSheetSet.IsAutomatic'=@('architecture','structure','mep','topography')
    'ViewSheetSet.SheetOrganizationId'=@('architecture','structure','mep','topography')
    'ViewSheetSet.ViewOrganizationId'=@('architecture','structure','mep','topography')
}
if ($orders.Count -ne 11) { throw 'Expected exactly 11 project cases.' }
$ledgerPath = Join-Path $root 'RevitCopilot/Agent/Knowledge/revit2026-validation.json'
$ledger = Get-Content $ledgerPath -Raw | ConvertFrom-Json
$cases = @($orders.GetEnumerator() | ForEach-Object {
    $operation = 'api.set:Autodesk.Revit.DB.' + $_.Key
    $entry = $ledger.entries | Where-Object operation -eq $operation
    if (!$entry -or $entry.state -eq 'changed_value_tested') { throw "Case is not unresolved: $operation" }
    [ordered]@{ property=$_.Key; operation=$operation; models=@($_.Value | ForEach-Object {$byDiscipline[$_]}) }
})
$base = Get-Content (Join-Path $evidence 'non-element-manifest.json') -Raw | ConvertFrom-Json
$manifest = [ordered]@{
    prepared_at=[DateTime]::UtcNow.ToString('o'); revision=(git -C $root rev-parse HEAD)
    ledger_sha256=(Get-FileHash $ledgerPath).Hash; api_sha256=$base.api_sha256
    classification_sha256=$base.classification_sha256
    preregistration_sha256=(Get-FileHash (Join-Path $evidence 'project-corpus-preregistration.md')).Hash
    snapshot_amendment_sha256=$base.snapshot_amendment_sha256
    prior_non_element_manifest_sha256=(Get-FileHash (Join-Path $evidence 'non-element-manifest.json')).Hash
    prior_non_element_run='20260911T020232'; pilot_model=$models[0].name; models=$models; cases=$cases
    baseline_entries=@($ledger.entries | Where-Object operation -in $cases.operation)
}
$manifest | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $target -Encoding utf8
"Frozen: $($cases.Count) cases, $($models.Count) user-authorized project fixtures."
