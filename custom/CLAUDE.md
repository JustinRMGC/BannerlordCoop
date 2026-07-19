# custom/ — the Fonza fork's own code (READ ME FIRST)

> This folder and everything it documents is **FONZA-CUSTOM**: added by this fork
> (owner **JustinRMGC**). It is **not** part of upstream `Bannerlord-Coop-Team/BannerlordCoop`.
> This file is the single "everything you need to know" doc. Exact inventory lives in
> [`MANIFEST.md`](./MANIFEST.md); deep architecture in
> [`../.claude/skills/coop-upstream-sync/references/architecture.md`](../.claude/skills/coop-upstream-sync/references/architecture.md).

## TL;DR

- We cloned a repo we don't own and added two things: the **Fonza Launcher** (an external
  WinForms server-console `.exe`) and an **in-mod bridge** it talks to.
- All custom code is **isolated** so upstream pulls almost never touch it. It's identifiable by the
  `FONZA-CUSTOM` marker (grep it) and listed in `MANIFEST.md`.
- To pull upstream safely: **`git merge`**, then run the **integrity check**. Easiest path is the
  `/coop-upstream-sync` skill, or `custom/scripts/sync-upstream.ps1`.
- The mod builds **only in Visual Studio**, not the `dotnet` CLI.

---

## 1. What's custom (and where it lives)

| Piece | Location | Notes |
|---|---|---|
| **Fonza Launcher** (control panel `.exe`) | `custom/CoopServerConsole/` (9 files + `.csproj`) | net472 WinForms, `AssemblyName = FonzaLauncher`. Standalone. |
| Launcher solution | `custom/FonzaLauncher.sln` | Build the launcher from here — **not** `Coop.sln`. |
| Launcher build props | `custom/Directory.Build.props` | Feeds the launcher `CoopVersion` + `CoopShareDir` after it moved out of `source/`. |
| **In-mod bridge** | `source/Coop.Core/Server/Admin/ConsoleControlBridge.cs` | Must stay here — it *compiles into* Coop.Core (see §5). New file, banner-marked. |
| Build isolation | `source/Directory.Build.targets` | NEW file (upstream has none). Holds the `TaleWorlds.Localization` ref (Coop.Core only), `CoopShareDir`, and the share-folder sync. |
| **The one in-place edit** | `source/Coop/CoopMod.cs` | 10 server-only lines in `NoHarmonyLoad()`, wrapped in `// >>> FONZA-CUSTOM START/END`. |
| Tooling | `custom/scripts/`, `custom/integrity/` | Provenance + integrity + sync scripts (§6). |

**Everything else in `source/` is upstream and should stay byte-for-byte upstream.**

### How to tell custom from upstream, instantly
1. **Marker:** every custom file starts with a `// === FONZA-CUSTOM` banner; the lone `CoopMod.cs`
   edit is wrapped in `FONZA-CUSTOM START/END`. Run `git grep -n FONZA-CUSTOM`.
2. **Manifest:** `custom/MANIFEST.md` lists every custom file + the one edit.
3. **Live:** `powershell -ExecutionPolicy Bypass -File custom\scripts\list-custom.ps1`.
4. **Diff vs upstream:** `git diff $(git merge-base upstream/development HEAD)..HEAD`.

## 2. Why it's built this way

We don't own upstream, so we must (a) always know what's ours, and (b) keep our changes when we pull
theirs. The strategy:

- **New files can't conflict.** Anything upstream doesn't have (the whole launcher, the bridge, the
  `Directory.Build.targets`, the tooling) is safe across pulls by construction — git only conflicts on
  files *both* sides changed.
- **We eliminated 4 of the original 5 in-place edits** by moving them into new files (see
  `MANIFEST.md` §"Eliminated edits"). Those 4 upstream files are now pristine again.
- **The 1 remaining edit** (`CoopMod.cs`) is tiny, additive, and marker-wrapped, so `git merge`
  re-applies it via 3-way merge and, if it ever conflicts, it's obvious what to keep.

## 3. Build & run

- **Open in Visual Studio** (the `dotnet` CLI can't build the mod — game-DLL resolution + a NuGet audit
  error). The game is linked via the `mb2` junction → your Bannerlord install; `Deploy.targets` copies
  the built module into `mb2\Modules\Coop` on every build.
- **The mod:** open `source/Coop.sln`, build. Requires the matching game version (the mod's
  `SubModule.xml` pins it).
- **The launcher:** open `custom/FonzaLauncher.sln`, build → `FonzaLauncher.exe`. It self-deploys next to
  the module and refreshes the share folder. It's a single portable exe (WinForms ships with Win10/11).
- **Version:** `CoopVersion` (currently `0.0.2`) lives in `source/Directory.Build.props`; the launcher
  inherits it via `custom/Directory.Build.props`.
- **Share folder:** every build syncs the deployed module into `%USERPROFILE%\Desktop\Coop-Mod-Share`
  (override with `-p:CoopShareDir=...`; no-ops if the folder doesn't exist).

> The **player list + admin actions need the mod rebuilt in VS** (so the bridge ships). Launch/logs/process
> features work regardless. Launcher and server must be on the **same machine** (they share files).

## 4. How to UPDATE from upstream (the important part)

Chosen workflow = **git merge** (no history rewrite; git reapplies our edit for us).

### The easy way
Run the skill: **`/coop-upstream-sync`** (or just tell Claude "update from upstream / pull the repo").
It fetches, merges, resolves conflicts *keeping the FONZA-CUSTOM block*, runs the integrity check, fixes
mechanical breakages, and reports. It never commits or pushes for you.

### The manual way
```powershell
# from repo root, on branch server-manager, with a CLEAN working tree:
powershell -ExecutionPolicy Bypass -File custom\scripts\sync-upstream.ps1
#   -> git fetch upstream; shows incoming; git merge upstream/development; runs verify-integrity.ps1
```
Or by hand:
```
git fetch upstream
git log --oneline HEAD..upstream/development      # what's coming
git merge upstream/development
#   conflicts? almost always just source/Coop/CoopMod.cs -> keep the whole
#   >>> FONZA-CUSTOM START ... <<< END block, re-applied on top of upstream's new code.
powershell -ExecutionPolicy Bypass -File custom\scripts\verify-integrity.ps1
```

### What "reapply my changes after a pull" means here
You do **not** manually strip/re-add anything. The custom files just sit there (new files); the one
`CoopMod.cs` edit is reapplied automatically by git's merge. If a merge conflict ever appears, the
`FONZA-CUSTOM START/END` markers show exactly what to preserve.

### After updating — always verify
`verify-integrity.ps1` confirms the custom code still binds to the (possibly-changed) upstream API. A
green run is a strong signal; a **real VS build is the final word**. See §6.

## 5. Why the bridge can't move to custom/

`CoopMod.cs` does `new ConsoleControlBridge()`, so the bridge type must exist in an assembly the mod
compiles/loads. `Coop.Core` is SDK-style, so dropping the `.cs` in
`source/Coop.Core/Server/Admin/` compiles it in with **no `.csproj` edit**. Moving it under `custom/`
would need either a `.csproj` edit (an upstream change we don't want) or a fragile MSBuild `Compile`
injection. It's already a new, pull-safe, banner-marked file, so it stays. (If you ever *insist* on
relocating it, the injection route via `source/Directory.Build.targets` is possible — ask Claude.)

## 6. Integrity check — what it protects against

The bridge calls ~15 upstream symbols. If upstream renames/moves/removes one, the bridge breaks. The
checker greps the freshly-merged source to confirm each still exists.

```
powershell -ExecutionPolicy Bypass -File custom\scripts\verify-integrity.ps1        # symbol + provenance checks
powershell -ExecutionPolicy Bypass -File custom\scripts\verify-integrity.ps1 -Build  # + best-effort Coop.Core compile
```

- Data: `custom/integrity/assertions.psd1` (ranked most-fragile-first). Human catalog:
  `../.claude/skills/coop-upstream-sync/references/integrity-assertions.md`.
- **#1 fragility:** `TimeControlEnum` / `ITimeControlInterface` declare namespace
  `GameInterface.Services.Heroes.*` but live under `Services/Time/` on disk (and `Interaces` is
  misspelled). If upstream cleans that up, **pause/resume/speed silently break** — the check catches it.
- **Silent-failure (runtime) risk:** the bridge resolves services from Autofac. If upstream drops a DI
  registration, a command reports "unavailable" with *no compile error*. The checker's DI probe warns on this.
- **Game-DLL symbols** (`TextObject`, `Hero.SetName`, `NetPeer.Disconnect`, …) can't be grep-verified;
  only a real build confirms them.

Exit code = number of failures (0 = good); final line is `RESULT pass=.. fail=.. warn=..`.

## 7. Runtime model (launcher ↔ bridge)

The launcher process is **not** loaded into the game. It launches `Bannerlord.exe`, tails the mod's logs,
and exchanges files with the bridge in the game's working dir:
- `coop_console_status.txt` — bridge writes ~1×/s: state + player rows (`controllerId|name|clan|state`).
- `coop_console_command.txt` — launcher writes a command (`save`/`pause`/`resume`/`speed`/`menu`/`kick <id>`/`rename <id> <name>`); bridge polls ~4×/s and deletes it.
- `coop_console_ack.txt` — bridge writes the result (`ok=1|0`, `msg=…`).

The bridge is server-only, registered in `CoopMod.NoHarmonyLoad()`, and fully try/caught — a bridge
failure never disturbs normal play.

## 8. Gotchas

- **Don't edit upstream files.** If unavoidable, mark it `FONZA-CUSTOM`, wrap it in START/END markers, and
  record it in `MANIFEST.md` **and** `Get-FonzaEditedFiles` in `custom/scripts/Common.ps1` so the checker
  tracks it.
- **Keep `Coop.sln` pristine.** The launcher has its own `custom/FonzaLauncher.sln`. If VS re-adds the
  launcher or x86 noise to `Coop.sln`, discard that change.
- **Nothing here auto-commits.** Scripts and the skill only fetch/merge/verify; you review and commit.
- **`custom/Directory.Build.props`** imports `source/Directory.Build.props` for `CoopVersion` — if upstream
  ever relocates that file, the import no-ops (guarded by `Exists`) and the launcher just loses its version
  stamp (harmless).

## 9. Pointers

- Exact inventory + the CoopMod.cs diff: [`MANIFEST.md`](./MANIFEST.md)
- The update/verify skill: `/coop-upstream-sync` (`../.claude/skills/coop-upstream-sync/SKILL.md`)
- Architecture deep-dive: `../.claude/skills/coop-upstream-sync/references/architecture.md`
- Assertion catalog: `../.claude/skills/coop-upstream-sync/references/integrity-assertions.md`
- Scripts: `custom/scripts/list-custom.ps1`, `verify-integrity.ps1`, `sync-upstream.ps1`, `Common.ps1`
