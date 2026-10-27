// Notification-area (tray) icon: left click opens the Studio page, right click the menu (status, restart, logs,
// start with Windows, quit).

using System.Diagnostics;
using Microsoft.Win32;

sealed class TrayApp : ApplicationContext
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string RunValue = "SimRig Studio";

    readonly NodeRunner _node;
    readonly TelemetryBridge _bridge;
    readonly AppPaths _paths;
    readonly NotifyIcon _tray;
    readonly System.Windows.Forms.Timer _timer = new() { Interval = 1000 };
    readonly SynchronizationContext _ui;
    readonly ToolStripMenuItem _status = new() { Enabled = false };
    readonly ToolStripMenuItem _game = new() { Enabled = false };
    readonly ToolStripMenuItem _autostart;
    bool _pluginWarned;

    public TrayApp(NodeRunner node, TelemetryBridge bridge, AppPaths paths, bool quiet)
    {
        _node = node;
        _bridge = bridge;
        _paths = paths;
        _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

        var open = new ToolStripMenuItem(Text.T("Studio'yu aç", "Open Studio")) { Font = new Font(SystemFonts.MenuFont!, FontStyle.Bold) };
        open.Click += (_, _) => OpenStudio();
        var restart = new ToolStripMenuItem(Text.T("Studio'yu yeniden başlat", "Restart Studio"));
        restart.Click += (_, _) => _node.Restart();
        var logs = new ToolStripMenuItem(Text.T("Günlük klasörünü aç", "Open the log folder"));
        logs.Click += (_, _) => OpenPath(_paths.Logs);
        _autostart = new ToolStripMenuItem(Text.T("Windows ile birlikte başlat", "Start with Windows")) { CheckOnClick = true };
        _autostart.Click += (_, _) => SetAutostart(_autostart.Checked);
        var exit = new ToolStripMenuItem(Text.T("Çıkış", "Quit"));
        exit.Click += (_, _) => Exit();

        var menu = new ContextMenuStrip();
        menu.Items.AddRange([open, new ToolStripSeparator(), _status, _game, new ToolStripSeparator(), restart, logs, _autostart,
            new ToolStripSeparator(), exit]);
        menu.Opening += (_, _) => { _autostart.Checked = AutostartEnabled(); Refresh(); };

        _tray = new NotifyIcon
        {
            Text = "SimRig Studio",
            Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? SystemIcons.Application,
            ContextMenuStrip = menu,
            Visible = true,
        };
        _tray.MouseUp += (_, e) => { if (e.Button == MouseButtons.Left) OpenStudio(); };
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();
        Refresh();

        // a game update or verify can delete the telemetry plugin: say so shortly after the start
        var startupCheck = new System.Windows.Forms.Timer { Interval = 6000 };
        startupCheck.Tick += (_, _) =>
        {
            startupCheck.Stop();
            var missing = PluginCheck.Find();
            if (missing.Count > 0 && !_pluginWarned)
            {
                _pluginWarned = true;
                int failed = PluginCheck.CopyMissing();
                _tray.ShowBalloonTip(8000, "SimRig Studio", failed == 0
                    ? Text.T($"Telemetri eklentisi eksikti ve geri kopyalandı ({string.Join(", ", missing.Select(m => m.Game))}). Oyunu yeniden başlat.",
                             $"The telemetry plugin was missing and has been copied back ({string.Join(", ", missing.Select(m => m.Game))}). Restart the game.")
                    : Text.T($"Telemetri eklentisi eksik ({string.Join(", ", missing.Select(m => m.Game))}). SimRig Studio'yu yeniden kur.",
                             $"The telemetry plugin is missing ({string.Join(", ", missing.Select(m => m.Game))}). Reinstall SimRig Studio."),
                    ToolTipIcon.Warning);
            }
        };
        startupCheck.Start();

        // started with Windows: stay in the tray
        if (!quiet) OpenStudio();
    }

    void Refresh()
    {
        _status.Text = _node.Running
            ? Text.T("Studio çalışıyor", "Studio is running")
            : Text.T("Studio çalışmıyor", "Studio is not running");
        _game.Text = _bridge.GameConnected
            ? Text.T("Oyun: veri geliyor", "Game: telemetry arriving")
            : Text.T("Oyun: veri yok", "Game: no telemetry");
        _tray.Text = _bridge.GameConnected ? "SimRig Studio" : "SimRig Studio (" + Text.T("oyun yok", "no game") + ")";
    }

    public void Post(Action a) => _ui.Post(_ => a(), null);

    public void OpenStudio() => OpenPath($"http://127.0.0.1:{NodeRunner.StudioPort}/");

    static void OpenPath(string target)
    {
        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
        catch (Exception e) { Console.WriteLine($"open {target}: {e.Message}"); }
    }

    public void Exit()
    {
        _timer.Stop();
        _tray.Visible = false;
        _tray.Dispose();
        ExitThread();
    }

    static bool AutostartEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(RunValue) is string;
    }

    static void SetAutostart(bool on)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (on) key.SetValue(RunValue, $"\"{Environment.ProcessPath}\" --autostart");
        else key.DeleteValue(RunValue, throwOnMissingValue: false);
    }
}
