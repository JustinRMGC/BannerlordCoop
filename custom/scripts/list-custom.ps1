<#
  =============================================================================
  FONZA-CUSTOM  ·  list-custom.ps1
  NOT part of upstream Bannerlord-Coop. Owned by this fork.  Docs: /custom/CLAUDE.md

  Read-only provenance report. Answers "what here is mine vs. upstream?" at a
  glance: the custom NEW files (100% ours), the one upstream file we edit
  in-place (with its actual custom diff), and every FONZA-CUSTOM marker in the
  source tree. Changes nothing and always exits 0 - safe to run any time.

  Usage:
    powershell -ExecutionPolicy Bypass -File custom\scripts\list-custom.ps1
  =============================================================================
#>

. (Join-Path $PSScriptRoot 'Common.ps1')

$repo = Get-FonzaRepoRoot
Set-Location $repo
$base = Get-FonzaUpstreamBase

Write-Host ""
Write-Host "========================================================================" -ForegroundColor White
Write-Host "  FONZA-CUSTOM  ·  custom-code provenance" -ForegroundColor White
Write-Host "  repo: $repo" -ForegroundColor DarkGray
if ($base) { Write-Host "  upstream base: $base" -ForegroundColor DarkGray }
else       { Write-Host "  upstream base: (unknown - no upstream/origin merge-base)" -ForegroundColor DarkGray }
Write-Host "========================================================================" -ForegroundColor White

# ---------------------------------------------------------------- 1. new files
Write-Head "1. Custom NEW files (100% ours)"
$newFiles = @(Get-FonzaNewFiles)
$present = 0
$missing = 0
foreach ($f in $newFiles) {
    if (Test-Path (Join-Path $repo $f)) {
        $present++
        Write-Ok  ("  OK      {0}" -f $f)
    } else {
        $missing++
        Write-Bad ("  MISSING {0}" -f $f)
    }
}

# ---------------------------------------------------------------- 2. in-place edits
Write-Head "2. In-place edits to upstream files"
$edited = @(Get-FonzaEditedFiles)
$editedOk = 0
foreach ($e in $edited) {
    Write-Info ("  {0}" -f $e.Path)
    Write-Host ("      what   : {0}" -f $e.What) -ForegroundColor DarkGray

    $p = Join-Path $repo $e.Path
    $markerPresent = $false
    if (Test-Path $p) {
        $text = Get-Content -Raw -LiteralPath $p
        if ($text -match [regex]::Escape($e.Marker)) { $markerPresent = $true }
    }
    if ($markerPresent) {
        $editedOk++
        Write-Ok  ("      marker : OK   '{0}' present" -f $e.Marker)
    } else {
        Write-Bad ("      marker : LOST '{0}' NOT found (an upstream merge may have dropped our edit)" -f $e.Marker)
    }

    # The actual custom diff so the reviewer sees exactly what we changed.
    if ($null -eq $base) {
        Write-Warn "      diff   : upstream base unknown - cannot show diff (add the 'upstream' remote)"
    } else {
        $diff = @(& git diff "$base..HEAD" -- $e.Path 2>$null)
        if ($diff.Count -eq 0) {
            Write-Warn "      diff   : (no committed changes vs. upstream base - edit may be uncommitted)"
        } else {
            Write-Host "      diff   : (base..HEAD)" -ForegroundColor DarkGray
            foreach ($line in $diff) {
                $color = 'DarkGray'
                if     ($line -match '^\+\+\+' -or $line -match '^---') { $color = 'DarkGray' }
                elseif ($line -match '^\+')  { $color = 'Green' }
                elseif ($line -match '^-')   { $color = 'Red' }
                elseif ($line -match '^@@')  { $color = 'Cyan' }
                Write-Host ("        {0}" -f $line) -ForegroundColor $color
            }
        }
    }
}

# ---------------------------------------------------------------- 3. all markers
Write-Head "3. All FONZA-CUSTOM markers in the tree"
$counts = [ordered]@{}
$grep   = @()
$gitOk  = $false
try {
    $grep = @(& git grep -n "FONZA-CUSTOM" 2>$null)
    if ($LASTEXITCODE -eq 0 -and $grep.Count -gt 0) { $gitOk = $true }
} catch {
    $gitOk = $false
}

if ($gitOk) {
    foreach ($line in $grep) {
        # git grep output is  <path>:<lineno>:<text>
        if ($line -match '^(.*?):\d+:') {
            $file = $Matches[1]
            if (-not $counts.Contains($file)) { $counts[$file] = 0 }
            $counts[$file] = $counts[$file] + 1
        }
    }
} else {
    # Fallback (git unavailable, or no committed matches): scan the source tree.
    # Restricted to source\ and custom\ - the only roots that carry markers -
    # which also avoids following the large mb2 game junction at the repo root.
    $scanRoots = @('source', 'custom') | ForEach-Object { Join-Path $repo $_ } | Where-Object { Test-Path $_ }
    if ($scanRoots.Count -gt 0) {
        $hits = Get-ChildItem -Path $scanRoots -Recurse -File -Include *.cs, *.props, *.targets, *.sln, *.psd1, *.ps1 -ErrorAction SilentlyContinue |
                Select-String -Pattern 'FONZA-CUSTOM' -ErrorAction SilentlyContinue
        foreach ($h in $hits) {
            $rel = $h.Path
            if ($rel.StartsWith($repo)) { $rel = $rel.Substring($repo.Length).TrimStart('\', '/') }
            if (-not $counts.Contains($rel)) { $counts[$rel] = 0 }
            $counts[$rel] = $counts[$rel] + 1
        }
    }
}

$markerLines = 0
if ($counts.Count -eq 0) {
    Write-Warn "  (no FONZA-CUSTOM markers found)"
} else {
    foreach ($k in $counts.Keys) {
        Write-Host ("  {0,3}x  {1}" -f $counts[$k], $k) -ForegroundColor Gray
        $markerLines += $counts[$k]
    }
    Write-Info ("  = {0} marker line(s) across {1} file(s)" -f $markerLines, $counts.Count)
}

# ---------------------------------------------------------------- summary
Write-Head "Summary"
if ($missing -eq 0) {
    Write-Ok  ("  custom NEW files : {0}/{0} present" -f $newFiles.Count)
} else {
    Write-Bad ("  custom NEW files : {0}/{1} present  ({2} MISSING)" -f $present, $newFiles.Count, $missing)
}
if ($edited.Count -eq 0) {
    Write-Info "  in-place edits   : none declared"
} elseif ($editedOk -eq $edited.Count) {
    Write-Ok  ("  in-place edits   : {0}/{1} still marked OK" -f $editedOk, $edited.Count)
} else {
    Write-Bad ("  in-place edits   : {0}/{1} still marked OK" -f $editedOk, $edited.Count)
}
Write-Host ""

# Read-only report: always succeeds.
exit 0
