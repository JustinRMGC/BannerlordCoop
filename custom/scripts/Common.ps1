# =============================================================================
#  FONZA-CUSTOM  ·  Common.ps1  ·  shared helpers (dot-sourced by the other scripts)
#  NOT part of upstream Bannerlord-Coop. Owned by this fork.  Docs: /custom/CLAUDE.md
# =============================================================================

# --- console colour helpers (match the repo's prepare-links.ps1 style) -------
function Write-Ok    { param([string]$m) Write-Host $m -ForegroundColor Green }
function Write-Bad   { param([string]$m) Write-Host $m -ForegroundColor Red }
function Write-Warn  { param([string]$m) Write-Host $m -ForegroundColor Yellow }
function Write-Info  { param([string]$m) Write-Host $m -ForegroundColor Cyan }
function Write-Head  { param([string]$m) Write-Host ""; Write-Host $m -ForegroundColor White; Write-Host ("-" * $m.Length) -ForegroundColor DarkGray }

# --- repo location -----------------------------------------------------------
# This file lives at <repo>/custom/scripts/, so the repo root is two levels up.
function Get-FonzaRepoRoot { (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path }

# --- upstream base -----------------------------------------------------------
# The commit our custom work sits on top of = merge-base(upstream/development, HEAD).
# Everything in `git diff <base>..HEAD` (+ the working tree) is ours.
function Get-FonzaUpstreamBase {
    $b = (& git merge-base upstream/development HEAD 2>$null)
    if (-not $b) { $b = (& git merge-base origin/development HEAD 2>$null) }
    if ($b) { return $b.Trim() }
    return $null
}

function Test-FonzaUpstreamRemote {
    $remotes = (& git remote 2>$null)
    return ($remotes -contains 'upstream')
}

# --- load the integrity assertions ------------------------------------------
function Import-FonzaAssertions {
    $p = Join-Path (Get-FonzaRepoRoot) 'custom\integrity\assertions.psd1'
    if (-not (Test-Path $p)) { throw "assertions.psd1 not found at $p" }
    return Import-PowerShellDataFile -Path $p
}

# --- the fork's inventory (kept in sync with /custom/MANIFEST.md) ------------
# NEW files that are 100% ours (never conflict on pull; only vanish if deleted).
# The bridge MUST live in Coop.Core (it compiles into the mod assembly); it is a
# new, banner-marked, pull-safe file. Everything else lives under custom/.
$script:FonzaNewFiles = @(
    'source/Coop.Core/Server/Admin/ConsoleControlBridge.cs',
    'source/Directory.Build.targets',
    'custom/Directory.Build.props',
    'custom/FonzaLauncher.sln',
    'custom/CoopServerConsole/AppConfig.cs',
    'custom/CoopServerConsole/ControlChannel.cs',
    'custom/CoopServerConsole/CoopServerConsole.csproj',
    'custom/CoopServerConsole/GameLauncher.cs',
    'custom/CoopServerConsole/LogFollower.cs',
    'custom/CoopServerConsole/LogInsights.cs',
    'custom/CoopServerConsole/MainForm.cs',
    'custom/CoopServerConsole/Paths.cs',
    'custom/CoopServerConsole/Program.cs'
)

# UPSTREAM files this fork edits in-place (must survive pulls; verified by markers).
# Each entry: the file + the FONZA-CUSTOM marker token that must remain present.
$script:FonzaEditedFiles = @(
    @{ Path = 'source/Coop/CoopMod.cs'; Marker = 'FONZA-CUSTOM START'; What = 'server-only ConsoleControlBridge hook in NoHarmonyLoad()' }
)

function Get-FonzaNewFiles     { $script:FonzaNewFiles }
function Get-FonzaEditedFiles  { $script:FonzaEditedFiles }

# Search .cs files under a repo-relative root for a regex; return matching file paths.
function Find-FonzaSourceMatch {
    param([string]$RepoRelRoot, [string]$Pattern)
    $abs = Join-Path (Get-FonzaRepoRoot) $RepoRelRoot
    if (-not (Test-Path $abs)) { return @() }
    Get-ChildItem -Path $abs -Recurse -Filter *.cs -File -ErrorAction SilentlyContinue |
        Select-String -Pattern $Pattern -List -ErrorAction SilentlyContinue |
        ForEach-Object { $_.Path }
}
