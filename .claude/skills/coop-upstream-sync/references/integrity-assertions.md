# Integrity assertions — human-readable catalog

The in-mod bridge (`source/Coop.Core/Server/Admin/ConsoleControlBridge.cs`)
binds to a set of **upstream** symbols. When we merge upstream, any of those can
be renamed, moved, or deleted — which would break the bridge, sometimes without
a compile error. `custom/integrity/assertions.psd1` catalogues each dependency
so `custom/scripts/verify-integrity.ps1` can confirm, right after a pull, that
it still exists.

**This file is the human-readable companion to
[`custom/integrity/assertions.psd1`](/custom/integrity/assertions.psd1).** The
`.psd1` is the source of truth; if the two disagree, the `.psd1` wins.

See also [`architecture.md`](./architecture.md) and
[`/custom/CLAUDE.md`](/custom/CLAUDE.md).

---

## How verification works

For each assertion the checker searches `.cs` files under the relevant project
root (`source/GameInterface`, `source/Coop.Core`, or `source/Common`) and
requires three things, in order:

1. a file **declaring the expected namespace** (`namespace <ns>`),
2. within those files, the **type** (`TypePattern`, e.g. `enum TimeControlEnum`),
3. within that type's file(s), every listed **member** (loose regex — matches
   the member name plus an opening paren/keyword, so formatting changes don't
   false-alarm).

**Verification is by DECLARED NAMESPACE, not by folder.** This matters: the
bridge's `using` directives bind to namespaces, so the check must too. Where a
file's on-disk folder disagrees with its declared namespace, the assertion
follows the **declared namespace** — see the #1 risk below.

Assertions are ordered **most-fragile-first** and that order is preserved here.
Fragility is a judgement of how likely upstream is to change the symbol, not how
bad the break is (a `very-low`-fragility symbol like `ContainerProvider` is
catastrophic if it ever *does* change).

---

## ⚠️ #1 risk: the TimeControl namespace/folder mismatch

The two most fragile assertions, `TIMECONTROL-ENUM` and `TIMECONTROL-IFACE`,
guard the pause / resume / speed commands. They are dangerous for a subtle
reason:

- `TimeControlEnum` and `ITimeControlInterface` **declare the namespaces
  `GameInterface.Services.Heroes.Enum` and `GameInterface.Services.Heroes.Interaces`**,
  but the files actually **live under `source/GameInterface/Services/Time/` on
  disk** — the namespace does not match the folder.
- The interface namespace also contains a **misspelling: `Interaces`** (not
  `Interfaces`).

The bridge's `using GameInterface.Services.Heroes.Enum;` and
`using GameInterface.Services.Heroes.Interaces;` bind to those exact (misspelled)
namespaces. So the single most likely upstream break is a **routine cleanup** —
someone moves the types under `...Services.Time.*` to match the folder, or fixes
the `Interaces` typo. Either change **compiles fine upstream** and would
**silently break pause/resume/speed** in the bridge.

The assertions intentionally search for the *current, misspelled, folder-mismatched*
namespace. If upstream cleans it up, the search finds nothing and the check
**FAILs loudly — before the silent breakage ships**. If a failure fires here,
update the bridge's `using` directives (and these assertions) to the new
namespace.

---

## Assertion catalog (most-fragile-first)

Each entry lists the upstream symbol as `namespace :: type :: member(s)`.

### 1. `TIMECONTROL-ENUM` — fragility: **critical**

- **Symbol:** `GameInterface.Services.Heroes.Enum :: enum TimeControlEnum :: Pause, Play_1x, Play_2x`
- **Used for:** `DoTime()` selects `TimeControlEnum.Pause` / `Play_1x` / `Play_2x` for the pause/resume/speed verbs.
- **What breaks:** pause / resume / speed commands.
- **Note:** Declared namespace is `*.Heroes.Enum` but the file sits under `Services/Time/Enum/`. An upstream namespace cleanup is the single most likely break. (See #1 risk above.)

### 2. `TIMECONTROL-IFACE` — fragility: **critical**

- **Symbol:** `GameInterface.Services.Heroes.Interaces :: interface ITimeControlInterface :: ServerSetTimeControl(...)`
- **Used for:** `DoTime()` resolves `ITimeControlInterface` and calls `ServerSetTimeControl(mode)`.
- **What breaks:** pause / resume / speed commands.
- **Note:** The declared namespace contains the misspelling **"Interaces"**; an upstream spelling fix breaks the bridge's `using`. (See #1 risk above.)

### 3. `CONNECTIONCOLLECTION-STATES` — fragility: **high**

- **Symbol:** `Coop.Core.Server.Connections :: class ConnectionCollection :: ConnectionStates`
- **Used for:** `EnumerateConnections()` downcasts `IConnectionCollection` to the concrete `ConnectionCollection` and reads `ConnectionStates` (the live `NetPeer → IConnectionLogic` dictionary) to build the player list.
- **What breaks:** live player list (peer enumeration).
- **Note:** Concrete-type downcast reading a public property that exposes the internal peer dictionary. The bridge has an `IEnumerable` fallback, so a rename is a **compile** break, not a runtime crash.

### 4. `SAVEDEBUG-FORCEAUTOSAVE` — fragility: **high**

- **Symbol:** `GameInterface.Services.Save.Commands :: class SaveDebugCommand :: ForceAutoSave(...)`
- **Used for:** `DoSave()` calls `SaveDebugCommand.ForceAutoSave(new List<string>())`.
- **What breaks:** save command.
- **Note:** Debug-command scaffolding is prone to being renamed/removed/relocated.

### 5. `CONNECTIONLOGIC-MEMBERS` — fragility: **medium**

- **Symbol:** `Coop.Core.Server.Connections :: interface IConnectionLogic :: Peer, State`
- **Used for:** `EnumerateConnections()` reads `.Peer`; the status writer reads `.State` (stripped to a display name) per connection.
- **What breaks:** player list + kick (peer/state lookup).
- **Note:** The connection-state subsystem is actively developed.

### 6. `CONNECTIONCOLLECTION-IFACE` — fragility: **medium**

- **Symbol:** `Coop.Core.Server.Connections :: interface IConnectionCollection :: IEnumerable<IConnectionLogic>`
- **Used for:** the fallback path in `EnumerateConnections()` iterates the collection as `IEnumerable<IConnectionLogic>` when the concrete downcast isn't available.
- **What breaks:** player list (fallback enumeration path).

### 7. `OBJECTMANAGER-TRYGET` — fragility: **low-medium**

- **Symbol:** `GameInterface.Services.ObjectManager :: interface IObjectManager :: TryGetObject<...>`
- **Used for:** `ResolveHeroName()` / `ResolveClanName()` / `DoRename()` call `TryGetObject<Hero>` and `TryGetObject<Clan>` to look up names by id.
- **What breaks:** rename command (hero/clan lookup).

### 8. `PLAYERMANAGER-TRYGET` — fragility: **low-medium**

- **Symbol:** `GameInterface.Services.Players :: interface IPlayerManager :: TryGetPlayer(...)`
- **Used for:** player enumeration, `DoKick()`, and `DoRename()` map peers/ids to `Player`.
- **What breaks:** player list, kick, rename (player lookup).
- **Note:** The bridge uses **both** overloads — `TryGetPlayer(NetPeer, out Player)` and `TryGetPlayer(string, out Player)`.

### 9. `PLAYER-FIELDS` — fragility: **low**

- **Symbol:** `GameInterface.Services.Players.Data :: class Player :: ControllerId, HeroId, ClanId`
- **Used for:** status rows print `ControllerId`; `HeroId`/`ClanId` feed the hero/clan name lookups; `DoKick`/`DoRename` match on `ControllerId`/`HeroId`.
- **What breaks:** player rows (id/hero/clan columns) + rename.

### 10. `GAMESTATE-GOTOMAINMENU` — fragility: **low**

- **Symbol:** `GameInterface.Services.GameState.Interfaces :: interface IGameStateInterface :: GoToMainMenu(...)`
- **Used for:** `DoMenu()` resolves `IGameStateInterface` and calls `GoToMainMenu()`.
- **What breaks:** menu command.

### 11. `CONTAINERPROVIDER-TRYRESOLVE` — fragility: **very-low**

- **Symbol:** `namespace GameInterface :: class ContainerProvider :: TryResolve<...>`
- **Used for:** **every** service the bridge touches is obtained via `ContainerProvider.TryResolve<T>(out T)`.
- **What breaks:** EVERYTHING (all services resolve through this).
- **Note:** Core infrastructure; very unlikely to change but catastrophic if it does.

### 12. `IUPDATEABLE` — fragility: **very-low**

- **Symbol:** `namespace Common :: interface IUpdateable :: Update(...), Priority`
- **Used for:** the bridge **is** an `IUpdateable` (implements `Update`/`Priority`) and is added to the game's update loop.
- **What breaks:** the entire bridge (it IS an IUpdateable).
- **Note:** Also relied on: `Common.UpdateableList.Add(IUpdateable)`.

### 13. `LOGMANAGER` — fragility: **very-low**

- **Symbol:** `Common.Logging :: class LogManager :: GetLogger<...>`
- **Used for:** the bridge's logger is `LogManager.GetLogger<ConsoleControlBridge>()`.
- **What breaks:** bridge logging (not gameplay).

---

## Runtime-only risk: the Autofac DI-registration probe

Section 4 of the checker is different from the symbol assertions above: it
guards a failure mode that **produces no compile error**. Several services must
stay **registered in the Autofac container**, or `ContainerProvider.TryResolve<T>`
returns `false` at runtime and the corresponding command **silently reports
"unavailable"** to the launcher.

The probe greps `source/Coop.Core` and `source/GameInterface` for an explicit
Autofac registration of each interface (`As<IFace>()`, `.As(typeof(IFace))`, or
`As<IFace, …>`). Because a service could instead be registered by **assembly
scanning**, a miss is a **WARN, not a FAIL** — a heads-up to confirm the
registration by other means, not a hard breakage.

| Interface | Command that goes silently "unavailable" if unregistered |
| --- | --- |
| `IConnectionCollection` | player list |
| `IPlayerManager` | player list / kick / rename |
| `IObjectManager` | rename |
| `ITimeControlInterface` | pause / resume / speed |
| `IGameStateInterface` | menu |

---

## Game-DLL symbols (manual / build-only)

The bridge also calls into the compiled game assemblies
(`TaleWorlds.*`, `LiteNetLib`). These **cannot be source-checked** here — they
live in DLLs, not in the repo — so the checker only lists them; they are
verified for real by a **compile** (`verify-integrity.ps1 -Build`, or opening
`source/Coop.sln` in Visual Studio). They are stable per game version.

- `TaleWorlds.Localization.TextObject(string, Dictionary<string,object>)` — the bridge builds `new TextObject(name, null)` in `DoRename`.
- `TaleWorlds.CampaignSystem`: `Hero.SetName(TextObject, TextObject)`; `Hero.Name` / `Clan.Name` return `TextObject`; `Campaign.Current`.
- `LiteNetLib.NetPeer.Disconnect()` — used by `DoKick`.

---

## Build invariant owned by this fork

Section 5 checks an invariant against **our own** files (not upstream):

| Id | File | Requires | Breaks if missing |
| --- | --- | --- | --- |
| `LOCALIZATION-REF` | `source/Directory.Build.targets` | a `TaleWorlds.Localization` reference | `Coop.Core` compile (the bridge's `TextObject`) |

The `TaleWorlds.Localization` reference was **relocated here** out of
`Coop.Core.csproj` (so upstream pulls can't touch it) and must remain,
**guarded to the `Coop.Core` project** — adding it to the seven other projects
that already reference it would duplicate it and fail the build.

---

## How to run it

```
powershell -ExecutionPolicy Bypass -File custom\scripts\verify-integrity.ps1 [-Build]
```

- Run it **after every `git merge upstream/development`.**
- `-Build` additionally attempts a best-effort `Coop.Core` compile (needs
  Visual Studio / MSBuild) — the only way to validate the game-DLL symbols
  above. Without a resolvable MSBuild it degrades to a WARN.
- `-Quiet` prints only failures plus the summary line.

The script prints a colour report over six sections (custom files present →
edit markers → upstream symbols → DI probe → build invariants → optional
compile) and **exits with the number of failures** — `0` means all good, so it
is CI- and skill-friendly. Warnings (e.g. an unmatched DI registration, or a
missing MSBuild) are heads-ups and do **not** count as failures. On failure it
lists the failing assertion Ids; cross-reference them with the catalog above to
see what broke and why.
