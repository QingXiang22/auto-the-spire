<#
.SYNOPSIS
    ENV-T04 strict regression entry (thin wrapper): one build, then the strict suite.

.DESCRIPTION
    Implements the default command from ENV constraints section 12:
        pwsh -File tools/differential/test-env.ps1 -Suite regression

    1. Builds the dependency closure of the strict suite
       (tools/differential/Differential.csproj) from the repository root.
       Use -FullSolution to build AutoTheSpire.sln instead.
    2. With -RuleChecks, also runs the Engine / Slice rule checks required by the
       root AGENTS.md when rules / DSL / content change (tool-only changes do not
       trigger them by default).
    3. Runs: dotnet run --project tools/differential -- test --suite <Suite>

    Any non-zero child process becomes this script's exit code verbatim: nothing
    is swallowed and nothing is turned into 0. If reference/sts2-sim is absent the
    build still succeeds (the reference project reference is conditional) and the
    strict suite fails with exit code 3.

    The script does not clean the workspace, commit, push or overwrite other
    sessions' changes.

    NOTE: this file is intentionally ASCII-only. Windows PowerShell 5.1 decodes
    BOM-less scripts as ANSI, which corrupts non-ASCII literals.

.EXAMPLE
    pwsh -File tools/differential/test-env.ps1 -Suite regression
    pwsh -File tools/differential/test-env.ps1 -Suite regression -SkipBuild -Output artifacts/environment/tests/regression
    pwsh -File tools/differential/test-env.ps1 -Suite regression -FullSolution -RuleChecks
#>
[CmdletBinding()]
param(
    [string]$Suite = "regression",
    [string]$Output = "artifacts/environment/tests/regression",
    [string]$Records = "",
    [switch]$SkipBuild,
    [switch]$FullSolution,
    [switch]$RuleChecks
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Push-Location $repoRoot
try {
    Write-Host "[test-env] repo root: $repoRoot"
    Write-Host "[test-env] suite: $Suite; output: $Output"

    if (-not $SkipBuild) {
        # Default: build only what the strict suite consumes (Core + Base + reference + tool).
        # Other lines (net/ai) may be mid-edit in this shared workspace; that is not this
        # line's change and must not block B-line differential evidence. Use -FullSolution
        # to build the whole solution explicitly.
        $target = if ($FullSolution) { 'AutoTheSpire.sln' } else { 'tools/differential/Differential.csproj' }
        Write-Host "[test-env] building $target (serial; shared obj/bin)"
        & dotnet build $target
        if ($LASTEXITCODE -ne 0) {
            Write-Host "[test-env] build failed, exit code $LASTEXITCODE"
            exit $LASTEXITCODE
        }
    }
    else {
        Write-Host "[test-env] -SkipBuild: no build (caller guarantees it is built)"
    }

    if ($RuleChecks) {
        Write-Host "[test-env] rule check: Core.TheSpire.Engine.Tests"
        & dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

        Write-Host "[test-env] rule check: BaseSpire.Slice.Tests"
        & dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }

    $arguments = @('run', '--project', 'tools/differential', '--', 'test', '--suite', $Suite, '--output', $Output)
    if ($Records -ne "") { $arguments += @('--records', $Records) }
    Write-Host "[test-env] strict suite: dotnet $($arguments -join ' ')"
    & dotnet @arguments
    $code = $LASTEXITCODE
    Write-Host "[test-env] strict suite exit code: $code (0=complete pass; 1=some case not passed or subset; 2=suite definition/usage error; 3=reference unavailable)"
    exit $code
}
finally {
    Pop-Location
}
