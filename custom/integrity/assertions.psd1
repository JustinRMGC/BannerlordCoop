# =============================================================================
#  FONZA-CUSTOM  ·  integrity assertions  (machine-readable)
#  NOT part of upstream Bannerlord-Coop. Owned by this fork.
#
#  Each assertion describes an UPSTREAM symbol that source/Coop.Core/Server/Admin/
#  ConsoleControlBridge.cs depends on. After pulling upstream, verify-integrity.ps1
#  greps the (freshly merged) upstream source to confirm each symbol still exists.
#  If one FAILS, the bridge will not compile / a command will break — the Fragility
#  field says how likely upstream is to change it, and Breaks says what stops working.
#
#  Verification is by DECLARED NAMESPACE, not folder — e.g. TimeControl* lives under
#  Services/Time/ on disk but declares namespace ...Services.Heroes.*, and the bridge
#  binds to the namespace. If upstream "cleans up" that mismatch, our search misses ->
#  FAIL -> we get told before it silently breaks pause/resume/speed.
#
#  Ranked most-fragile first. Regex patterns are intentionally loose (match the member
#  name + an opening paren / keyword) to avoid false alarms on formatting changes.
#  See /custom/CLAUDE.md.
# =============================================================================
@{
    # Where each project's source lives, relative to repo root.
    SearchRoots = @{
        GameInterface = 'source/GameInterface'
        CoopCore      = 'source/Coop.Core'
        Common        = 'source/Common'
    }

    # The file this fork owns that consumes all of the below.
    Consumer = 'source/Coop.Core/Server/Admin/ConsoleControlBridge.cs'

    Assertions = @(
        @{
            Id = 'TIMECONTROL-ENUM'; Fragility = 'critical'; Root = 'GameInterface'
            Namespace = 'GameInterface.Services.Heroes.Enum'
            TypePattern = 'enum\s+TimeControlEnum'
            Members = @('\bPause\b', '\bPlay_1x\b', '\bPlay_2x\b')
            Breaks = 'pause / resume / speed commands'
            Note   = 'Declared ns is *.Heroes.Enum but file sits under Services/Time/Enum/. An upstream namespace cleanup is the single most likely break.'
        },
        @{
            Id = 'TIMECONTROL-IFACE'; Fragility = 'critical'; Root = 'GameInterface'
            Namespace = 'GameInterface.Services.Heroes.Interaces'
            TypePattern = 'interface\s+ITimeControlInterface'
            Members = @('ServerSetTimeControl\s*\(')
            Breaks = 'pause / resume / speed commands'
            Note   = 'Note the misspelling "Interaces" in the declared namespace; an upstream spelling fix breaks the bridge''s using.'
        },
        @{
            Id = 'CONNECTIONCOLLECTION-STATES'; Fragility = 'high'; Root = 'CoopCore'
            Namespace = 'Coop.Core.Server.Connections'
            TypePattern = 'class\s+ConnectionCollection'
            Members = @('ConnectionStates')
            Breaks = 'live player list (peer enumeration)'
            Note   = 'Concrete-type downcast reading a public property that exposes the internal peer dictionary. Bridge has an IEnumerable fallback, so a rename is a COMPILE break, not a runtime crash.'
        },
        @{
            Id = 'SAVEDEBUG-FORCEAUTOSAVE'; Fragility = 'high'; Root = 'GameInterface'
            Namespace = 'GameInterface.Services.Save.Commands'
            TypePattern = 'class\s+SaveDebugCommand'
            Members = @('ForceAutoSave\s*\(')
            Breaks = 'save command'
            Note   = 'Debug command scaffolding is prone to being renamed/removed/relocated.'
        },
        @{
            Id = 'CONNECTIONLOGIC-MEMBERS'; Fragility = 'medium'; Root = 'CoopCore'
            Namespace = 'Coop.Core.Server.Connections'
            TypePattern = 'interface\s+IConnectionLogic'
            Members = @('\bPeer\b', '\bState\b')
            Breaks = 'player list + kick (peer/state lookup)'
            Note   = 'Connection-state subsystem is actively developed.'
        },
        @{
            Id = 'CONNECTIONCOLLECTION-IFACE'; Fragility = 'medium'; Root = 'CoopCore'
            Namespace = 'Coop.Core.Server.Connections'
            TypePattern = 'interface\s+IConnectionCollection'
            Members = @('IEnumerable<\s*IConnectionLogic\s*>')
            Breaks = 'player list (fallback enumeration path)'
            Note   = ''
        },
        @{
            Id = 'OBJECTMANAGER-TRYGET'; Fragility = 'low-medium'; Root = 'GameInterface'
            Namespace = 'GameInterface.Services.ObjectManager'
            TypePattern = 'interface\s+IObjectManager'
            Members = @('TryGetObject<')
            Breaks = 'rename command (hero/clan lookup)'
            Note   = ''
        },
        @{
            Id = 'PLAYERMANAGER-TRYGET'; Fragility = 'low-medium'; Root = 'GameInterface'
            Namespace = 'GameInterface.Services.Players'
            TypePattern = 'interface\s+IPlayerManager'
            Members = @('TryGetPlayer\s*\(')
            Breaks = 'player list, kick, rename (player lookup)'
            Note   = 'Bridge uses BOTH overloads: TryGetPlayer(NetPeer,out Player) and TryGetPlayer(string,out Player).'
        },
        @{
            Id = 'PLAYER-FIELDS'; Fragility = 'low'; Root = 'GameInterface'
            Namespace = 'GameInterface.Services.Players.Data'
            TypePattern = 'class\s+Player'
            Members = @('\bControllerId\b', '\bHeroId\b', '\bClanId\b')
            Breaks = 'player rows (id/hero/clan columns) + rename'
            Note   = ''
        },
        @{
            Id = 'GAMESTATE-GOTOMAINMENU'; Fragility = 'low'; Root = 'GameInterface'
            Namespace = 'GameInterface.Services.GameState.Interfaces'
            TypePattern = 'interface\s+IGameStateInterface'
            Members = @('GoToMainMenu\s*\(')
            Breaks = 'menu command'
            Note   = ''
        },
        @{
            Id = 'CONTAINERPROVIDER-TRYRESOLVE'; Fragility = 'very-low'; Root = 'GameInterface'
            Namespace = 'namespace GameInterface'
            TypePattern = 'class\s+ContainerProvider'
            Members = @('TryResolve<')
            Breaks = 'EVERYTHING (all services resolve through this)'
            Note   = 'Core infrastructure; very unlikely to change but catastrophic if it does.'
        },
        @{
            Id = 'IUPDATEABLE'; Fragility = 'very-low'; Root = 'Common'
            Namespace = 'namespace Common'
            TypePattern = 'interface\s+IUpdateable'
            Members = @('Update\s*\(', '\bPriority\b')
            Breaks = 'the entire bridge (it IS an IUpdateable)'
            Note   = 'Also relied on: Common.UpdateableList.Add(IUpdateable).'
        },
        @{
            Id = 'LOGMANAGER'; Fragility = 'very-low'; Root = 'Common'
            Namespace = 'Common.Logging'
            TypePattern = 'class\s+LogManager'
            Members = @('GetLogger<')
            Breaks = 'bridge logging (not gameplay)'
            Note   = ''
        }
    )

    # Runtime-only risk: these services must stay REGISTERED in the Autofac container,
    # or TryResolve returns false and the command silently reports "unavailable" (no
    # compile error). The probe greps for an Autofac registration of each interface.
    DiRegistrations = @(
        @{ Interface = 'IConnectionCollection'; Breaks = 'player list' },
        @{ Interface = 'IPlayerManager';        Breaks = 'player list / kick / rename' },
        @{ Interface = 'IObjectManager';         Breaks = 'rename' },
        @{ Interface = 'ITimeControlInterface';  Breaks = 'pause / resume / speed' },
        @{ Interface = 'IGameStateInterface';    Breaks = 'menu' }
    )

    # Game-DLL symbols (TaleWorlds.* / LiteNetLib). Stable per game version; can't be
    # source-checked here (they live in compiled DLLs), so they are MANUAL / build-only.
    GameDllSymbols = @(
        'TaleWorlds.Localization.TextObject(string, Dictionary<string,object>)  [bridge builds new TextObject(name, null)]',
        'TaleWorlds.CampaignSystem.Hero.SetName(TextObject, TextObject) ; Hero.Name / Clan.Name return TextObject ; Campaign.Current',
        'LiteNetLib.NetPeer.Disconnect()  [kick]'
    )

    # Build-level invariant this fork owns (checked against our own files, not upstream).
    BuildInvariants = @(
        @{ Id = 'LOCALIZATION-REF'; File = 'source/Directory.Build.targets'; Pattern = 'TaleWorlds\.Localization'; Breaks = 'Coop.Core compile (bridge TextObject)'; Note = 'The reference was relocated here from Coop.Core.csproj; it must stay, guarded to Coop.Core.' }
    )
}
