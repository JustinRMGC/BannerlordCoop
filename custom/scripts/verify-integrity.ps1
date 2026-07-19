<#
  =============================================================================
  FONZA-CUSTOM  ·  verify-integrity.ps1
  NOT part of upstream Bannerlord-Coop. Owned by this fork.  Docs: /custom/CLAUDE.md

  Runs AFTER pulling upstream. Confirms every upstream symbol the console bridge
  depends on still exists in the freshly-merged source, that all custom files are
  present, and that the one in-place edit is still marked. Prints a colour report
  and exits with the number of FAILURES (0 = all good) so it is CI/skill friendly.

  Usage:
    powershell -ExecutionPolicy Bypass -File custom\scripts\verify-integrity.ps1
    ... -Build     also attempt a best-effort Coop.Core compile (needs VS/msbuild)
    ... -Quiet     only print failures + the summary line
  =============================================================================
#>
[CmdletBinding()]
param([switch]$Build, [switch]$Quiet)

. (Join-Path $PSScriptRoot 'Common.ps1')

$repo = Get-FonzaRepoRoot
Set-Location $repo
$data  = Import-FonzaAssertions
$roots = $data.SearchRoots

$pass = 0; $fail = 0; $warn = 0
$failIds = @()

Write-Host ""
Write-Host "========================================================================" -ForegroundColor White
Write-Host "  FONZA-CUSTOM  ·  post-pull integrity check" -ForegroundColor White
Write-Host "  repo: $repo" -ForegroundColor DarkGray
$base = Get-FonzaUpstreamBase
if ($base) { Write-Host "  upstream base: $base" -ForegroundColor DarkGray }
Write-Host "========================================================================" -ForegroundColor White

# ---------------------------------------------------------------- provenance
Write-Head "1. Custom files present"
foreach ($f in (Get-FonzaNewFiles)) {
    if (Test-Path (Join-Path $repo $f)) {
        $pass++; if (-not $Quiet) { Write-Ok  ("  OK   {0}" -f $f) }
    } else {
        $fail++; $failIds += "MISSING:$f"; Write-Bad ("  GONE {0}   <- a custom file disappeared (bad merge / accidental delete)" -f $f)
    }
}

Write-Head "2. In-place edits still marked"
foreach ($e in (Get-FonzaEditedFiles)) {
    $p = Join-Path $repo $e.Path
    if (-not (Test-Path $p)) {
        $fail++; $failIds += "EDIT-FILE-GONE:$($e.Path)"; Write-Bad ("  GONE {0}" -f $e.Path); continue
    }
    $t = Get-Content -Raw -LiteralPath $p
    if ($t -match [regex]::Escape($e.Marker)) {
        $pass++; if (-not $Quiet) { Write-Ok ("  OK   {0}  [{1}]" -f $e.Path, $e.What) }
    } else {
        $fail++; $failIds += "EDIT-LOST:$($e.Path)"
        Write-Bad ("  LOST {0}  <- marker '{1}' gone; upstream merge likely dropped our edit ({2})" -f $e.Path, $e.Marker, $e.What)
    }
}

# ---------------------------------------------------------------- symbol assertions
Write-Head "3. Upstream API the bridge depends on"
foreach ($a in $data.Assertions) {
    $rel = $roots[$a.Root]
    $ns  = ($a.Namespace -replace '^namespace\s+','')
    $nsPattern = 'namespace\s+' + [regex]::Escape($ns) + '(?![\w.])'
    $nsFiles = Find-FonzaSourceMatch -RepoRelRoot $rel -Pattern $nsPattern

    $ok = $false; $why = ''
    if (-not $nsFiles) {
        $why = "namespace '$ns' not found under $rel (moved/renamed?)"
    } else {
        $typeFiles = @()
        foreach ($f in $nsFiles) { if ((Get-Content -Raw -LiteralPath $f) -match $a.TypePattern) { $typeFiles += $f } }
        if (-not $typeFiles) {
            $why = "type /$($a.TypePattern)/ not found in the '$ns' file(s)"
        } else {
            $blob = ($typeFiles | ForEach-Object { Get-Content -Raw -LiteralPath $_ }) -join "`n"
            $missing = @(); foreach ($m in $a.Members) { if ($blob -notmatch $m) { $missing += $m } }
            if ($missing.Count -gt 0) {
                $why = "member(s) missing in $([System.IO.Path]::GetFileName($typeFiles[0])): " + ($missing -join ', ')
            } else { $ok = $true; $why = [System.IO.Path]::GetFileName($typeFiles[0]) }
        }
    }

    if ($ok) {
        $pass++; if (-not $Quiet) { Write-Ok ("  OK   [{0,-9}] {1}  ({2})" -f $a.Fragility, $a.Id, $why) }
    } else {
        $fail++; $failIds += $a.Id
        Write-Bad  ("  FAIL [{0,-9}] {1}" -f $a.Fragility, $a.Id)
        Write-Bad  ("         reason : {0}" -f $why)
        Write-Bad  ("         breaks : {0}" -f $a.Breaks)
        if ($a.Note) { Write-Warn ("         note   : {0}" -f $a.Note) }
    }
}

# ---------------------------------------------------------------- DI probe (runtime risk)
Write-Head "4. Autofac registrations (silent-failure probe)"
foreach ($di in $data.DiRegistrations) {
    $if = $di.Interface
    # (a) explicit registration:  builder.RegisterType<X>().As<IFoo>()  /  .As(typeof(IFoo))
    $patExplicit = "As<\s*$if\s*>|\.As\(\s*typeof\(\s*$if\s*\)|As<\s*$if\s*,"
    $hit = @()
    $hit += Find-FonzaSourceMatch -RepoRelRoot $roots['CoopCore']      -Pattern $patExplicit
    $hit += Find-FonzaSourceMatch -RepoRelRoot $roots['GameInterface'] -Pattern $patExplicit
    $how = ''
    if ($hit) {
        $how = 'explicit .As<>()'
    } else {
        # (b) convention: GameInterface's ServiceModule auto-registers every type whose interface
        #     derives from IGameAbstraction (GetGameAbstractions scan). So an interface declared
        #     "interface IFoo : ...IGameAbstraction" is registered even with no explicit .As<IFoo>().
        $patConv = "interface\s+$if\b[^{]*IGameAbstraction"
        if (Find-FonzaSourceMatch -RepoRelRoot $roots['GameInterface'] -Pattern $patConv) {
            $how = 'IGameAbstraction convention (ServiceModule)'
        }
    }
    if ($how) {
        $pass++; if (-not $Quiet) { Write-Ok ("  OK   {0} registered  ({1})" -f $if, $how) }
    } else {
        $warn++
        Write-Warn ("  WARN {0}: no explicit '.As<{0}>()' and not an IGameAbstraction. If it isn't registered by some other scan, the '{1}' command will silently report 'unavailable'." -f $if, $di.Breaks)
    }
}

# ---------------------------------------------------------------- build invariants (our files)
Write-Head "5. Build invariants owned by this fork"
foreach ($bi in $data.BuildInvariants) {
    $p = Join-Path $repo $bi.File
    if ((Test-Path $p) -and ((Get-Content -Raw -LiteralPath $p) -match $bi.Pattern)) {
        $pass++; if (-not $Quiet) { Write-Ok ("  OK   {0}: /{1}/ present in {2}" -f $bi.Id, $bi.Pattern, $bi.File) }
    } else {
        $fail++; $failIds += $bi.Id
        Write-Bad ("  FAIL {0}: /{1}/ missing from {2}  (breaks: {3})" -f $bi.Id, $bi.Pattern, $bi.File, $bi.Breaks)
    }
}

# ---------------------------------------------------------------- optional build
if ($Build) {
    Write-Head "6. Best-effort compile (Coop.Core)"
    $msb = $null
    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $vs = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath 2>$null
        if ($vs) { $cand = Join-Path $vs 'MSBuild\Current\Bin\MSBuild.exe'; if (Test-Path $cand) { $msb = $cand } }
    }
    if (-not $msb) { $g = Get-Command msbuild -ErrorAction SilentlyContinue; if ($g) { $msb = $g.Source } }

    if (-not $msb) {
        $warn++; Write-Warn "  WARN msbuild not found - skipped. Open source\Coop.sln in Visual Studio to confirm the build."
    } else {
        Write-Info "  using $msb"
        & $msb "source\Coop.Core\Coop.Core.csproj" /t:Build /p:Configuration=Debug /p:NuGetAudit=false /p:TreatWarningsAsErrors=false /v:minimal /nologo
        if ($LASTEXITCODE -eq 0) { $pass++; Write-Ok "  OK   Coop.Core compiled" }
        else { $fail++; $failIds += 'BUILD:Coop.Core'; Write-Bad "  FAIL Coop.Core did not compile (see msbuild output above)" }
    }
}

# ---------------------------------------------------------------- summary
Write-Host ""
Write-Host "========================================================================" -ForegroundColor White
if ($fail -eq 0) { Write-Ok  ("  ALL GOOD  ·  {0} passed, {1} warnings" -f $pass, $warn) }
else             { Write-Bad ("  {0} FAILURE(S)  ·  {1} passed, {2} warnings" -f $fail, $pass, $warn); Write-Bad ("  failed: " + ($failIds -join ', ')) }
if ($warn -gt 0 -and $fail -eq 0) { Write-Warn "  (warnings are heads-ups, not breakages - review them)" }
Write-Host "========================================================================" -ForegroundColor White
Write-Host ("RESULT pass={0} fail={1} warn={2}" -f $pass, $fail, $warn)

exit $fail
