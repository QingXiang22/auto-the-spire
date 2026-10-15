param([string]$Root = (Get-Location).Path)
$ErrorActionPreference = 'Stop'
$tool = Join-Path $Root '.artifacts/parallel/dsh-headbutt/bin/Differential/debug/Differential.dll'
$audit = Join-Path $Root '.audit/dsh-headbutt-choice-2026-10-11'
function LoadJson($path) { Get-Content -LiteralPath $path -Raw -Encoding UTF8 | ConvertFrom-Json }
function Require($condition, $message) { if (-not $condition) { throw $message } }
$before = LoadJson (Join-Path $audit 'regression-before.json')
$suite = LoadJson (Join-Path $Root 'tools/differential/suites/regression.json')
Require (($before.expected_case_ids -join ',') -ceq ($suite.expected_case_ids -join ',')) 'Original denominator changed'
Require ($suite.cases.Count -eq 17) 'Expected original 17 cases'
foreach ($case in $before.cases) {
    if ($case.id -eq 'headbutt-mid-choice') { continue }
    $current = $suite.cases | Where-Object id -eq $case.id
    Require (($case | ConvertTo-Json -Depth 100 -Compress) -ceq ($current | ConvertTo-Json -Depth 100 -Compress)) "Unrelated case changed: $($case.id)"
}
$positive = LoadJson (Join-Path $audit 'positive/regression.json')
Require ($positive.counts.executed -eq 1 -and $positive.counts.reference_ran -eq 1 -and $positive.cases[0].verdict -eq 'passed') 'Positive not independently executed and passed'
foreach ($backend in @('base','reference')) {
    $record = LoadJson (Join-Path $audit "positive/records/$backend/headbutt-mid-choice.json")
    $step = $record.boundaries[1]
    Require ($record.status -eq 'ok' -and $record.boundaries.Count -eq 2) "$backend did not execute"
    Require ($step.decisions.Count -eq 2) "$backend missing actual target/card decisions"
    Require ($step.decisions[0].chosen -eq 'enemy:0') "$backend wrong target"
    Require (($step.decisions[1].candidates -join ',') -eq 'card:discard:0,card:discard:1') "$backend wrong card candidates"
    Require ($step.decisions[1].minimum -eq 1 -and $step.decisions[1].maximum -eq 1 -and $step.decisions[1].chosen -eq 'card:discard:0') "$backend wrong selection constraints"
    Require (($step.facts | Where-Object path -eq 'pile.draw.top').value -eq 'base-spire:defend-ironclad') "$backend not top-decked"
    Require (($step.facts | Where-Object path -eq 'pile.discard.sorted').value -eq 'base-spire:headbutt,base-spire:strike-ironclad') "$backend wrong aftermath discard"
    Require (($step.facts | Where-Object path -eq 'pile.draw.size').value -eq '9') "$backend wrong draw count"
    Require (($step.facts | Where-Object path -eq 'pile.hand.size').value -eq '0') "$backend wrong hand count"
}
# Change input, execute reference independently, then compare original input against that execution.
$changed = $suite.cases | Where-Object id -eq 'headbutt-mid-choice'
$changed.script[0].choose = 'card:discard:1'
$suite | ConvertTo-Json -Depth 100 | Set-Content (Join-Path $audit 'changed-choice-suite.json') -Encoding UTF8
& dotnet $tool test --suite .audit/dsh-headbutt-choice-2026-10-11/changed-choice-suite.json --only headbutt-mid-choice --output .audit/dsh-headbutt-choice-2026-10-11/changed-choice 2>&1 | Out-File (Join-Path $audit 'changed-choice.log') -Encoding utf8
$changedRc = $LASTEXITCODE
$changedReport = LoadJson (Join-Path $audit 'changed-choice/regression.json')
Require ($changedRc -eq 1 -and $changedReport.counts.reference_ran -eq 1 -and $changedReport.cases[0].verdict -eq 'passed') 'Changed choice did not independently execute'
$reference = LoadJson (Join-Path $audit 'changed-choice/records/reference/headbutt-mid-choice.json')
Require (($reference.boundaries[1].facts | Where-Object path -eq 'pile.draw.top').value -eq 'base-spire:strike-ironclad') 'Changed choice did not change actual top card'
& dotnet $tool test --suite regression --only headbutt-mid-choice --records .audit/dsh-headbutt-choice-2026-10-11/changed-choice/records/reference --output .audit/dsh-headbutt-choice-2026-10-11/negative 2>&1 | Out-File (Join-Path $audit 'negative.log') -Encoding utf8
$negativeRc = $LASTEXITCODE
$negative = LoadJson (Join-Path $audit 'negative/regression.json')
Require ($negativeRc -eq 1 -and $negative.cases[0].verdict -eq 'diverged') 'Expected mismatched-choice failure not detected'
[ordered]@{check='headbutt-choice';positive_executed=$positive.counts.executed;positive_reference_ran=$positive.counts.reference_ran;original_denominator=17;unchanged_other_cases=16;changed_choice_exit=$changedRc;changed_choice_reference_ran=$changedReport.counts.reference_ran;negative_exit=$negativeRc;negative_verdict=$negative.cases[0].verdict;negative_first_difference=$negative.cases[0].divergences[0];positive_strict_exit=1;positive_self_test_failures=$positive.counts.self_test_failures} | ConvertTo-Json -Depth 30 | Set-Content (Join-Path $audit 'mechanism-check.json') -Encoding UTF8
Write-Output 'Headbutt mechanism checks passed; changed-choice negative detected; original denominator preserved.'
