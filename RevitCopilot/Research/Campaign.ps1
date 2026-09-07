param([ValidateSet('Start', 'Status', 'Stop', 'Qualify')][string]$Action = 'Status')
$ErrorActionPreference = 'Stop'
$campaignRoot = Join-Path $PSScriptRoot 'artifacts\campaign-v5'
$progressPath = Join-Path $campaignRoot 'progress.json'
$stopPath = Join-Path $campaignRoot 'STOP'
function Test-CampaignQualification {
    $plan = Get-Content -LiteralPath (Join-Path $campaignRoot 'qualification-plan.json') -Raw | ConvertFrom-Json
    $bank = Get-Content -LiteralPath (Join-Path $campaignRoot 'questions.json') -Raw | ConvertFrom-Json
    $rows = @(Get-Content -LiteralPath (Join-Path $campaignRoot 'answers.jsonl') | ForEach-Object { $_ | ConvertFrom-Json })
    $sample = @($rows | Where-Object { $_.id -in $plan.ids })
    $code = @($sample | Where-Object kind -eq 'code')
    $codePassed = @($code | Where-Object { $_.validation.status -eq 'compiled_candidate_NOT_runtime_verified' }).Count
    $firstPassed = @($code | Where-Object { $_.attempts[0].validation.status -eq 'compiled_candidate_NOT_runtime_verified' }).Count
    $lookupPassed = @($sample | Where-Object { $_.kind -eq 'lookup' -and $_.validation.status -eq 'candidate_aliases_need_semantic_review' }).Count
    $regressionIds = @($bank | Select-Object -First $plan.retest_failures -ExpandProperty id)
    $regressionPassed = @($code | Where-Object { $_.id -in $regressionIds -and $_.validation.status -eq 'compiled_candidate_NOT_runtime_verified' }).Count
    $expectedCode = $plan.retest_failures + $plan.spread_code
    $passed = $sample.Count -eq $plan.total -and @($sample.id | Select-Object -Unique).Count -eq $plan.total -and
        $code.Count -eq $expectedCode -and $codePassed -ge [Math]::Ceiling($expectedCode * 0.9) -and
        $firstPassed -ge [Math]::Ceiling($expectedCode * 0.8) -and $regressionPassed -eq $plan.retest_failures -and $lookupPassed -eq $plan.lookups
    $result = [ordered]@{ passed=$passed; tested=$sample.Count; expected=$plan.total; code_first_pass=$firstPassed; code_after_bounded_repair=$codePassed;
        code_total=$expectedCode; prior_failures_resolved=$regressionPassed; prior_failures_total=$plan.retest_failures; lookup_passed=$lookupPassed;
        manifest_sha256=(Get-FileHash -LiteralPath (Join-Path $campaignRoot 'run-manifest.json') -Algorithm SHA256).Hash;
        checked_utc=[DateTimeOffset]::UtcNow.ToString('o'); runtime_verified=$false }
    $result | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $campaignRoot 'qualification-result.json')
    return $result
}
if ($Action -eq 'Qualify') { Test-CampaignQualification | ConvertTo-Json; return }
if ($Action -eq 'Status') {
    if (Test-Path -LiteralPath $progressPath) { Get-Content -LiteralPath $progressPath }
    else { Write-Output 'No progress recorded yet.' }
    return
}
if ($Action -eq 'Stop') {
    New-Item -ItemType File -Path $stopPath -Force | Out-Null
    Write-Output 'Stop requested. The current answer will finish and the campaign will save its reports.'
    return
}
$runnerPath = Join-Path $PSScriptRoot 'bin\x64\Debug\net10.0\KnowledgeLab.dll'
if (!(Test-Path -LiteralPath $runnerPath)) { throw 'Build KnowledgeLab first with SkipRevitDeploy=true.' }
if (!(Test-Path -LiteralPath (Join-Path $campaignRoot 'questions.json'))) { throw 'Prepare campaign-v5 first.' }
$lockPath = Join-Path $campaignRoot 'run.lock'
$campaignLock = [System.IO.File]::Open($lockPath, 'OpenOrCreate', 'ReadWrite', 'None')
$campaignLock.Dispose()
$qualification = Test-CampaignQualification
if (!$qualification.passed) { throw 'Qualification did not pass. Inspect qualification-result.json; full-batch resume is blocked.' }
if (Test-Path -LiteralPath $stopPath) { Remove-Item -LiteralPath $stopPath }
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$runArgs = @('"' + $runnerPath + '"', 'run', '--limit', '3000', '--model', 'qwen3:14b', '--output', '"' + $campaignRoot + '"')
$campaignProcess = Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList ($runArgs -join ' ') -WorkingDirectory $PSScriptRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $campaignRoot "$stamp.stdout.log") -RedirectStandardError (Join-Path $campaignRoot "$stamp.stderr.log")
Write-Output "Local campaign started; process $($campaignProcess.Id). Use Campaign.ps1 Status or Campaign.ps1 Stop."
