<#
  =============================================================================
  FONZA-CUSTOM  ·  sync-upstream.ps1
  NOT part of upstream Bannerlord-Coop. Owned by this fork.  Docs: /custom/CLAUDE.md

  Convenience wrapper for the merge-based upstream update workflow:

      git fetch upstream  ->  git merge --no-edit upstream/development  ->  verify

  Deliberately conservative and safe to review: it only FETCHES and MERGES.
  It never pushes, resets, force-updates, rebases, or deletes anything, and it
  refuses to run on a dirty tree so your work-in-progress can never be folded
  into an upstream merge. On conflicts it stops and tells you to keep the
  FONZA-CUSTOM blocks; on a clean merge it runs verify-integrity.ps1 for you.

  Exit codes:
     0  up to date, or clean merge (verify passed or was skipped)
     1  precondition failed (no 'upstream' remote, dirty tree, or bad ref)
     2  merge left conflicts for you to resolve by hand
    >0  after a clean merge = verify-integrity.ps1 failure count

  Usage:
    powershell -ExecutionPolicy Bypass -File custom\scripts\sync-upstream.ps1
    ... -DryRun        fetch + show what would merge, then stop (no merge)
    ... -NoVerify      merge but skip the post-merge integrity check
    ... -UpstreamRef upstream/some-branch   merge a ref other than the default
  =============================================================================
#>
[CmdletBinding()]
param(
    [switch]$DryRun,
    [switch]$NoVerify,
    [string]$UpstreamRef = 'upstream/development'
)

. (Join-Path $PSScriptRoot 'Common.ps1')

$repo = Get-FonzaRepoRoot
Set-Location $repo

Write-Host ""
Write-Host "========================================================================" -ForegroundColor White
Write-Host "  FONZA-CUSTOM  ·  upstream sync (fetch + merge)" -ForegroundColor White
Write-Host "  repo: $repo" -ForegroundColor DarkGray
Write-Host "  ref : $UpstreamRef" -ForegroundColor DarkGray
Write-Host "========================================================================" -ForegroundColor White

# ---------------------------------------------------------------- 1. upstream remote
if (-not (Test-FonzaUpstreamRemote)) {
    Write-Bad "No 'upstream' remote is configured, so there is nothing to sync from."
    Write-Bad "Add it once, then re-run this script:"
    Write-Info "  git remote add upstream https://github.com/Bannerlord-Coop-Team/BannerlordCoop.git"
    exit 1
}

# ---------------------------------------------------------------- 2. clean tree
$dirty = @(& git status --porcelain)
if ($dirty.Count -gt 0) {
    Write-Bad "Working tree not clean - commit or stash first (this script must not mix your WIP with an upstream merge)."
    Write-Host "  uncommitted changes:" -ForegroundColor DarkGray
    foreach ($d in $dirty) { Write-Host ("    {0}" -f $d) -ForegroundColor DarkGray }
    exit 1
}

# ---------------------------------------------------------------- 3. fetch
Write-Head "Fetching upstream"
Write-Info "  git fetch upstream"
& git fetch upstream
if ($LASTEXITCODE -ne 0) {
    Write-Warn "  'git fetch upstream' returned a non-zero exit code; continuing with cached refs (results may be stale)."
}

# Make sure the ref actually resolves, so a typo can't masquerade as "up to date".
$null = & git rev-parse --verify --quiet "$UpstreamRef^{commit}" 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Bad ("Ref '{0}' could not be resolved after fetch (typo, or not on the fetched remote?)." -f $UpstreamRef)
    Write-Info "  List remote branches with:  git branch -r"
    exit 1
}

# ---------------------------------------------------------------- 4. show incoming
Write-Head ("Incoming (HEAD..{0})" -f $UpstreamRef)
$incoming = @(& git log --oneline "HEAD..$UpstreamRef" 2>$null)
if ($incoming.Count -eq 0) {
    Write-Ok "Already up to date."
    exit 0
}
Write-Info ("  {0} commit(s) to merge:" -f $incoming.Count)
foreach ($c in $incoming) { Write-Host ("    {0}" -f $c) -ForegroundColor Gray }

Write-Host ""
Write-Info "  diff --stat:"
$stat = @(& git diff --stat "HEAD..$UpstreamRef" 2>$null)
foreach ($s in $stat) { Write-Host ("    {0}" -f $s) -ForegroundColor DarkGray }

# ---------------------------------------------------------------- 5. dry run
if ($DryRun) {
    Write-Host ""
    Write-Info "Dry run - not merging."
    exit 0
}

# ---------------------------------------------------------------- 6. merge
Write-Head "Merging"
Write-Info ("  git merge --no-edit {0}" -f $UpstreamRef)
& git merge --no-edit $UpstreamRef
$mergeExit = $LASTEXITCODE

# ---------------------------------------------------------------- 7. conflicts?
$conflicts = @(& git diff --name-only --diff-filter=U 2>$null)
$unmerged  = @(& git ls-files -u 2>$null)
if ($conflicts.Count -gt 0 -or $unmerged.Count -gt 0 -or $mergeExit -ne 0) {
    Write-Bad ("Merge did not complete cleanly (git merge exit={0})." -f $mergeExit)
    if ($conflicts.Count -gt 0) {
        Write-Bad "  conflicted files:"
        foreach ($c in $conflicts) { Write-Bad ("    {0}" -f $c) }
    }
    Write-Head "How to finish the merge by hand"
    Write-Info "  1. Edit each conflicted file, KEEPING the FONZA-CUSTOM blocks (see /custom/CLAUDE.md)."
    Write-Info "  2. Stage + commit to complete the merge:   git add -A ; git commit --no-edit"
    Write-Info "  3. Re-check integrity:                     powershell -ExecutionPolicy Bypass -File custom\scripts\verify-integrity.ps1"
    Write-Warn "  (Nothing was pushed or reset - the repo is simply mid-merge. 'git merge --abort' backs it all out.)"
    exit 2
}

# ---------------------------------------------------------------- 8. clean merge
Write-Ok "Merge clean."
if ($NoVerify) {
    Write-Warn "Skipped the post-merge integrity check (-NoVerify)."
    Write-Info "  Run it when ready:  powershell -ExecutionPolicy Bypass -File custom\scripts\verify-integrity.ps1"
    exit 0
}
Write-Head "Post-merge integrity check"
& (Join-Path $PSScriptRoot 'verify-integrity.ps1')
exit $LASTEXITCODE
