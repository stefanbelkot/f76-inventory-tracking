using System.Diagnostics;
using InventoryTracking.Syncer.Settings;
using InventoryTracking.Syncer.Startup;
using InventoryTracking.Syncer.Sync;

namespace InventoryTracking.Syncer.Shell;

/// <summary>The notification-area icon with its menu; owns the sync runner. <see cref="Run"/> is the message loop.</summary>
internal sealed class TrayApplication : ApplicationContext
{
    private const int MaxTooltipLength = 63;

    private readonly SettingsStore _store;
    private readonly SyncEngine _engine;
    private readonly SyncRunner _runner;
    private readonly Autostart _autostart;
    private readonly ILog _log;
    private readonly NotifyIcon _notify;
    private readonly ToolStripMenuItem _status = new("Starting...") { Enabled = false };
    private readonly ToolStripMenuItem _startWithWindows = new("Start with Windows") { CheckOnClick = false };
    private readonly Control _marshal = new();
    private SettingsForm? _form;
    private SyncState _shownState = SyncState.NotConfigured;
    private bool _warned;

    public TrayApplication(SettingsStore store, SyncEngine engine, SyncRunner runner, Autostart autostart, ILog log, bool background)
    {
        _store = store;
        _engine = engine;
        _runner = runner;
        _autostart = autostart;
        _log = log;
        _ = _marshal.Handle;

        var menu = new ContextMenuStrip();
        menu.Items.Add(_status);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Sync now", null, (_, _) => _runner.Request());
        menu.Items.Add("Open viewer in browser", null, (_, _) => OpenViewer());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Update session...", null, (_, _) => UpdateSession());
        menu.Items.Add("Update Xbox / Steam folders...", null, (_, _) => UpdateFolders());
        menu.Items.Add("Settings...", null, (_, _) => ShowSettings());
        _startWithWindows.Click += (_, _) => ToggleAutostart();
        menu.Items.Add(_startWithWindows);
        menu.Items.Add("Open log folder", null, (_, _) => OpenFolder(_store.Directory));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => Quit());
        menu.Opening += (_, _) => _startWithWindows.Checked = _autostart.IsEnabled;

        _notify = new NotifyIcon { Icon = TrayIconFactory.Create(SyncState.NotConfigured), Text = Tooltip("Starting"), ContextMenuStrip = menu, Visible = true };
        _notify.DoubleClick += (_, _) => ShowSettings();
        _runner.StatusChanged += outcome => _marshal.BeginInvoke(() => Show(outcome));

        var configured = _store.Read(s => s.Configured);
        if (configured)
        {
            _runner.Start();
        }

        if (!configured || !background)
        {
            // A first start (or incomplete settings) always opens the window; a normal start by hand shows it as well.
            _marshal.BeginInvoke(() => ShowSettings(first: !_store.Read(s => s.AskedAboutAutostart)));
        }
    }

    public void ShowSettingsFromOutside() => _marshal.BeginInvoke(() => ShowSettings());

    private void ShowSettings(bool first = false)
    {
        if (_form is { IsDisposed: false })
        {
            _form.Activate();
            return;
        }

        if (first)
        {
            var answer = MessageBox.Show(
                "Start Inventory Tracking Syncer when Windows starts?\n\nIt then runs in the notification area and uploads your export files automatically.",
                "Inventory Tracking Syncer",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            _store.Update(s =>
            {
                s.StartWithWindows = answer == DialogResult.Yes;
                s.AskedAboutAutostart = true;
            });
            _autostart.Set(answer == DialogResult.Yes);
            if (_store.Read(s => s.Folders.Count) == 0)
            {
                _store.Update(s => FolderDetector.Merge(s.Folders, FolderDetector.Detect()));
            }
        }

        _form = new SettingsForm(_store, _engine, _autostart);
        _form.FormClosed += (_, _) =>
        {
            var saved = _form?.Saved ?? false;
            _form = null;
            if (saved)
            {
                if (_store.Read(s => s.Configured))
                {
                    _runner.Start();
                    _runner.Reconfigure();
                }
            }
        };
        _form.Show();
        _form.Activate();
    }

    private void UpdateSession()
    {
        using var dialog = new SessionDialog(_store.Read(s => s.Server));
        if (dialog.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        _store.Update(s =>
        {
            s.Session = dialog.SessionId;
            s.Server = dialog.Server;
        });
        _engine.ResetRejected();
        _log.Write("session updated");
        _warned = false;
        if (_store.Read(s => s.Configured))
        {
            _runner.Start();
            _runner.Reconfigure();
        }
        else
        {
            ShowSettings();
        }
    }

    private void UpdateFolders()
    {
        var found = FolderDetector.Detect();
        var fresh = _store.Read(s => found.Where(f => !s.Folders.Any(e => string.Equals(e.Path.TrimEnd('\\'), f.Path.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))).ToList());
        if (fresh.Count == 0)
        {
            var known = _store.Read(s => s.Folders.Count);
            MessageBox.Show($"No new Steam or Xbox folders found ({known} folder(s) are already set up).", "Inventory Tracking Syncer", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var list = string.Join("\n", fresh.Select(f => $"{(f.Origin == "steam" ? "Steam" : "Xbox")}: {f.Path}"));
        if (MessageBox.Show($"Add these folders?\n\n{list}", "Inventory Tracking Syncer", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        _store.Update(s => FolderDetector.Merge(s.Folders, fresh));
        _log.Write($"folders added: {string.Join(", ", fresh.Select(f => f.Path))}");
        if (_store.Read(s => s.Configured))
        {
            _runner.Start();
            _runner.Reconfigure();
        }
    }

    private void ToggleAutostart()
    {
        var enable = !_autostart.IsEnabled;
        _autostart.Set(enable);
        _store.Update(s => s.StartWithWindows = enable);
        _startWithWindows.Checked = enable;
        _notify.ShowBalloonTip(2000, "Inventory Tracking Syncer", enable ? "Will start with Windows." : "Will not start with Windows.", ToolTipIcon.Info);
    }

    private void OpenViewer()
    {
        var (server, session) = _store.Read(s => (s.Server, s.Session));
        var address = SessionLink.IsSessionId(session) ? $"{server}?session={session}" : server;
        try
        {
            Process.Start(new ProcessStartInfo(address) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _log.Write($"cannot open browser: {ex.Message}");
        }
    }

    private void OpenFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException)
        {
            _log.Write($"cannot open folder: {ex.Message}");
        }
    }

    private void Show(SyncOutcome outcome)
    {
        _status.Text = outcome.Message;
        _notify.Text = Tooltip(outcome.Message);
        if (outcome.State != _shownState)
        {
            var previous = _shownState;
            _shownState = outcome.State;
            var old = _notify.Icon;
            _notify.Icon = TrayIconFactory.Create(outcome.State);
            old?.Dispose();
            if (outcome.State is SyncState.SessionExpired or SyncState.Problem or SyncState.Offline && !_warned)
            {
                _warned = true;
                _notify.ShowBalloonTip(4000, "Inventory Tracking Syncer", outcome.Message, ToolTipIcon.Warning);
            }
            else if (outcome.State == SyncState.Ok && previous is SyncState.SessionExpired or SyncState.Problem or SyncState.Offline)
            {
                _warned = false;
            }
        }

        if (outcome is { State: SyncState.Ok } && _firstSync)
        {
            _firstSync = false;
            _notify.ShowBalloonTip(3000, "Inventory Tracking Syncer", outcome.Message, ToolTipIcon.Info);
        }
    }

    private bool _firstSync = true;

    private void Quit()
    {
        _runner.Dispose();
        _notify.Visible = false;
        _notify.Dispose();
        ExitThread();
    }

    private static string Tooltip(string status)
    {
        var text = $"Inventory Tracking Syncer - {status}";
        return text.Length <= MaxTooltipLength ? text : text[..MaxTooltipLength];
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _notify.Dispose();
            _marshal.Dispose();
        }

        base.Dispose(disposing);
    }
}
