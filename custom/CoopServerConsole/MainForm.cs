// === FONZA-CUSTOM ============================================================
// Not part of upstream Bannerlord-Coop. Owned by this fork (JustinRMGC).
// Fonza Launcher / Coop server console.  Docs: /custom/CLAUDE.md
// =============================================================================

using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CoopServerConsole
{
    /// <summary>
    /// Fonza Launcher — a Fluent/Windows 11-style control panel for the Bannerlord Coop mod.
    /// Left navigation rail + card pages. Launches the game as server/client, tails logs live,
    /// and (via the in-mod bridge) manages connected players. Game/mod logic lives in the backend
    /// classes; this is the UI.
    /// </summary>
    public sealed class MainForm : Form
    {
        // ---- Fluent dark palette ----
        private static readonly Color ColBg     = Color.FromArgb(28, 28, 32);
        private static readonly Color ColNav    = Color.FromArgb(22, 22, 26);
        private static readonly Color ColCard   = Color.FromArgb(38, 38, 44);
        private static readonly Color ColInput  = Color.FromArgb(48, 48, 54);
        private static readonly Color ColBorder = Color.FromArgb(54, 54, 62);
        private static readonly Color ColText   = Color.FromArgb(240, 240, 243);
        private static readonly Color ColDim    = Color.FromArgb(154, 154, 165);
        private static readonly Color ColAccent = Color.FromArgb(76, 194, 255);
        private static readonly Color ColOk     = Color.FromArgb(87, 199, 126);
        private static readonly Color ColWarn   = Color.FromArgb(232, 184, 87);
        private static readonly Color ColBad    = Color.FromArgb(232, 106, 106);
        private static readonly Color ColLogBg  = Color.FromArgb(23, 23, 26);
        private static readonly Color ColNavSel = Color.FromArgb(40, 42, 50);
        private static readonly Color ColNavHov = Color.FromArgb(32, 33, 39);

        private static readonly FontFamily BodyFamily = PickFamily("Segoe UI Variable Text", "Segoe UI Variable", "Segoe UI");
        private static readonly FontFamily HeadFamily = PickFamily("Segoe UI Variable Display", "Segoe UI Variable", "Segoe UI Semibold", "Segoe UI");
        private static readonly FontFamily IconFamily = PickFamily("Segoe Fluent Icons", "Segoe MDL2 Assets", "Segoe UI Symbol");

        private readonly Font uiFont    = new Font(BodyFamily, 9.5f, FontStyle.Regular, GraphicsUnit.Point);
        private readonly Font navFont   = new Font(BodyFamily, 10.5f, FontStyle.Regular, GraphicsUnit.Point);
        private readonly Font titleFont = new Font(HeadFamily, 20f, FontStyle.Regular, GraphicsUnit.Point);
        private readonly Font h2Font    = new Font(HeadFamily, 13.5f, FontStyle.Regular, GraphicsUnit.Point);
        private readonly Font subFont   = new Font(BodyFamily, 8.75f, FontStyle.Regular, GraphicsUnit.Point);
        private readonly Font monoFont  = new Font(PickFamily("Cascadia Mono", "Cascadia Code", "Consolas"), 9.5f, FontStyle.Regular, GraphicsUnit.Point);
        private readonly Font iconFont  = new Font(IconFamily, 13f, FontStyle.Regular, GraphicsUnit.Point);

        private readonly AppConfig cfg;
        private readonly GameLauncher launcher;

        private LogFollower serverFollower, clientFollower;
        private bool serverFirstLoad = true, clientFirstLoad = true;
        private readonly Timer timer = new Timer();
        private int tick;

        private Panel pageHost;
        private NavButton[] navButtons;
        private Panel[] pages;

        private Label navServer, navClient, navEngine;
        private Label homeServerStat, homeServerSummary, homeClientStat;
        private RoundedButton hbStartServer, hbStopServer, hbRestart, hbStartClient, hbStopClient;

        private RichTextBox rtbServer, rtbClient;
        private DataGridView gridPlayers;
        private Label lblPlayersInfo;
        private RoundedButton btnRename, btnKick, btnSave, btnPause, btnResume, btnFast, btnMenu;

        private TextBox txtSave, txtPass, txtAddr, txtGame;
        private ComboBox cboVis;
        private ToggleSwitch tglHideDbg, tglHideNoise;
        private Label toast;

        private string Version { get { try { var v = GetType().Assembly.GetName().Version; return "v" + v.Major + "." + v.Minor + "." + v.Build; } catch { return "v0.0.0"; } } }
        private string Bin { get { return Paths.GameBinDir(cfg.GameRoot); } }

        public MainForm()
        {
            cfg = AppConfig.Load();
            launcher = new GameLauncher(cfg);
            BuildUi();
        }

        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Native.ApplyModernChrome(Handle); }

        // ---------------------------------------------------------------- shell

        private void BuildUi()
        {
            Text = "Fonza Launcher";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1060, 700);
            MinimumSize = new Size(900, 600);
            BackColor = ColBg;
            ForeColor = ColText;
            Font = uiFont;
            DoubleBuffered = true;

            var nav = BuildNav();

            var main = new Panel { Dock = DockStyle.Fill, BackColor = ColBg, Padding = new Padding(22, 18, 22, 6) };
            pageHost = new Panel { Dock = DockStyle.Fill, BackColor = ColBg };
            toast = new Label { Dock = DockStyle.Bottom, Height = 24, ForeColor = ColDim, Font = subFont, TextAlign = ContentAlignment.MiddleLeft, UseMnemonic = false, Text = "Ready." };
            main.Controls.Add(pageHost);
            main.Controls.Add(toast);

            pages = new[] { BuildHome(), BuildLogPage(true), BuildLogPage(false), BuildPlayers(), BuildSettings() };
            foreach (var p in pages) { p.Visible = false; pageHost.Controls.Add(p); }

            Controls.Add(main);
            Controls.Add(nav);

            ShowPage(0);
        }

        private Panel BuildNav()
        {
            var nav = new Panel { Dock = DockStyle.Left, Width = 224, BackColor = ColNav };

            var header = new Panel { Dock = DockStyle.Top, Height = 84, BackColor = ColNav };
            header.Controls.Add(new Label { Text = "Fonza Launcher", Font = new Font(HeadFamily, 15f), ForeColor = ColAccent, AutoSize = true, Location = new Point(18, 22), BackColor = Color.Transparent, UseMnemonic = false });
            header.Controls.Add(new Label { Text = "Bannerlord Coop  ·  " + Version, Font = subFont, ForeColor = ColDim, AutoSize = true, Location = new Point(19, 50), BackColor = Color.Transparent, UseMnemonic = false });

            var statusBlock = new Panel { Dock = DockStyle.Bottom, Height = 104, BackColor = ColNav, Padding = new Padding(18, 0, 12, 14) };
            navServer = NavStat(); navClient = NavStat(); navEngine = NavStat();
            var sflow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = ColNav };
            sflow.Controls.Add(new Label { Text = "STATUS", Font = subFont, ForeColor = ColDim, AutoSize = true, Margin = new Padding(0, 0, 0, 6), UseMnemonic = false });
            sflow.Controls.Add(navServer); sflow.Controls.Add(navClient); sflow.Controls.Add(navEngine);
            statusBlock.Controls.Add(sflow);

            var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = ColNav, Padding = new Padding(12, 10, 12, 10) };
            var items = new[]
            {
                new[] { "", "Home" },
                new[] { "", "Server Log" },
                new[] { "", "Client Log" },
                new[] { "", "Players" },
                new[] { "", "Settings" },
            };
            navButtons = new NavButton[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                int idx = i;
                var nb = new NavButton
                {
                    Glyph = items[i][0], Text = items[i][1], Width = 196, Height = 42, Margin = new Padding(0, 2, 0, 2),
                    Font = navFont, IconFont = iconFont, Accent = ColAccent, TextCol = ColText, DimCol = ColDim,
                    HoverCol = ColNavHov, SelBg = ColNavSel,
                };
                nb.Click += (s, e) => ShowPage(idx);
                navButtons[i] = nb;
                flow.Controls.Add(nb);
            }

            nav.Controls.Add(flow);
            nav.Controls.Add(statusBlock);
            nav.Controls.Add(header);
            return nav;
        }

        private Label NavStat() { return new Label { AutoSize = true, ForeColor = ColDim, Font = uiFont, Margin = new Padding(0, 2, 0, 2), BackColor = Color.Transparent, UseMnemonic = false }; }

        private void ShowPage(int index)
        {
            for (int i = 0; i < pages.Length; i++)
            {
                pages[i].Visible = (i == index);
                navButtons[i].Selected = (i == index);
                navButtons[i].Invalidate();
            }
            if (index == 3) RefreshPlayers();
        }

        // ---------------------------------------------------------------- pages

        private Label PageTitle(string text)
        {
            return new Label { Text = text, Dock = DockStyle.Top, Height = 52, Font = titleFont, ForeColor = ColText, UseMnemonic = false, Padding = new Padding(2, 4, 0, 0) };
        }

        private Panel BuildHome()
        {
            var page = new Panel { Dock = DockStyle.Fill, BackColor = ColBg };

            var tools = new CardPanel { Height = 92, Dock = DockStyle.Top, CardColor = ColCard, BorderColor = ColBorder, Radius = 12 };
            tools.Controls.Add(new Label { Text = "Tools", Font = h2Font, ForeColor = ColText, AutoSize = true, Location = new Point(18, 12), BackColor = Color.Transparent, UseMnemonic = false });
            var tSaves = Btn("Open Saves", 108, (s, e) => OpenFolder(Paths.SavesDir())); tSaves.Location = new Point(18, 44);
            var tLogs = Btn("Open Logs", 104, (s, e) => OpenFolder(Bin)); tLogs.Location = new Point(134, 44);
            var tUnb = Btn("Unblock DLLs", 116, (s, e) => UnblockDlls()); tUnb.Location = new Point(246, 44);
            tools.Controls.Add(tSaves); tools.Controls.Add(tLogs); tools.Controls.Add(tUnb);
            var gap2 = new Panel { Dock = DockStyle.Top, Height = 14, BackColor = ColBg };

            var client = new CardPanel { Height = 118, Dock = DockStyle.Top, CardColor = ColCard, BorderColor = ColBorder, Radius = 12 };
            client.Controls.Add(new Label { Text = "Client", Font = h2Font, ForeColor = ColText, AutoSize = true, Location = new Point(18, 12), BackColor = Color.Transparent, UseMnemonic = false });
            homeClientStat = new Label { AutoSize = true, Location = new Point(20, 46), Font = uiFont, ForeColor = ColDim, BackColor = Color.Transparent, UseMnemonic = false };
            client.Controls.Add(homeClientStat);
            hbStartClient = Btn("▶  Start Client", 134, (s, e) => StartClient(), ColAccent); hbStartClient.Location = new Point(18, 72);
            hbStopClient = Btn("■  Stop Client", 122, (s, e) => StopClient()); hbStopClient.Location = new Point(160, 72);
            client.Controls.Add(hbStartClient); client.Controls.Add(hbStopClient);
            var gap1 = new Panel { Dock = DockStyle.Top, Height = 14, BackColor = ColBg };

            var server = new CardPanel { Height = 150, Dock = DockStyle.Top, CardColor = ColCard, BorderColor = ColBorder, Radius = 12 };
            server.Controls.Add(new Label { Text = "Server", Font = h2Font, ForeColor = ColText, AutoSize = true, Location = new Point(18, 12), BackColor = Color.Transparent, UseMnemonic = false });
            homeServerStat = new Label { AutoSize = true, Location = new Point(20, 46), Font = uiFont, ForeColor = ColDim, BackColor = Color.Transparent, UseMnemonic = false };
            homeServerSummary = new Label { AutoSize = true, Location = new Point(20, 70), Font = subFont, ForeColor = ColDim, BackColor = Color.Transparent, UseMnemonic = false };
            server.Controls.Add(homeServerStat); server.Controls.Add(homeServerSummary);
            hbStartServer = Btn("▶  Start Server", 138, (s, e) => StartServer(), ColOk); hbStartServer.Location = new Point(18, 100);
            hbStopServer = Btn("■  Stop Server", 124, (s, e) => StopServer()); hbStopServer.Location = new Point(164, 100);
            hbRestart = Btn("⟳  Restart", 100, (s, e) => RestartServer()); hbRestart.Location = new Point(296, 100);
            server.Controls.Add(hbStartServer); server.Controls.Add(hbStopServer); server.Controls.Add(hbRestart);

            page.Controls.Add(tools);
            page.Controls.Add(gap2);
            page.Controls.Add(client);
            page.Controls.Add(gap1);
            page.Controls.Add(server);
            page.Controls.Add(PageTitle("Dashboard"));
            return page;
        }

        private Panel BuildLogPage(bool isServer)
        {
            var page = new Panel { Dock = DockStyle.Fill, BackColor = ColBg };
            var card = new CardPanel { Dock = DockStyle.Fill, CardColor = ColCard, BorderColor = ColBorder, Radius = 12, Padding = new Padding(12, 10, 12, 12) };

            var bar = new Panel { Dock = DockStyle.Top, Height = 34, BackColor = Color.Transparent };
            var rtb = new RichTextBox { Dock = DockStyle.Fill, BackColor = ColLogBg, ForeColor = ColText, Font = monoFont, ReadOnly = true, BorderStyle = BorderStyle.None, WordWrap = false, ScrollBars = RichTextBoxScrollBars.Both, DetectUrls = false };
            if (isServer) rtbServer = rtb; else rtbClient = rtb;

            bar.Controls.Add(new Label { Text = "Filters are in Settings › Logs", Dock = DockStyle.Left, Width = 260, ForeColor = ColDim, Font = subFont, TextAlign = ContentAlignment.MiddleLeft, UseMnemonic = false });
            var clear = Btn("Clear", 72, (s, e) => rtb.Clear()); clear.Dock = DockStyle.Right; clear.Width = 72;
            bar.Controls.Add(clear);

            card.Controls.Add(rtb);
            card.Controls.Add(bar);
            page.Controls.Add(card);
            page.Controls.Add(PageTitle(isServer ? "Server Log" : "Client Log"));
            return page;
        }

        private Panel BuildPlayers()
        {
            var page = new Panel { Dock = DockStyle.Fill, BackColor = ColBg };
            var card = new CardPanel { Dock = DockStyle.Fill, CardColor = ColCard, BorderColor = ColBorder, Radius = 12, Padding = new Padding(14, 12, 14, 12) };

            lblPlayersInfo = new Label { Dock = DockStyle.Top, Height = 28, ForeColor = ColDim, TextAlign = ContentAlignment.MiddleLeft, UseMnemonic = false, BackColor = Color.Transparent };

            gridPlayers = new DataGridView
            {
                Dock = DockStyle.Fill, BackgroundColor = ColLogBg, BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false,
                ReadOnly = true, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false, GridColor = ColBorder, EnableHeadersVisualStyles = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, Font = uiFont,
                RowTemplate = { Height = 30 }, ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = 34, CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            };
            StyleGrid(gridPlayers);
            AddCol("#", 8); AddCol("Name", 40); AddCol("Clan", 30); AddCol("State", 22);

            var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50, BackColor = Color.Transparent, Padding = new Padding(0, 9, 0, 0) };
            btnRename = Btn("Rename…", 92, (s, e) => RenameSelected(), ColAccent);
            btnKick = Btn("Kick", 72, (s, e) => KickSelected(), ColBad);
            btnSave = Btn("Save game", 98, (s, e) => Cmd("save", "Saving game…"));
            btnPause = Btn("Pause", 74, (s, e) => Cmd("pause", "Pausing…"));
            btnResume = Btn("Resume 1x", 96, (s, e) => Cmd("resume", "Resuming…"));
            btnFast = Btn("Fast 2x", 82, (s, e) => Cmd("speed 2", "Fast-forward…"));
            btnMenu = Btn("To menu", 88, (s, e) => { if (Confirm("Return the server to the main menu (disconnects everyone)?")) Cmd("menu", "Returning to menu…"); });
            actions.Controls.AddRange(new Control[] { btnRename, btnKick, btnSave, btnPause, btnResume, btnFast, btnMenu });

            card.Controls.Add(gridPlayers);
            card.Controls.Add(actions);
            card.Controls.Add(lblPlayersInfo);
            page.Controls.Add(card);
            page.Controls.Add(PageTitle("Players"));
            return page;
        }

        private Panel BuildSettings()
        {
            var page = new Panel { Dock = DockStyle.Fill, BackColor = ColBg, AutoScroll = true };

            var advanced = new CardPanel { Height = 176, Dock = DockStyle.Top, CardColor = ColCard, BorderColor = ColBorder, Radius = 12 };
            CardTitle(advanced, "Advanced");
            txtAddr = FieldText(advanced, "Address hint", "Reminder of the host address to join (typed in-game).", 44);
            txtGame = FieldText(advanced, "Game folder", "Leave blank to auto-detect from where this exe lives.", 96);
            var save = Btn("Save settings", 128, (s, e) => { CommitSettings(); Toast("✓ Settings saved to coopconsole.cfg"); }, ColAccent);
            save.Location = new Point(170, 140); advanced.Controls.Add(save);
            var g3 = new Panel { Dock = DockStyle.Top, Height = 14, BackColor = ColBg };

            var logs = new CardPanel { Height = 128, Dock = DockStyle.Top, CardColor = ColCard, BorderColor = ColBorder, Radius = 12 };
            CardTitle(logs, "Logs");
            tglHideDbg = ToggleRow(logs, "Hide DBG / VRB log lines", 46);
            tglHideNoise = ToggleRow(logs, "Hide internal sync-error spam", 84);
            var g2 = new Panel { Dock = DockStyle.Top, Height = 14, BackColor = ColBg };

            var hosting = new CardPanel { Height = 150, Dock = DockStyle.Top, CardColor = ColCard, BorderColor = ColBorder, Radius = 12 };
            CardTitle(hosting, "Hosting");
            cboVis = FieldCombo(hosting, "Visibility", new[] { "public", "friends_only", "none" }, 46);
            txtPass = FieldText(hosting, "Password", "Optional. Leave blank for no password.", 90);
            var g1 = new Panel { Dock = DockStyle.Top, Height = 14, BackColor = ColBg };

            var savefile = new CardPanel { Height = 116, Dock = DockStyle.Top, CardColor = ColCard, BorderColor = ColBorder, Radius = 12 };
            CardTitle(savefile, "Save file");
            txtSave = FieldText(savefile, "Save name", "The save the server loads & hosts — must exist in Game Saves as <name>.sav", 46);

            page.Controls.Add(advanced);
            page.Controls.Add(g3);
            page.Controls.Add(logs);
            page.Controls.Add(g2);
            page.Controls.Add(hosting);
            page.Controls.Add(g1);
            page.Controls.Add(savefile);
            page.Controls.Add(PageTitle("Settings"));
            return page;
        }

        // ---------------------------------------------------------------- lifecycle

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            serverFollower = new LogFollower(Paths.ServerLog(cfg.GameRoot), false);
            clientFollower = new LogFollower(Paths.ClientLog(cfg.GameRoot), false);

            LoadSettingsIntoControls();
            RefreshStatus();
            RefreshPlayers();
            SelectInitialTab();

            timer.Interval = 500;
            timer.Tick += (s, ev) => OnTick();
            timer.Start();
        }

        protected override void OnFormClosing(FormClosingEventArgs e) { try { timer.Stop(); CommitSettings(); } catch { } base.OnFormClosing(e); }

        private void SelectInitialTab()
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++)
            {
                if (!a[i].Equals("--tab", StringComparison.OrdinalIgnoreCase)) continue;
                var want = a[i + 1].Replace(" ", "").ToLowerInvariant();
                string[] names = { "home", "serverlog", "clientlog", "players", "settings" };
                for (int j = 0; j < names.Length; j++) if (names[j] == want) ShowPage(j);
            }
        }

        private void OnTick()
        {
            tick++;
            TailInto(serverFollower, rtbServer, ref serverFirstLoad);
            TailInto(clientFollower, rtbClient, ref clientFirstLoad);
            RefreshStatus();
            if (tick % 2 == 0 && pages[3].Visible) RefreshPlayers();
        }

        // ---------------------------------------------------------------- logs

        private void TailInto(LogFollower follower, RichTextBox box, ref bool firstLoad)
        {
            if (follower == null || box == null) return;
            var lines = follower.Poll();
            if (lines.Count == 0) return;

            int start = (firstLoad && lines.Count > 400) ? lines.Count - 400 : 0;
            firstLoad = false;
            bool hideDbg = cfg.HideDebugLogLines, hideNoise = cfg.HideSyncNoise;

            box.SuspendLayout();
            for (int i = start; i < lines.Count; i++)
            {
                var line = lines[i];
                var lvl = LogInsights.LevelOf(line);
                if (hideDbg && (lvl == "DBG" || lvl == "VRB")) continue;
                if (hideNoise && LogInsights.IsSyncNoise(line)) continue;
                box.SelectionStart = box.TextLength;
                box.SelectionLength = 0;
                box.SelectionColor = LevelColor(lvl);
                box.AppendText(line + "\n");
            }
            if (box.TextLength > 600000) { box.Select(0, 300000); box.SelectedText = ""; }
            box.SelectionStart = box.TextLength;
            box.ScrollToCaret();
            box.ResumeLayout();
        }

        private static Color LevelColor(string lvl)
        {
            switch (lvl)
            {
                case "ERR": return Color.FromArgb(240, 110, 110);
                case "FTL": return Color.FromArgb(232, 130, 232);
                case "WRN": return Color.FromArgb(236, 191, 97);
                case "INF": return Color.FromArgb(232, 232, 236);
                case "DBG":
                case "VRB": return Color.FromArgb(124, 124, 134);
                default: return Color.FromArgb(186, 186, 196);
            }
        }

        // ---------------------------------------------------------------- status + players

        private void RefreshStatus()
        {
            bool srv = launcher.IsRunning(launcher.ServerProcess);
            bool cli = launcher.IsRunning(launcher.ClientProcess);

            SetStat(navServer, "Server", srv, srv ? "running" : "stopped");
            SetStat(navClient, "Client", cli, cli ? "running" : "stopped");
            navEngine.ForeColor = ColDim; navEngine.Text = "Engine   " + GameLauncher.BannerlordProcessCount() + " process(es)";

            if (homeServerStat != null)
            {
                homeServerStat.ForeColor = srv ? ColOk : ColDim;
                homeServerStat.Text = srv ? "● RUNNING   ·   pid " + launcher.ServerProcess.Id : "○ stopped";
                homeServerSummary.Text = "Hosting save \"" + cfg.SaveName + "\"   ·   " + cfg.Visibility;
                homeClientStat.ForeColor = cli ? ColOk : ColDim;
                homeClientStat.Text = cli ? "● RUNNING   ·   pid " + launcher.ClientProcess.Id : "○ stopped   ·   connect in-game to " + cfg.ServerAddressHint;
                hbStartServer.Enabled = !srv; hbStopServer.Enabled = srv;
                hbStartClient.Enabled = !cli; hbStopClient.Enabled = cli;
            }
        }

        private void SetStat(Label lbl, string name, bool on, string text)
        {
            lbl.ForeColor = on ? ColOk : ColDim;
            lbl.Text = name + "   " + (on ? "● " : "○ ") + text;
        }

        private void RefreshPlayers()
        {
            if (gridPlayers == null) return;
            var snap = ControlChannel.ReadStatus(Bin);
            if (!snap.BridgeLive)
            {
                lblPlayersInfo.ForeColor = ColWarn;
                lblPlayersInfo.Text = snap.StatusFilePresent
                    ? "Server bridge not responding (status " + (int)snap.AgeSeconds + "s old). Rebuild the mod so the bridge ships."
                    : "No live server. Start & host a server (with the updated mod) to see players.";
                if (gridPlayers.Rows.Count > 0) gridPlayers.Rows.Clear();
                SetPlayerActions(false);
                return;
            }

            lblPlayersInfo.ForeColor = ColDim;
            var port = snap.Port == "?" ? "" : "   ·   port " + snap.Port;
            lblPlayersInfo.Text = snap.State + "   ·   save " + (snap.Save == "?" ? cfg.SaveName : snap.Save) + port + "   ·   " + snap.Players.Count + " player(s)";
            SetPlayerActions(true);

            string sel = (gridPlayers.CurrentRow != null && gridPlayers.CurrentRow.Tag is PlayerInfo cur) ? cur.ControllerId : null;
            gridPlayers.Rows.Clear();
            int i = 1;
            foreach (var p in snap.Players)
            {
                int idx = gridPlayers.Rows.Add((i++).ToString(),
                    string.IsNullOrEmpty(p.Name) ? "(connecting)" : p.Name,
                    string.IsNullOrEmpty(p.Clan) ? "—" : p.Clan,
                    string.IsNullOrEmpty(p.State) ? "—" : p.State);
                gridPlayers.Rows[idx].Tag = p;
                if (sel != null && p.ControllerId == sel) gridPlayers.Rows[idx].Selected = true;
            }
        }

        private void SetPlayerActions(bool on)
        {
            btnRename.Enabled = on; btnKick.Enabled = on; btnSave.Enabled = on;
            btnPause.Enabled = on; btnResume.Enabled = on; btnFast.Enabled = on; btnMenu.Enabled = on;
        }

        private PlayerInfo SelectedPlayer()
        {
            var row = gridPlayers.CurrentRow;
            return (row != null && row.Tag is PlayerInfo p) ? p : null;
        }

        // ---------------------------------------------------------------- actions

        private void StartServer() { CommitSettings(); try { launcher.StartServer(); Toast("Server launching (pid " + launcher.ServerProcess.Id + ") — watch the Server Log for 'MapState'."); } catch (Exception ex) { Error(ex.Message); } RefreshStatus(); }
        private void StartClient() { try { launcher.StartClient(); Toast("Client launching (pid " + launcher.ClientProcess.Id + "). Enter the host address in-game (127.0.0.1)."); } catch (Exception ex) { Error(ex.Message); } RefreshStatus(); }
        private void StopServer() { Toast(launcher.StopServer(false)); RefreshStatus(); }
        private void StopClient() { Toast(launcher.StopClient(false)); RefreshStatus(); }

        private void RestartServer()
        {
            if (!Confirm("Restart the server?")) return;
            Toast(launcher.StopServer(false));
            Task.Delay(1500).ContinueWith(_ => BeginInvoke((Action)(() =>
            {
                try { launcher.StartServer(); Toast("Server restarting (pid " + launcher.ServerProcess.Id + ")."); } catch (Exception ex) { Error(ex.Message); }
                RefreshStatus();
            })));
        }

        private void KickSelected()
        {
            var p = SelectedPlayer();
            if (p == null) { Toast("Select a player row first."); return; }
            if (!Confirm("Kick \"" + p.Name + "\"?")) return;
            Cmd("kick " + p.ControllerId, "Kicking " + p.Name + "…");
        }

        private void RenameSelected()
        {
            var p = SelectedPlayer();
            if (p == null) { Toast("Select a player row first."); return; }
            var name = InputBox("Rename player", "New name for \"" + p.Name + "\":", p.Name);
            if (string.IsNullOrWhiteSpace(name) || name == p.Name) return;
            Cmd("rename " + p.ControllerId + " " + name.Trim(), "Renaming to " + name.Trim() + "…");
        }

        private void Cmd(string command, string doing)
        {
            Toast(doing);
            var bin = Bin;
            Task.Run(() =>
            {
                bool ok = ControlChannel.SendCommand(bin, command, out var msg);
                BeginInvoke((Action)(() => Toast((ok ? "✓ " : "✗ ") + (string.IsNullOrEmpty(msg) ? (ok ? "Done." : "Failed.") : msg))));
            });
        }

        private void OpenFolder(string path)
        {
            try { if (!Directory.Exists(path)) { Toast("Folder not found: " + path); return; } Process.Start("explorer.exe", "\"" + path + "\""); Toast("Opened " + path); }
            catch (Exception ex) { Error(ex.Message); }
        }

        private void UnblockDlls()
        {
            var mod = Paths.ModuleDir(cfg.GameRoot);
            if (!Directory.Exists(mod)) { Toast("Module folder not found: " + mod); return; }
            Toast("Unblocking DLLs…");
            Task.Run(() =>
            {
                string result;
                try
                {
                    var psi = new ProcessStartInfo("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -Command \"Get-ChildItem -LiteralPath '" + mod + "' -Recurse | Unblock-File\"") { UseShellExecute = false, CreateNoWindow = true };
                    var p = Process.Start(psi); p.WaitForExit(30000); result = "✓ Unblock-File completed.";
                }
                catch (Exception ex) { result = "✗ " + ex.Message; }
                BeginInvoke((Action)(() => Toast(result)));
            });
        }

        // ---------------------------------------------------------------- settings glue

        private void LoadSettingsIntoControls()
        {
            txtSave.Text = cfg.SaveName;
            cboVis.SelectedItem = AppConfig.NormalizeVisibility(cfg.Visibility);
            if (cboVis.SelectedIndex < 0) cboVis.SelectedIndex = 0;
            txtPass.Text = cfg.Password;
            txtAddr.Text = cfg.ServerAddressHint;
            txtGame.Text = cfg.GameRoot;
            tglHideDbg.Checked = cfg.HideDebugLogLines;
            tglHideNoise.Checked = cfg.HideSyncNoise;
            tglHideDbg.CheckedChanged += (s, e) => { cfg.HideDebugLogLines = tglHideDbg.Checked; cfg.Save(); };
            tglHideNoise.CheckedChanged += (s, e) => { cfg.HideSyncNoise = tglHideNoise.Checked; cfg.Save(); };
        }

        private void CommitSettings()
        {
            if (txtSave == null) return;
            cfg.SaveName = string.IsNullOrWhiteSpace(txtSave.Text) ? "MP" : txtSave.Text.Trim();
            cfg.Visibility = AppConfig.NormalizeVisibility(cboVis.SelectedItem?.ToString());
            cfg.Password = txtPass.Text;
            cfg.ServerAddressHint = txtAddr.Text.Trim();
            cfg.GameRoot = txtGame.Text.Trim();
            cfg.HideDebugLogLines = tglHideDbg.Checked;
            cfg.HideSyncNoise = tglHideNoise.Checked;
            cfg.Save();
        }

        // ---------------------------------------------------------------- widgets

        private RoundedButton Btn(string text, int width, EventHandler onClick, Color? accent = null)
        {
            var b = new RoundedButton { Text = text, Width = width, Height = 34, Font = uiFont, Margin = new Padding(0, 0, 8, 0), Cursor = Cursors.Hand, Radius = 9 };
            if (accent.HasValue) { b.BaseColor = accent.Value; b.HoverColor = ColorFx.Lighten(accent.Value, 1.12f); b.PressColor = ColorFx.Lighten(accent.Value, 0.88f); b.TextColor = Color.FromArgb(16, 18, 22); }
            else { b.BaseColor = ColInput; b.HoverColor = Color.FromArgb(66, 66, 74); b.PressColor = Color.FromArgb(42, 42, 48); b.TextColor = ColText; }
            b.DisabledColor = Color.FromArgb(44, 44, 50); b.DisabledText = Color.FromArgb(110, 110, 118);
            b.Click += onClick;
            return b;
        }

        private void CardTitle(CardPanel card, string title)
        {
            card.Controls.Add(new Label { Text = title, Font = h2Font, ForeColor = ColText, AutoSize = true, Location = new Point(18, 12), BackColor = Color.Transparent, UseMnemonic = false });
        }

        private TextBox FieldText(CardPanel card, string label, string help, int y)
        {
            card.Controls.Add(new Label { Text = label, ForeColor = ColText, AutoSize = true, Location = new Point(18, y + 4), BackColor = Color.Transparent, UseMnemonic = false });
            var t = new TextBox { Location = new Point(170, y), Width = 340, BackColor = ColInput, ForeColor = ColText, BorderStyle = BorderStyle.FixedSingle, Font = uiFont };
            card.Controls.Add(t);
            card.Controls.Add(new Label { Text = help, ForeColor = ColDim, Font = subFont, AutoSize = true, Location = new Point(172, y + 25), BackColor = Color.Transparent, UseMnemonic = false });
            return t;
        }

        private ComboBox FieldCombo(CardPanel card, string label, string[] items, int y)
        {
            card.Controls.Add(new Label { Text = label, ForeColor = ColText, AutoSize = true, Location = new Point(18, y + 4), BackColor = Color.Transparent, UseMnemonic = false });
            var c = new ComboBox { Location = new Point(170, y), Width = 200, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = ColInput, ForeColor = ColText, FlatStyle = FlatStyle.Flat, Font = uiFont };
            c.Items.AddRange(items);
            card.Controls.Add(c);
            return c;
        }

        private ToggleSwitch ToggleRow(CardPanel card, string label, int y)
        {
            card.Controls.Add(new Label { Text = label, ForeColor = ColText, AutoSize = true, Location = new Point(18, y + 2), BackColor = Color.Transparent, UseMnemonic = false });
            var t = new ToggleSwitch { Location = new Point(360, y), OnColor = ColAccent, OffColor = Color.FromArgb(78, 78, 86) };
            card.Controls.Add(t);
            return t;
        }

        private void AddCol(string header, int fill) { gridPlayers.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = header, FillWeight = fill, SortMode = DataGridViewColumnSortMode.NotSortable }); }

        private void StyleGrid(DataGridView g)
        {
            g.ColumnHeadersDefaultCellStyle.BackColor = ColCard;
            g.ColumnHeadersDefaultCellStyle.ForeColor = ColDim;
            g.ColumnHeadersDefaultCellStyle.Font = new Font(BodyFamily, 9f, FontStyle.Regular, GraphicsUnit.Point);
            g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            g.DefaultCellStyle.BackColor = ColLogBg;
            g.DefaultCellStyle.ForeColor = ColText;
            g.DefaultCellStyle.SelectionBackColor = Color.FromArgb(38, 74, 104);
            g.DefaultCellStyle.SelectionForeColor = Color.White;
            g.DefaultCellStyle.Padding = new Padding(8, 0, 0, 0);
            g.RowsDefaultCellStyle.BackColor = ColLogBg;
            g.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(30, 30, 34);
        }

        // ---------------------------------------------------------------- helpers

        private void Toast(string text) { if (toast != null) toast.Text = text; }
        private void Error(string msg) { Toast("✗ " + msg); MessageBox.Show(msg, "Fonza Launcher", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        private bool Confirm(string msg) { return MessageBox.Show(msg, "Fonza Launcher", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes; }

        private string InputBox(string title, string prompt, string def)
        {
            using (var f = new Form { Text = title, FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(400, 130), BackColor = ColBg, ForeColor = ColText, Font = uiFont, MaximizeBox = false, MinimizeBox = false })
            {
                var lbl = new Label { Text = prompt, ForeColor = ColText, AutoSize = true, Location = new Point(16, 16), UseMnemonic = false };
                var tb = new TextBox { Text = def, Location = new Point(16, 44), Width = 368, BackColor = ColInput, ForeColor = ColText, BorderStyle = BorderStyle.FixedSingle };
                var ok = Btn("OK", 90, null, ColAccent); ok.Location = new Point(196, 84); ok.DialogResult = DialogResult.OK;
                var cancel = Btn("Cancel", 90, null); cancel.Location = new Point(294, 84); cancel.DialogResult = DialogResult.Cancel;
                f.Controls.Add(lbl); f.Controls.Add(tb); f.Controls.Add(ok); f.Controls.Add(cancel);
                f.AcceptButton = ok; f.CancelButton = cancel;
                try { Native.ApplyModernChrome(f.Handle); } catch { }
                return f.ShowDialog(this) == DialogResult.OK ? tb.Text : null;
            }
        }

        private static FontFamily PickFamily(params string[] names)
        {
            foreach (var n in names) { try { return new FontFamily(n); } catch { } }
            return FontFamily.GenericSansSerif;
        }
    }

    // -------------------------------------------------------------------- custom controls

    internal static class Native
    {
        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;
        public static void ApplyModernChrome(IntPtr hwnd)
        {
            int on = 1; try { DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, sizeof(int)); } catch { }
            int round = DWMWCP_ROUND; try { DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int)); } catch { }
        }
    }

    internal static class ColorFx
    {
        public static Color Lighten(Color c, float f) { return Color.FromArgb(c.A, Clamp(c.R * f), Clamp(c.G * f), Clamp(c.B * f)); }
        private static int Clamp(float v) { return (int)Math.Max(0, Math.Min(255, v)); }
    }

    internal sealed class RoundedButton : Button
    {
        public Color BaseColor = Color.FromArgb(48, 48, 54);
        public Color HoverColor = Color.FromArgb(66, 66, 74);
        public Color PressColor = Color.FromArgb(42, 42, 48);
        public Color TextColor = Color.White;
        public Color DisabledColor = Color.FromArgb(44, 44, 50);
        public Color DisabledText = Color.FromArgb(110, 110, 118);
        public int Radius = 9;
        private bool hover, down;

        public RoundedButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; BackColor = Color.Transparent;
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            var bg = Parent != null ? Parent.BackColor : Color.FromArgb(28, 28, 32);
            using (var b = new SolidBrush(bg)) g.FillRectangle(b, ClientRectangle);
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fill = !Enabled ? DisabledColor : down ? PressColor : hover ? HoverColor : BaseColor;
            using (var path = Round(rect, Radius)) using (var b = new SolidBrush(fill)) g.FillPath(b, path);
            TextRenderer.DrawText(g, Text, Font, rect, Enabled ? TextColor : DisabledText, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
        private static GraphicsPath Round(Rectangle r, int radius)
        {
            int d = radius * 2; var p = new GraphicsPath();
            if (d <= 0) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure(); return p;
        }
    }

    internal sealed class NavButton : Control
    {
        public bool Selected;
        public string Glyph = "";
        public Font IconFont;
        public Color Accent, TextCol, DimCol, HoverCol, SelBg;
        private bool hover;
        public NavButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            var bg = Parent != null ? Parent.BackColor : Color.Black;
            using (var b = new SolidBrush(bg)) g.FillRectangle(b, ClientRectangle);
            var r = new Rectangle(0, 1, Width - 1, Height - 3);
            if (Selected || hover)
                using (var path = Round(r, 9)) using (var b = new SolidBrush(Selected ? SelBg : HoverCol)) g.FillPath(b, path);
            if (Selected)
                using (var b = new SolidBrush(Accent)) using (var p = new GraphicsPath())
                { var bar = new Rectangle(3, r.Y + 9, 3, r.Height - 18); p.AddRectangle(bar); g.FillPath(b, p); }

            var textCol = Selected ? TextCol : (hover ? TextCol : DimCol);
            if (IconFont != null)
                TextRenderer.DrawText(g, Glyph, IconFont, new Rectangle(14, 0, 26, Height), Selected ? Accent : textCol, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
            TextRenderer.DrawText(g, Text, Font, new Rectangle(46, 0, Width - 50, Height), textCol, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        }
        private static GraphicsPath Round(Rectangle r, int radius)
        {
            int d = radius * 2; var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure(); return p;
        }
    }

    internal sealed class CardPanel : Panel
    {
        public Color CardColor = Color.FromArgb(38, 38, 44);
        public Color BorderColor = Color.FromArgb(54, 54, 62);
        public int Radius = 12;
        public CardPanel()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint, true);
            BackColor = CardColor;
        }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            var pbg = Parent != null ? Parent.BackColor : Color.FromArgb(28, 28, 32);
            using (var b = new SolidBrush(pbg)) g.FillRectangle(b, ClientRectangle);
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Round(r, Radius))
            {
                using (var b = new SolidBrush(CardColor)) g.FillPath(b, path);
                using (var p = new Pen(BorderColor)) g.DrawPath(p, path);
            }
        }
        private static GraphicsPath Round(Rectangle r, int radius)
        {
            int d = radius * 2; var p = new GraphicsPath();
            if (d <= 0) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure(); return p;
        }
    }

    internal sealed class ToggleSwitch : Control
    {
        private bool _checked;
        public event EventHandler CheckedChanged;
        public Color OnColor = Color.FromArgb(76, 194, 255);
        public Color OffColor = Color.FromArgb(78, 78, 86);
        public bool Checked { get { return _checked; } set { if (_checked != value) { _checked = value; Invalidate(); CheckedChanged?.Invoke(this, EventArgs.Empty); } } }
        public ToggleSwitch()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Size = new Size(46, 24); Cursor = Cursors.Hand;
        }
        protected override void OnClick(EventArgs e) { Checked = !Checked; base.OnClick(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            var bg = Parent != null ? Parent.BackColor : Color.Black;
            using (var b = new SolidBrush(bg)) g.FillRectangle(b, ClientRectangle);
            var r = new Rectangle(1, 2, Width - 3, Height - 5);
            int rad = r.Height / 2;
            using (var path = Pill(r, rad))
            {
                using (var b = new SolidBrush(_checked ? OnColor : OffColor)) g.FillPath(b, path);
                if (!_checked) using (var p = new Pen(Color.FromArgb(120, 150, 150, 160))) g.DrawPath(p, path);
            }
            int knob = r.Height - 6;
            int ky = r.Y + 3;
            int kx = _checked ? r.Right - knob - 4 : r.X + 4;
            using (var kb = new SolidBrush(_checked ? Color.White : Color.FromArgb(205, 205, 212))) g.FillEllipse(kb, kx, ky, knob, knob);
        }
        private static GraphicsPath Pill(Rectangle r, int rad)
        {
            int d = rad * 2; var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 90, 180); p.AddArc(r.Right - d, r.Y, d, d, 270, 180);
            p.CloseFigure(); return p;
        }
    }
}
