---
name: coop-upstream-sync
description: >-
  Update this Bannerlord-Coop fork from upstream and verify/repair the fork's custom code
  (the "Fonza Launcher" server console + the in-mod ConsoleControlBridge). Use whenever the
  user pulls/merges upstream, says "update from the repo/upstream", says a pull broke the
  launcher/bridge, or asks to check that the custom (FONZA-CUSTOM) code still compiles against
  the mod. It drives `git fetch` + `git merge upstream/development`, resolves conflicts while
  preserving the FONZA-CUSTOM blocks, then runs the integrity checker and proposes fixes.
  Never auto-commits or pushes.
---

# Coop upstream sync + custom-code integrity

This fork (owner: JustinRMGC) layers custom code on top of `Bannerlord-Coop-Team/BannerlordCoop`.
Your job when this skill runs: **bring the fork up to date with upstream and make sure every
custom piece still works** — surgically, preserving the fork's work, and reporting clearly.

Read `custom/CLAUDE.md` and `custom/MANIFEST.md` first if you have not this session — they are the
source of truth for what is custom. Marker token everywhere: **`FONZA-CUSTOM`**.

## What "custom" means here (quick map)

- **Never conflicts on a pull** (new files upstream doesn't have): everything under `custom/`
  (the launcher `custom/CoopServerConsole/`, `custom/FonzaLauncher.sln`, `custom/Directory.Build.props`,
  the tooling), plus `source/Directory.Build.targets` and the bridge
  `source/Coop.Core/Server/Admin/ConsoleControlBridge.cs`.
- **The ONE in-place edit that a pull can touch**: `source/Coop/CoopMod.cs` — a 10-line, server-only
  block in `NoHarmonyLoad()` wrapped in `// >>> FONZA-CUSTOM START` … `// <<< FONZA-CUSTOM END`.
- **Reverted-to-pristine upstream files** (their custom content moved into the new files above):
  `Deploy.targets`, `source/Directory.Build.props`, `source/Coop.Core/Coop.Core.csproj`, `source/Coop.sln`.

## Decide where to start

1. If the user already merged/pulled (a merge is done, or `git status` shows a completed merge, or they
   just say "check integrity") → **skip to Phase 3 (Verify)**.
2. If a merge is **in progress with conflicts** (`git ls-files -u` non-empty) → **go to Phase 2 (Resolve)**.
3. Otherwise (they want to update) → start at **Phase 1 (Update)**.

Always confirm the current branch is the fork's working branch (default `server-manager`) and that an
`upstream` remote exists before merging.

## Phase 1 — Update from upstream

Prefer the wrapper (it refuses to run on a dirty tree and stops safely on conflict):

```
powershell -ExecutionPolicy Bypass -File custom\scripts\sync-upstream.ps1
```

Or drive it yourself:
- If the working tree is dirty, **stop** and ask the user to commit or stash (never mix their WIP into an
  upstream merge). Do not stash on their behalf without saying so.
- `git fetch upstream`
- Show them what's incoming: `git log --oneline HEAD..upstream/development` and `git diff --stat HEAD..upstream/development`.
- `git merge --no-edit upstream/development`

If the merge is clean → Phase 3. If it reports conflicts → Phase 2.

## Phase 2 — Resolve conflicts (fan out subagents)

New custom files never conflict, so a conflict is almost always in `source/Coop/CoopMod.cs` (our one edit)
or in a file upstream heavily refactored.

- List conflicts: `git diff --name-only --diff-filter=U`.
- **Spawn one subagent per conflicted file** (in parallel) with this instruction: "Resolve the git merge
  conflict in `<file>`. Take upstream's changes as the base. **Preserve the entire
  `// >>> FONZA-CUSTOM START` … `// <<< FONZA-CUSTOM END` block** (and the marked `using` line) — re-apply
  our additive block on top of upstream's new code. Our block only *adds* a server-only
  `Updateables.Add(new ConsoleControlBridge())` in `NoHarmonyLoad()`; it must remain server-guarded and
  after `Updateables.Add(GameThread.Instance)`. Output the fully merged file." Review each result, write it,
  `git add` it.
- If a conflict is in a file we thought was pristine (e.g. `Coop.sln`), that means upstream changed it — just
  take upstream's version (`git checkout --theirs`) since our customization no longer lives there; then
  confirm in Phase 3 that our replacement still holds.
- Finish the merge only after all conflicts are staged. Do **not** commit automatically unless the user asks;
  a merge that's mid-resolution can be left staged for them to review, or committed on explicit request.

## Phase 3 — Verify integrity (the core)

```
powershell -ExecutionPolicy Bypass -File custom\scripts\verify-integrity.ps1
```

(Add `-Build` to also attempt a best-effort `Coop.Core` compile — needs VS/msbuild; skips cleanly if absent.)

Interpret the output:
- Exit code / final `RESULT pass=.. fail=.. warn=..` line tells you the state. **0 failures = the custom code
  still binds to upstream.**
- **FAIL** = an upstream symbol the bridge depends on moved/renamed/vanished → the bridge won't compile or a
  command is broken. The report names the assertion Id, the reason, and what breaks.
- **WARN (DI probe)** = a service registration wasn't found by grep → a command may *silently* report
  "unavailable" at runtime even though it compiles. Worth a look, not necessarily broken.

The assertion catalog (with fragility + what-breaks) is in
`.claude/skills/coop-upstream-sync/references/integrity-assertions.md`; the machine-readable source is
`custom/integrity/assertions.psd1`.

## Phase 4 — Diagnose + fix failures (fan out subagents)

For each FAIL, **spawn a subagent** to locate the change and propose the minimal fix. Give it the assertion
(from `assertions.psd1`) and the bridge's usage. Typical outcomes:

- **Namespace/type moved or renamed** (most likely: `TimeControlEnum` / `ITimeControlInterface` — declared
  under `...Services.Heroes.*` but sitting in `Services/Time/`; upstream may "fix" that). Fix = update the
  `using` / type reference in `source/Coop.Core/Server/Admin/ConsoleControlBridge.cs`, **and** update the
  matching `Namespace`/`TypePattern` in `custom/integrity/assertions.psd1` so the check tracks reality.
- **Member signature changed** (e.g. `SaveDebugCommand.ForceAutoSave`, `ITimeControlInterface.ServerSetTimeControl`)
  → adapt the call site in the bridge; update the assertion's `Members` pattern if needed.
- **Type made internal / removed** → find the new public equivalent; if none, the affected command must be
  rewritten or disabled — surface this to the user, don't guess.
- **DI WARN** → check whether the service is still registered (search the Autofac modules / assembly scanning);
  if genuinely dropped, note which command degrades.

**Autonomy rule:** auto-apply *mechanical* fixes (a moved namespace, a renamed member with an obvious 1:1
replacement) and re-run the verifier to confirm. **Pause and ask** for anything ambiguous, anything that
changes behavior, or anything requiring a command to be rewritten/removed. **Never** `git commit`, `git push`,
`git reset --hard`, force-push, or edit files outside the fork's custom set without saying so.

## Phase 5 — Report

Give the user a tight summary:
- what upstream brought in (commit count / notable areas),
- whether the `CoopMod.cs` edit survived the merge (marker present),
- integrity result (pass/fail/warn) and any fixes you applied (file + what changed),
- anything that needs their decision or a VS build to confirm,
- remind them nothing was committed — they review and commit.

## Guardrails

- This is a fork of code the user does not own. Keep upstream files pristine except the single marked
  `CoopMod.cs` block. If you must edit an upstream file, mark it `FONZA-CUSTOM` and record it in
  `custom/MANIFEST.md` + `Get-FonzaEditedFiles` in `custom/scripts/Common.ps1`.
- The mod only builds in **Visual Studio** (see `custom/CLAUDE.md`), so a green integrity check is a strong
  signal but a real VS build is the final word. Say so when relevant.
- Same-machine assumption: launcher ↔ bridge talk via files in the game working dir.
