# FONZA-CUSTOM · MANIFEST

**What this file is.** The precise, machine-and-human **source of truth** for everything this
fork adds on top of upstream Bannerlord-Coop. It inventories every wholly-custom file, the single
in-place edit made to an upstream file, and the edits that were deliberately *relocated out* of
upstream files so `git pull upstream` can never touch them. If this file and the code ever
disagree, the code wins — then fix this file (or run the verifier in section 4).

| Fact | Value |
|------|-------|
| Marker token | `FONZA-CUSTOM` — appears in the banner of every custom file and wraps every in-place edit as `// >>> FONZA-CUSTOM START` … `// <<< FONZA-CUSTOM END`. `git grep FONZA-CUSTOM` finds the entire custom surface. |
| Upstream base commit | `1d62f74a5d6286a03b396a1319ec931322079dc1` = `git merge-base upstream/development HEAD`. Everything in `git diff <base>` (committed + working tree) is ours. |
| Branch | `server-manager` |
| Remote `origin` (this fork) | `https://github.com/JustinRMGC/BannerlordCoop.git` — owned by **JustinRMGC** |
| Remote `upstream` (the team) | `https://github.com/Bannerlord-Coop-Team/BannerlordCoop.git` — **Bannerlord-Coop-Team** |

Owner: JustinRMGC. Docs: `/custom/CLAUDE.md`.

---

## 1. Custom NEW files

Files that are **100% ours** — upstream has no file at these paths. Because upstream contributes
nothing at these paths, a merge/pull **can never conflict** on them; they only ever change if *we*
change them, or vanish if someone deletes them. The build/bridge-critical set is enumerated
authoritatively in `custom/scripts/Common.ps1` as `$FonzaNewFiles` and verified for
presence on every pull.

### Launcher — `custom/CoopServerConsole/*` (the external "Fonza Launcher" control panel)

Standalone `net472` **WPF** WinExe (single portable `FonzaLauncher.exe`, **zero NuGet dependencies** —
WPF ships inside the .NET Framework already present on every Win10/11, so the exe is still zero-install).
It does **not** load into the game — it launches `Bannerlord.exe` as a child process, tails the mod's
logs, and talks to the in-mod bridge via files. **Rebuilt from WinForms → WPF (MVVM) on 2026-07-20**; the
UI-agnostic backend classes were reused byte-for-byte (only the presentation layer changed).

**App shell & bootstrap**

| Path | What it is | Purpose (one line) |
|------|-----------|--------------------|
| `App.xaml` / `App.xaml.cs` | WPF entry point | Boots the app; preserves the CLI contract: `--selftest` (construct-and-exit-0 build check) and `--tab <name>` deep-link; dark fatal handler. |
| `MainWindow.xaml` / `.cs` | Shell window | Left nav rail (brand + nav + live STATUS block), content status-header, page frame (rise+fade transition), footer status line; applies Win11 dark title bar + rounded corners. |
| `Native.cs` | DWM interop | Dark title bar + rounded corners (+ optional Mica) via `DwmSetWindowAttribute`. |
| `Ui.cs` | Attached properties | `Ui.Glyph` (a button's leading Segoe Fluent glyph) + `Ui.CornerRadius`, read by the control templates. |
| `Services.cs` | UI services | `LauncherServices` (shared cfg/launcher/bin/snapshot + `ShowToast`/`Commit`/`Confirm`/`Prompt`), `ITickable`, `ToastKind`. |

**Design system** (bespoke, zero-dependency): `Theme/Theme.xaml` (Radix-slate + indigo tokens, fonts, radii), `Theme/Controls.xaml` (all control styles/templates: buttons, inputs, toggle, segmented, card, chip, dark scrollbars, DataGrid), `Theme/Icons.xaml` (Segoe Fluent glyph codepoints).

**MVVM**: `Mvvm/ViewModelBase.cs`, `Mvvm/RelayCommand.cs`, `Mvvm/Converters.cs`.

**View-models**: `ViewModels/MainViewModel.cs` (shell — nav, single 500 ms tick, rail/header status, toast) + `DashboardViewModel`, `LogViewModel` (one class, server & client instances), `PlayersViewModel`, `SettingsViewModel` (+ `PlaceholderViewModel`).

**Views, controls & dialog**: `Views/{Dashboard,Log,Players,Settings,Placeholder}View.xaml`; `Controls/StatusPill.xaml` (tinted status dot+label); `Dialogs/FonzaDialog.xaml` (dark confirm/input modal, replaces the light system `MessageBox`).

**Reused, UI-agnostic backend** (unchanged from the WinForms build):

| Path | What it is | Purpose (one line) |
|------|-----------|--------------------|
| `custom/CoopServerConsole/GameLauncher.cs` | Process manager | Launches & tracks Bannerlord as server/client child processes using the same args/module list as the `start-*.bat` launchers. |
| `custom/CoopServerConsole/ControlChannel.cs` | Bridge client (file-based) | Parses the status/player snapshot the in-mod bridge writes (`PlayerInfo`/`StatusSnapshot`) and sends admin commands back to it. |
| `custom/CoopServerConsole/LogFollower.cs` | Log tailer | Incrementally reads newly-appended log lines with shared read/write/delete access; resets when the file shrinks or is recreated. |
| `custom/CoopServerConsole/LogInsights.cs` | Log parser | Level/noise classification for live colorized tails (+ a `ServerStatus` digest helper). |
| `custom/CoopServerConsole/AppConfig.cs` | Settings | Hand-rolled `key=value` config persisted next to the exe (`coopconsole.cfg`) so the tool stays a single dependency-free exe. |
| `custom/CoopServerConsole/Paths.cs` | Path resolver | Resolves game / module / log / save locations relative to where the exe runs, or from an explicitly configured game root. |
| `custom/CoopServerConsole/CoopServerConsole.csproj` | Project file | Standalone `net472` `WinExe` with `<UseWPF>true</UseWPF>` (AssemblyName `FonzaLauncher`); no NuGet/`ProjectReference`s; self-deploys into `mb2\Modules\Coop` + the share folder, no-op when the game junction is absent. |

### Build isolation

| Path | What it is | Purpose (one line) |
|------|-----------|--------------------|
| `source/Directory.Build.targets` | Fork-owned MSBuild auto-import | Upstream has **no** `Directory.Build.targets`; this whole file is ours. Holds the three build customizations relocated out of upstream files: `CoopShareDir` property, the `FonzaSyncShareFolder` post-deploy sync target, and the `Coop.Core`-guarded `TaleWorlds.Localization` reference. Auto-imported alongside (and independently of) upstream's `Directory.Build.props`. |
| `custom/Directory.Build.props` | MSBuild auto-import for `custom/` | Makes the launcher self-sufficient now that it lives outside `source/`: imports `CoopVersion` from `source/Directory.Build.props` (version-stamp parity) and defines `CoopShareDir`. |
| `custom/FonzaLauncher.sln` | Standalone solution | Keeps the launcher **out of** `source/Coop.sln` so upstream pulls never touch a solution file. Contains only the `CoopServerConsole` project. |

### Bridge (compiles into the mod)

| Path | What it is | Purpose (one line) |
|------|-----------|--------------------|
| `source/Coop.Core/Server/Admin/ConsoleControlBridge.cs` | Server-side `IUpdateable` | Optional server bridge pumped on the game thread: writes a live status/player file and consumes admin commands (player list / kick / rename / pause-resume-speed / save / menu) for the external launcher. A **new, banner-marked, pull-safe file** — it lives in `Coop.Core` (not `custom/`) because it must compile into the mod assembly. |

### Tooling

These files are themselves marked `FONZA-CUSTOM` but are **not** listed in `$FonzaNewFiles` — that
inventory is the build/bridge-critical set the verifier checks for *presence*, and the verifier
does not assert its own existence.

| Path | What it is | Purpose (one line) |
|------|-----------|--------------------|
| `custom/scripts/Common.ps1` | Shared PS helpers | Dot-sourced by the other scripts; **owns the authoritative inventory** (`$FonzaNewFiles`, `$FonzaEditedFiles`), the upstream-base resolver, and assertion loading. |
| `custom/scripts/verify-integrity.ps1` | Post-pull integrity check | Confirms every upstream symbol the bridge depends on still exists, all custom files are present, and the one in-place edit is still marked; exits with the failure count (CI-friendly). `-Build` also attempts a best-effort `Coop.Core` compile. |
| `custom/integrity/assertions.psd1` | Machine-readable assertions | The upstream symbols / namespaces / DI-registrations / game-DLL symbols the bridge binds to, ranked by fragility, plus build invariants — consumed by `verify-integrity.ps1`. |

---

## 2. In-place edits to UPSTREAM files — the critical section

**Exactly ONE upstream file is edited in place today:** `source/Coop/CoopMod.cs`
(authoritative: `$FonzaEditedFiles` in `custom/scripts/Common.ps1`).

- **Purpose:** server-only registration of `ConsoleControlBridge` inside `NoHarmonyLoad()`. The
  edit is **additive and fully self-guarded** — it only runs when `isServer`, and any failure is
  caught and logged so it can never disturb normal play.
- **Convention:** the whole edit is wrapped in marker comments so it is unmistakable in a merge:
  `// >>> FONZA-CUSTOM START` … `// <<< FONZA-CUSTOM END` (verified by the `FONZA-CUSTOM START`
  marker recorded in `$FonzaEditedFiles`).

> **If a future pull conflicts here, keep this whole block.**

### Exact added lines (captured from `git diff`)

Two hunks. First, the `using` directive (top of file, line 5):

```csharp
using Coop.Core.Server.Admin;   // FONZA-CUSTOM: needed by the bridge hook below (see /custom/CLAUDE.md)
```

Second, the marked block inside `NoHarmonyLoad()`, immediately after `Updateables.Add(GameThread.Instance);`:

```csharp
            // >>>>>>>>>> FONZA-CUSTOM START — server console bridge hook >>>>>>>>>>
            // The ONLY edit this fork makes to a core upstream file. Additive + self-guarded.
            // If a pull conflicts here, keep this whole block. See /custom/CLAUDE.md.
            // Server-only bridge for the external CoopServerConsole control panel: writes a live
            // status/player file and consumes admin commands. Fully guarded — a failure here must
            // never disturb normal play, so it is best-effort and swallows its own errors.
            if (isServer)
            {
                try { Updateables.Add(new ConsoleControlBridge()); }
                catch (Exception ex) { Logger.Warning(ex, "[ConsoleBridge] failed to start (ignored)"); }
            }


            // <<<<<<<<<< FONZA-CUSTOM END — server console bridge hook <<<<<<<<<<
```

---

## 3. Edits that were ELIMINATED (relocated out of upstream files)

To shrink the in-place footprint to the single edit above, three build customizations and one
solution entry that used to live *inside* upstream files were moved into wholly-new files. The
upstream files below are now **pristine** — byte-identical to the upstream base
(`git diff <base> -- <file>` is empty) — so pulls never conflict on them.

| Upstream file (now pristine) | What was removed from it | Where it lives now |
|------------------------------|--------------------------|--------------------|
| `Deploy.targets` | Share-folder sync appended to `DeployToGame` | `source/Directory.Build.targets` → `FonzaSyncShareFolder` target (`AfterTargets="DeployToGame"`) |
| `source/Directory.Build.props` | `CoopShareDir` property | `source/Directory.Build.targets` (for the mod build) **+** `custom/Directory.Build.props` (for the launcher, which no longer inherits `source/`) |
| `source/Coop.Core/Coop.Core.csproj` | `TaleWorlds.Localization` assembly reference | `source/Directory.Build.targets`, **guarded** to `'$(MSBuildProjectName)' == 'Coop.Core'` so it is added to that project only (the bridge builds `TextObject`s) |
| `source/Coop.sln` | `CoopServerConsole` project entry | `custom/FonzaLauncher.sln` (standalone; launcher project also physically moved `source/CoopServerConsole/*` → `custom/CoopServerConsole/*`) |

---

## 4. How to confirm

- **Live inventory / integrity:** `powershell -ExecutionPolicy Bypass -File custom\scripts\verify-integrity.ps1` — reads the inventory from `custom/scripts/Common.ps1` (`$FonzaNewFiles` / `$FonzaEditedFiles`) plus `custom/integrity/assertions.psd1`, confirms every custom file is present and the in-place edit is still marked, and exits with the failure count.
- **All committed custom changes:** `git diff 1d62f74a5d6286a03b396a1319ec931322079dc1..HEAD`. For the *full current* custom surface (including the in-progress relocation of the four eliminated edits, which lives in the working tree), use `git diff 1d62f74a5d6286a03b396a1319ec931322079dc1`.
- **Every marker:** `git grep FONZA-CUSTOM` — lists the banner in every custom file and the `START`/`END` wrappers around the one in-place edit.
