# Fonza fork architecture — custom-code isolation

Reference for the `coop-upstream-sync` skill. Explains how this fork keeps its
custom work cleanly separated from — and able to survive `git pull` of — the
upstream **Bannerlord-Coop** project (a Mount & Blade II: Bannerlord co-op mod).

Companion docs:

- [`/custom/CLAUDE.md`](/custom/CLAUDE.md) — day-to-day rules for working in this fork.
- [`/custom/MANIFEST.md`](/custom/MANIFEST.md) — the authoritative list of every fork-owned file.
- [`./integrity-assertions.md`](./integrity-assertions.md) — the upstream symbols the bridge depends on and what breaks if they change.

---

## 1. Why this exists

This repository is a **fork of a project we do not own**. Upstream
(`Bannerlord-Coop-Team/BannerlordCoop`, tracked here as the `upstream` remote,
branch `development`) keeps moving, and we want its fixes. But a naive fork has
two chronic problems:

1. **Merge conflicts.** Every upstream file we edit becomes a conflict on the
   next pull, and conflicts are where custom behaviour silently gets dropped.
2. **Custom code gets lost or confused with upstream.** Six months later nobody
   can tell which lines are "ours" versus vanilla, so nobody dares touch them.

The fork's design goal is therefore: **custom code must be (a) instantly
identifiable and (b) merge-safe** — a routine `git merge upstream/development`
should never conflict on our work and never quietly discard it.

## 2. The isolation strategy (four rules)

The whole scheme rests on one MSBuild/Git fact: **a merge only conflicts on
files that both sides changed. New files, and files only one side touches, merge
cleanly.** So we push nearly everything into *new* files and shrink our
in-place footprint to a single guarded block.

- **(a) Everything wholly-custom lives under the root `custom/` folder — with
  one exception, the bridge (rule below).** `custom/` does not exist upstream,
  so nothing in it can ever conflict.

- **(b) Build customizations live in NEW MSBuild files that upstream has none
  of.** Upstream ships no `source/Directory.Build.targets`, so we created one
  and moved three build tweaks *out of* upstream files into it (see §5). We also
  added `custom/Directory.Build.props` for the launcher. MSBuild auto-imports
  both by directory walk, and a pull that never touches them can never conflict.

- **(c) The launcher builds from its own solution, `custom/FonzaLauncher.sln`.**
  We never add our project to upstream's `source/Coop.sln`, so that solution
  stays byte-for-byte upstream.

- **(d) The ONLY in-place edit to an upstream source file is a small
  server-only block in `source/Coop/CoopMod.cs`** (inside `NoHarmonyLoad()`),
  wrapped in `// >>> FONZA-CUSTOM START/END` markers (see §4). Being additive
  and marker-wrapped, git's 3-way merge re-applies it automatically in the
  common case, and it's trivially spotted if a merge ever does conflict there.

**Marker token (grep for this to find everything):** `FONZA-CUSTOM`.

## 3. The two custom components

**Fonza Launcher** — an external WinForms control panel (`AssemblyName`
**FonzaLauncher**, `net472`) under `custom/CoopServerConsole/`. It launches the
Bannerlord engine as a server/client, tails the Serilog logs, shows a live
player list, and sends admin commands. It is a *separate process* — it is not
loaded by the game.

**The in-mod bridge** — `source/Coop.Core/Server/Admin/ConsoleControlBridge.cs`.
This is the server-side counterpart that runs *inside* the game and does the
actual work the launcher asks for. The two halves talk over a file channel (§6).

## 4. Why the bridge can't leave Coop.Core

The bridge is the one piece of wholly-custom code that does **not** live under
`custom/`, and that is unavoidable:

- It implements `Common.IUpdateable` and is added to the game's `Updateables`
  list, so it is **pumped on the game thread** and can touch the live campaign
  (`Campaign.Current`) directly.
- It resolves game services (`IConnectionCollection`, `IPlayerManager`,
  `IObjectManager`, `ITimeControlInterface`, `IGameStateInterface`) through
  `GameInterface.ContainerProvider`, and downcasts to
  `Coop.Core.Server.Connections.ConnectionCollection`.
- It therefore has to **compile into the mod assembly (`Coop.Core`)** — it can't
  be an external DLL or process without re-exporting large chunks of internal
  API.

So it lives at `source/Coop.Core/Server/Admin/ConsoleControlBridge.cs`, but it
is a **new file** (never conflicts on merge) carrying the `=== FONZA-CUSTOM ===`
banner, and it is registered by the marker-wrapped block in `CoopMod.cs`:

```csharp
// >>>>>>>>>> FONZA-CUSTOM START — server console bridge hook >>>>>>>>>>
// The ONLY edit this fork makes to a core upstream file. Additive + self-guarded.
if (isServer)
{
    try { Updateables.Add(new ConsoleControlBridge()); }
    catch (Exception ex) { Logger.Warning(ex, "[ConsoleBridge] failed to start (ignored)"); }
}
// <<<<<<<<<< FONZA-CUSTOM END — server console bridge hook <<<<<<<<<<
```

The same edit adds one marker-tagged `using Coop.Core.Server.Admin;` at the top
of `CoopMod.cs`. It is **server-only** (`if (isServer)`) and **self-guarded**
(a throw is logged and swallowed), so it can never disturb normal play. The
integrity check keys on the literal marker `FONZA-CUSTOM START` to confirm the
block survived the last merge.

## 5. Custom file map

### New files, 100% ours (never conflict; only vanish if deleted)

| Path | Role |
| --- | --- |
| `source/Coop.Core/Server/Admin/ConsoleControlBridge.cs` | The in-mod bridge (must compile into Coop.Core — see §4). |
| `source/Directory.Build.targets` | Build customizations relocated out of upstream files: `CoopShareDir` property, the `TaleWorlds.Localization` reference **guarded to `Coop.Core`**, and the share-folder sync target `FonzaSyncShareFolder` (`AfterTargets="DeployToGame"`). Upstream has no such file. |
| `custom/Directory.Build.props` | Auto-imported for projects under `custom/`. Imports `CoopVersion` from `source/Directory.Build.props` (so the launcher is version-stamped to match the mod) and defines `CoopShareDir` for the launcher's own deploy. |
| `custom/FonzaLauncher.sln` | The launcher's solution — keeps upstream `source/Coop.sln` pristine. |
| `custom/CoopServerConsole/CoopServerConsole.csproj` | Launcher project (`AssemblyName` = FonzaLauncher, `net472`, WinForms). |
| `custom/CoopServerConsole/Program.cs` | WinForms entry point. |
| `custom/CoopServerConsole/MainForm.cs` | The control-panel UI (Fluent / Windows-11 style) plus its custom controls. |
| `custom/CoopServerConsole/ControlChannel.cs` | Console side of the file channel: reads the status file, writes commands, reads acks. |
| `custom/CoopServerConsole/GameLauncher.cs` | Launches / tracks the Bannerlord engine as server & client child processes. |
| `custom/CoopServerConsole/Paths.cs` | Resolves game / module / log / save locations relative to the exe. |
| `custom/CoopServerConsole/LogFollower.cs` | Incrementally tails appended log lines. |
| `custom/CoopServerConsole/LogInsights.cs` | Distills meaning (server status) out of the mod's Serilog output. |
| `custom/CoopServerConsole/AppConfig.cs` | `key=value` settings persisted next to the exe (`coopconsole.cfg`). |

### Fork-owned tooling & docs (also under `custom/`)

| Path | Role |
| --- | --- |
| `custom/integrity/assertions.psd1` | Machine-readable catalog of the upstream symbols the bridge depends on. |
| `custom/scripts/verify-integrity.ps1` | Post-pull integrity checker (see [integrity-assertions.md](./integrity-assertions.md)). |
| `custom/scripts/Common.ps1` | Shared PowerShell helpers + the authoritative file inventory (`FonzaNewFiles` / `FonzaEditedFiles`). |
| `custom/CLAUDE.md` | Working rules for the fork. |
| `custom/MANIFEST.md` | Human-readable manifest of all fork-owned files. |

### In-place edits to upstream files (must survive pulls)

| Path | Edit | Verified by |
| --- | --- | --- |
| `source/Coop/CoopMod.cs` | Server-only bridge hook in `NoHarmonyLoad()` + a `using` — see §4. | Presence of marker `FONZA-CUSTOM START`. |

The inventory in `custom/scripts/Common.ps1` (`$FonzaNewFiles`,
`$FonzaEditedFiles`) is the machine-readable source of truth and is kept in sync
with `custom/MANIFEST.md`.

## 6. How to identify custom code

Three redundant, self-reinforcing signals — use any of them:

1. **Banners & markers.** Every fork-owned file starts with a banner comment
   (`// === FONZA-CUSTOM ===` for C#, `# ... FONZA-CUSTOM ...` for PowerShell/MSBuild).
   The single in-place edit is fenced with `// >>> FONZA-CUSTOM START` /
   `... END`. A repo-wide search for the token `FONZA-CUSTOM` surfaces every
   custom touchpoint, including the one edit inside an otherwise-upstream file.

2. **The manifest.** `custom/MANIFEST.md` (human) and the `$FonzaNewFiles` /
   `$FonzaEditedFiles` lists in `custom/scripts/Common.ps1` (machine) enumerate
   exactly which files are ours and which upstream files we edit.

3. **`list-custom` (enumerate against upstream).** Because all custom work sits
   *on top of* upstream, everything in the diff from the upstream merge-base to
   `HEAD` (plus the working tree) is ours. `Get-FonzaUpstreamBase` in
   `Common.ps1` computes that base as
   `git merge-base upstream/development HEAD` (falling back to
   `origin/development`), so:

   ```bash
   base=$(git merge-base upstream/development HEAD)
   git diff --stat "$base"..HEAD          # everything we changed since upstream
   git grep -n "FONZA-CUSTOM" -- source/  # our touchpoints inside upstream trees
   ```

## 7. The runtime file-channel (launcher ⇄ bridge)

The launcher and the in-mod bridge communicate **entirely through three text
files** in the engine's working directory — the game `bin` folder
(`bin/Win64_Shipping_Client/`, next to the Serilog logs). **No socket, port, or
firewall is involved**, and it is deliberately **same-machine only**.

The filenames are declared identically on both ends
(`ConsoleControlBridge.cs` constants vs. `ControlChannel.cs` constants):

| File | Constant | Written by | Read by |
| --- | --- | --- | --- |
| `coop_console_status.txt` | `StatusFileName` / `StatusFile` | bridge | launcher |
| `coop_console_command.txt` | `CommandFileName` / `CommandFile` | launcher | bridge |
| `coop_console_ack.txt` | `AckFileName` / `AckFile` | bridge | launcher |

### Cadence

- The bridge is pumped every frame; it **polls for a command every ~0.25 s**
  (`commandAccum >= 0.25`) and **rewrites the status snapshot every ~1.0 s**
  (`statusAccum >= 1.0`).
- The launcher treats status as **"live" only if it was written within the last
  6 s** (`FreshnessSeconds = 6.0`) — a stale file means the server isn't
  hosting.
- When sending a command, the launcher deletes any old ack, writes the command,
  then **polls for the ack every 150 ms up to a 5 s timeout**; on timeout it
  reports "No response from the server."

### Status snapshot (bridge → launcher)

Plain `key=value` lines the launcher parses tolerantly (unknown keys ignored):

```
# CoopServerConsole status (auto-generated by the mod)
updated=HH:mm:ss
state=Campaign running | Main menu / loading
player=<controllerId>|<name>|<clan>|<state>   (one line per connected peer)
players=<count>
```

### Command / ack round-trip (launcher → bridge → launcher)

```
launcher: delete coop_console_ack.txt
launcher: write  coop_console_command.txt   ->  "cmd=<verb> [args]"
bridge  : (within ~0.25s) read the cmd= line, DELETE the command file, dispatch it
bridge  : write coop_console_ack.txt         ->  ts=... / ok=1|0 / msg=<text>
launcher: (poll 150ms, <=5s) read the ack, DELETE it, surface ok + msg
```

Supported command verbs (`ConsoleControlBridge.Dispatch`): `save`, `pause`,
`resume`, `speed [2]`, `menu`, `kick <controllerId>`,
`rename <controllerId> <name>`. Each maps to an upstream API call catalogued in
[integrity-assertions.md](./integrity-assertions.md). Every bridge operation is
wrapped in try/catch so a failure only logs — it never disturbs the game loop.

## 8. The update workflow (git merge)

Updating from upstream is a plain merge — no cherry-picking, no rebasing our
custom files:

```bash
git fetch upstream
git merge upstream/development
```

What happens:

- **New custom files** (all of `custom/`, `ConsoleControlBridge.cs`,
  `source/Directory.Build.targets`) are untouched — upstream doesn't know them,
  so they never conflict.
- **The one in-place edit** in `source/Coop/CoopMod.cs` is re-applied by git's
  3-way merge in the common case. If upstream changed the same region, resolve
  the conflict by **keeping the entire `FONZA-CUSTOM START … END` block** (and
  the tagged `using`).
- **Then verify integrity.** Upstream may have renamed or moved a symbol the
  bridge binds to. Run the checker before trusting the build:

  ```
  powershell -ExecutionPolicy Bypass -File custom\scripts\verify-integrity.ps1 -Build
  ```

  It confirms all custom files are present, the edit marker survived, every
  upstream symbol still exists, DI registrations are intact, and (with `-Build`)
  that `Coop.Core` still compiles. See
  [integrity-assertions.md](./integrity-assertions.md) for the full catalog and
  the specific ways an upstream change can break the bridge.
