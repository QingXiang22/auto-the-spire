param(
    [string[]] $Name = @(),
    [switch] $Remove
)

$ErrorActionPreference = 'Stop'
if ($Remove -and $Name.Count -eq 0) { throw 'Removal requires explicit worktree names via -Name.' }
foreach ($taskName in $Name) {
    if ($taskName -notmatch '^[A-Za-z0-9][A-Za-z0-9_.-]*$') { throw "Invalid worktree name: $taskName" }
}
$taskRepository = (git rev-parse --show-toplevel)
if ($LASTEXITCODE -ne 0) { throw 'Run inside the project repository.' }
$taskRoot = [IO.Path]::GetFullPath((Join-Path $taskRepository '.artifacts/worktrees'))
$taskPrefix = $taskRoot + [IO.Path]::DirectorySeparatorChar
$taskPaths = @(git worktree list --porcelain | Where-Object { $_.StartsWith('worktree ') } | ForEach-Object { $_.Substring(9) })
foreach ($taskEntry in $taskPaths) {
    $taskPath = [IO.Path]::GetFullPath($taskEntry)
    if (-not $taskPath.StartsWith($taskPrefix, [StringComparison]::OrdinalIgnoreCase)) { continue }
    $taskName = Split-Path -Leaf $taskPath
    if ($Name.Count -gt 0 -and $taskName -notin $Name) { continue }
    if (-not (Test-Path -LiteralPath $taskPath)) { continue }
    $taskResolved = (Resolve-Path -LiteralPath $taskPath).Path
    if ($taskResolved -ne $taskPath -or (Get-Item -LiteralPath $taskPath).LinkType) { throw "Linked worktree path refused: $taskPath" }
    $taskReason = ''
    $taskStatus = git -C $taskPath status --porcelain
    if ($LASTEXITCODE -ne 0) { throw "Cannot read worktree status: $taskPath" }
    if ($taskStatus) { $taskReason = 'uncommitted or untracked changes' }
    git -C $taskPath merge-base --is-ancestor HEAD main
    if ($LASTEXITCODE -ne 0) { $taskReason = 'HEAD is not contained in local main' }
    $taskReference = Join-Path $taskPath 'reference/sts2-sim'
    if (Test-Path -LiteralPath (Join-Path $taskReference '.git')) {
        $taskReferenceStatus = git -C $taskReference status --porcelain
        if ($LASTEXITCODE -ne 0 -or $taskReferenceStatus) { $taskReason = 'reference checkout is not clean' }
    }
    if ($taskReason) {
        [pscustomobject]@{ Name = $taskName; Action = 'kept'; Reason = $taskReason }
        if ($Remove) { throw "Worktree is not safe to remove: $taskName" }
        continue
    }
    $taskHead = git -C $taskPath rev-parse HEAD
    if ($Remove) {
        # Recheck immediately before Git removes the explicitly selected directory, including ignored outputs.
        $taskLatestStatus = git -C $taskPath status --porcelain
        $taskLatestHead = git -C $taskPath rev-parse HEAD
        if ($LASTEXITCODE -ne 0 -or $taskLatestStatus -or $taskLatestHead -ne $taskHead) { throw "Worktree changed: $taskName" }
        git worktree remove --force $taskPath
        if ($LASTEXITCODE -ne 0) { throw "Worktree removal failed: $taskName" }
        [pscustomobject]@{ Name = $taskName; Action = 'removed'; Reason = 'clean; HEAD is contained in main' }
    }
    else {
        [pscustomobject]@{ Name = $taskName; Action = 'candidate'; Reason = 'confirm task completion before using -Name and -Remove' }
    }
}
