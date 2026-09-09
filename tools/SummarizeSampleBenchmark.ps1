param(
    [Parameter(Mandatory=$true)][string]$ResultPath,
    [string]$CampaignPath = 'C:\LECG\RevitAddins\LECG\RevitCopilot\Research\artifacts\campaign-v5'
)
$ErrorActionPreference = 'Stop'
$data = Get-Content -LiteralPath $ResultPath -Raw | ConvertFrom-Json
if ($data.status -ne 'completed' -or $data.error) { throw "Benchmark did not complete cleanly: $($data.status): $($data.error)" }
$out = Split-Path -Parent (Resolve-Path -LiteralPath $ResultPath).Path
$runtimeRows = @($data.operations | Where-Object { $_.operation -ne 'reviewed_fixture_suite' })
foreach ($suite in ($data.operations | Where-Object { $_.operation -eq 'reviewed_fixture_suite' -and $_.status -eq 'failed' })) {
    foreach ($check in $suite.completed_checks) {
        if ($check.test -match '^agent_(read|change):(.+)$' -and $check.passed) {
            $runtimeRows += [pscustomobject]@{model=$suite.model;operation=$Matches[2];status='passed'}
        }
    }
}
$byOperation = @{}
foreach ($group in ($runtimeRows | Group-Object operation)) {
    $byOperation[$group.Name] = @($group.Group)
}
function Coverage([string]$operation, [string]$kind) {
    $rows = @($byOperation[$operation]) | Where-Object { $null -ne $_ }
    $passed = @($rows | Where-Object status -eq 'passed')
    $failed = @($rows | Where-Object status -eq 'failed')
    $unsupported = @($rows | Where-Object status -eq 'unsupported')
    $changed = @($passed | Where-Object verification -like 'Changed-value*')
    $roundtrip = @($rows | Where-Object status -eq 'roundtrip_only')
    $rejected = @($rows | Where-Object status -eq 'context_rejected')
    $missing = @($rows | Where-Object status -eq 'missing_fixture')
    $outOfContract = @($rows | Where-Object status -eq 'out_of_contract')
    if ($outOfContract.Count -and $passed.Count) { throw "Contradictory out-of-contract and passed evidence for $operation" }
    $status = if ($outOfContract.Count) { 'out_of_contract' } elseif ($changed.Count) { 'changed_value_tested' } elseif ($passed.Count) { 'passed' } elseif ($failed.Count) { 'failed' } elseif ($roundtrip.Count) { 'same_value_only' } elseif ($rejected.Count) { 'context_rejected' } elseif ($missing.Count) { 'missing_fixture' } elseif ($unsupported.Count) { 'unsupported' } elseif ($kind -eq 'change') { 'awaiting_review' } else { 'unsupported' }
    [pscustomobject]@{operation=$operation; kind=$kind; status=$status; passed_models=$passed.Count; failed_models=$failed.Count; unsupported_models=$unsupported.Count}
}
$coverage = @(@($data.installed_catalog.native) + @($data.installed_catalog.api) | ForEach-Object { Coverage $_.operation $_.kind })
$coverageIndex = @{}; foreach ($entry in $coverage) { $coverageIndex[$entry.operation] = $entry }
if (($coverage.operation | Select-Object -Unique).Count -ne $coverage.Count) { throw 'Duplicate operation identifiers in coverage.' }
if ($data.models.Count -ne @($data.source_hashes.PSObject.Properties).Count) { throw 'Not every copied sample has a completed model record.' }
$coverage | Export-Csv -LiteralPath (Join-Path $out 'operation-coverage.csv') -NoTypeInformation -Encoding utf8
$promoted = @{
    '742369128674aae8a9b74352'='host_inserts'
    'da314384a8346ca50371fffc'='element_phase_status'
    '2c0d92d0a287c6d4189043f2'='host_bottom_faces'
    '8e2a12a6ec341250c7847a4b'='element_action_checks'
    '7630b8ed085ef156615dd58b'='element_action_checks'
    '6b8e3a13c6373f0081237d55'='element_action_checks'
    '5eebf2ea745551f5002036fd'='elements_joined'
    'ec8b1c584cb1b6c5896ccbcf'='type_compound_layers'
    '10c4da6d08ef6053ab62b250'='instance_transform'
}
$answers = @(Get-Content -LiteralPath (Join-Path $CampaignPath 'answers.jsonl') | ForEach-Object { $_ | ConvertFrom-Json })
$lookup = Get-Content -LiteralPath (Join-Path $CampaignPath 'lookup-candidates.json') -Raw | ConvertFrom-Json
$code = Get-Content -LiteralPath (Join-Path $CampaignPath 'code-candidates.json') -Raw | ConvertFrom-Json
$lookupIds = @{}; foreach ($c in $lookup.candidates) { $lookupIds[$c.result.id] = $true }
$codeIds = @{}; foreach ($c in $code.candidates) { $codeIds[$c.result.id] = $true }
if (($answers.id | Select-Object -Unique).Count -ne 3000) { throw 'Expected 3000 unique research result IDs.' }
$research = @(foreach ($a in $answers) {
    $classification = 'rejected'; $status = 'failed'; $native = $null; $runtime = $null
    if ($lookupIds.ContainsKey($a.id)) {
        $classification = 'lookup_reference'; $status = 'reference_only'; $native = $a.operation
        $runtime = $coverageIndex[$native].status
    } elseif ($codeIds.ContainsKey($a.id)) {
        $classification = 'compiled_candidate'; $status = 'awaiting_review'
        if ($promoted.ContainsKey($a.id) -and $coverageIndex.ContainsKey($promoted[$a.id])) {
            $native = $promoted[$a.id]; $classification = 'reviewed_native_promotion'
            $status = $coverageIndex[$native].status
            $runtime = $status
        }
    }
    [pscustomobject]@{id=$a.id; member=$a.member; classification=$classification; status=$status; native_operation=$native; runtime_status=$runtime}
})
$research | Export-Csv -LiteralPath (Join-Path $out 'research-3000-accounting.csv') -NoTypeInformation -Encoding utf8
if (@($research | Where-Object { $_.classification -eq 'lookup_reference' -and -not $_.runtime_status }).Count) { throw 'A research lookup does not map to an installed binding.' }
function Percentile($values, [double]$p) {
    $v = @($values | Sort-Object)
    if (-not $v.Count) { return $null }
    return [math]::Round($v[[math]::Max(0,[math]::Ceiling($p*$v.Count)-1)],2)
}
$latency = @(foreach ($m in $data.models) {
    foreach ($mode in 'separate','batch') {
        $samples = @($m.transport.samples | Where-Object { $_.mode -eq $mode -and -not $_.warmup })
        [pscustomobject]@{model=$m.model; mode=$mode; status=$m.transport.status; samples=$samples.Count;
            startup_ms=$m.transport.startup_ms; median_ms=(Percentile $samples.round_trip_ms 0.5);
            p95_ms=(Percentile $samples.round_trip_ms 0.95); median_response_bytes=(Percentile $samples.response_utf8_bytes 0.5)}
    }
})
$latency | Export-Csv -LiteralPath (Join-Path $out 'mcp-latency.csv') -NoTypeInformation -Encoding utf8
$summary = [ordered]@{
    benchmark_status=$data.status; samples=$data.models.Count; live_ai_calls=$data.live_ai_calls;
    catalog_counts=@($coverage | Group-Object kind,status | Select-Object Name,Count);
    research_counts=@($research | Group-Object classification,status | Select-Object Name,Count);
    failing_model_operations=@($data.operations | Where-Object status -eq 'failed').Count;
    fixture_suite_failures=@($data.operations | Where-Object { $_.operation -eq 'reviewed_fixture_suite' -and $_.status -eq 'failed' }).Count;
    transport_failures=@($data.models | Where-Object { $_.transport.status -ne 'passed' }).Count;
    limits=@('Getter pass means real invocation and serialization, not full semantic correctness.',
        'A global pass requires at least one passing fixture; failures on other models remain visible in CSV.',
        'Unloaded links exclude loaded-link behavior. Missing classes and unreviewed writes are not passes.',
        'Generated methods were not loaded. Lookup references are not executable methods.',
        'Timing excludes AI reasoning and measures a local single-client MCP session; ten warm repetitions per mode and sample.');
}
$summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $out 'summary.json') -Encoding utf8
$summary | ConvertTo-Json -Depth 5
