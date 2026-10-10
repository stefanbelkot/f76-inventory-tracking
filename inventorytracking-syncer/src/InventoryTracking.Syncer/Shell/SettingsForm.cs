using InventoryTracking.Core.Setup;
using InventoryTracking.Syncer.Settings;
using InventoryTracking.Syncer.Startup;
using InventoryTracking.Syncer.Sync;

namespace InventoryTracking.Syncer.Shell;

/// <summary>The settings window: server, session, folders and start with Windows. Built in code (no designer files).</summary>
internal sealed class SettingsForm : Form
{
    private readonly SettingsStore _store;
    private readonly SyncEngine _engine;
    private readonly Autostart _autostart;
    private readonly TextBox _server = new() { Dock = DockStyle.Fill };
    private readonly TextBox _session = new() { Dock = DockStyle.Fill };
    private readonly CheckedListBox _folders = new() { Dock = DockStyle.Fill, CheckOnClick = true, IntegralHeight = false };
    private readonly CheckBox _startWithWindows = new() { Text = "Start Inventory Tracking Syncer with Windows", AutoSize = true };
    private readonly Label _message = new() { AutoSize = true, MaximumSize = new Size(560, 0) };
    private readonly List<WatchedFolder> _items = [];

    public SettingsForm(SettingsStore store, SyncEngine engine, Autostart autostart)
    {
        _store = store;
        _engine = engine;
        _autostart = autostart;
        Text = "Inventory Tracking Syncer";
        Icon = TrayIconFactory.Create(SyncState.Ok);
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9f);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(640, 560);
        MinimumSize = new Size(560, 520);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(12), RowCount = 9 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var row = 0; row < 9; row++)
        {
            // The folder list takes the free height; every other row is as tall as its content.
            layout.RowStyles.Add(row == 3 ? new RowStyle(SizeType.Percent, 100) : new RowStyle(SizeType.AutoSize));
        }

        Controls.Add(layout);

        layout.Controls.Add(new Label { Text = "Viewer address", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 0, 6) }, 0, 0);
        layout.Controls.Add(_server, 1, 0);

        layout.Controls.Add(new Label { Text = "Session link or id", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 0, 6) }, 0, 1);
        layout.Controls.Add(_session, 1, 1);

        var sessionButtons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        var paste = new Button { Text = "Paste", AutoSize = true };
        paste.Click += (_, _) => _session.Text = Clipboard.ContainsText() ? Clipboard.GetText().Trim() : _session.Text;
        var create = new Button { Text = "Create new session", AutoSize = true };
        create.Click += async (_, _) => await CreateSessionAsync();
        var check = new Button { Text = "Check session", AutoSize = true };
        check.Click += async (_, _) => await CheckSessionAsync(showSuccess: true);
        sessionButtons.Controls.AddRange([paste, create, check]);
        layout.Controls.Add(sessionButtons, 1, 2);

        layout.Controls.Add(new Label { Text = "Folders", AutoSize = true, Anchor = AnchorStyles.Top | AnchorStyles.Left, Margin = new Padding(0, 6, 0, 6) }, 0, 3);
        _folders.MinimumSize = new Size(0, 110);
        layout.Controls.Add(_folders, 1, 3);

        var folderButtons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        var add = new Button { Text = "Add folder...", AutoSize = true };
        add.Click += (_, _) => AddFolder();
        var remove = new Button { Text = "Remove", AutoSize = true };
        remove.Click += (_, _) => RemoveSelected();
        var detect = new Button { Text = "Detect Steam / Xbox", AutoSize = true };
        detect.Click += (_, _) => Detect();
        folderButtons.Controls.AddRange([add, remove, detect]);
        layout.Controls.Add(folderButtons, 1, 4);

        layout.Controls.Add(_startWithWindows, 1, 5);

        var privacy = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(480, 0),
            ForeColor = SystemColors.GrayText,
            Text = "Uploading sends your export files (including account and character names) to the viewer address above. "
                + "The session id is the only access key: keep it private.",
        };
        layout.Controls.Add(privacy, 1, 6);

        var bottom = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
        var save = new Button { Text = "Save and start", AutoSize = true };
        save.Click += async (_, _) => await SaveAsync();
        var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        bottom.Controls.AddRange([save, cancel]);
        layout.Controls.Add(_message, 0, 7);
        layout.SetColumnSpan(_message, 2);
        layout.Controls.Add(bottom, 1, 8);
        CancelButton = cancel;
        AcceptButton = save;

        _store.Read(s =>
        {
            _server.Text = s.Server;
            _session.Text = s.Session;
            _items.AddRange(s.Folders.Select(f => new WatchedFolder { Path = f.Path, Enabled = f.Enabled, Origin = f.Origin }));
            _startWithWindows.Checked = s.StartWithWindows || autostart.IsEnabled;
            return 0;
        });
        FillFolders();
    }

    /// <summary>The settings window was saved with a configuration that can run.</summary>
    public bool Saved { get; private set; }

    private void FillFolders()
    {
        _folders.Items.Clear();
        foreach (var folder in _items)
        {
            var label = folder.Origin switch { "steam" => "Steam", "xbox" => "Xbox", _ => "Added" };
            var files = Directory.Exists(folder.Path) ? $"{InventoryTracking.Core.Sources.ExportFileName.CountIn(folder.Path)} export files" : "folder not found yet";
            _folders.Items.Add($"[{label}] {folder.Path}  ({files})", folder.Enabled);
        }
    }

    private void SyncChecks()
    {
        for (var index = 0; index < _items.Count; index++)
        {
            _items[index].Enabled = _folders.GetItemChecked(index);
        }
    }

    private void AddFolder()
    {
        SyncChecks();
        using var dialog = new FolderBrowserDialog { Description = "Choose the Fallout 76 install folder or its Data\\modsdata folder", UseDescriptionForTitle = true };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var resolved = GameFolderResolver.Resolve(dialog.SelectedPath);
        if (resolved is null)
        {
            MessageBox.Show(this, "No modsdata folder was found there.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!_items.Any(f => string.Equals(f.Path, resolved.Path, StringComparison.OrdinalIgnoreCase)))
        {
            _items.Add(new WatchedFolder { Path = resolved.Path, Origin = "manual", Enabled = true });
        }

        FillFolders();
    }

    private void RemoveSelected()
    {
        SyncChecks();
        if (_folders.SelectedIndex >= 0)
        {
            _items.RemoveAt(_folders.SelectedIndex);
            FillFolders();
        }
    }

    private void Detect()
    {
        SyncChecks();
        var added = FolderDetector.Merge(_items, FolderDetector.Detect());
        FillFolders();
        Say(added.Count == 0 ? "No new Steam or Xbox folders found." : $"Added {added.Count} folder(s).");
    }

    private async Task CreateSessionAsync()
    {
        var server = SessionLink.NormalizeServer(_server.Text);
        if (server is null)
        {
            Say("Enter a valid https viewer address first.");
            return;
        }

        try
        {
            Say("Creating a session...");
            var id = await _engine.CreateSessionAsync(server, CancellationToken.None);
            var link = $"{server}?session={id}";
            _session.Text = link;
            Clipboard.SetText(link);
            Say("New session created; the link was copied. Open it in a browser to see your data.");
        }
        catch (SessionException ex)
        {
            Say(ex.Message);
        }
    }

    /// <returns>True when saving may go on (session exists, or the server could not be asked and the user agreed).</returns>
    private async Task<bool> CheckSessionAsync(bool showSuccess)
    {
        if (!TryReadSession(out var server, out var id))
        {
            return false;
        }

        try
        {
            Say("Checking the session...");
            await _engine.CheckAsync(server, id, CancellationToken.None);
            if (showSuccess)
            {
                Say("The session exists.");
            }

            return true;
        }
        catch (SessionException ex) when (ex.Problem == SessionProblem.Unknown)
        {
            Say("The server does not know this session (it may have expired). Create a new one or paste another link.");
            return false;
        }
        catch (SessionException ex)
        {
            Say(ex.Message);
            return !showSuccess && MessageBox.Show(this, ex.Message + "\n\nSave anyway?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        }
    }

    private bool TryReadSession(out string server, out string id)
    {
        id = "";
        server = SessionLink.NormalizeServer(_server.Text) ?? "";
        if (!SessionLink.TryParse(_session.Text, out id, out var linkServer))
        {
            Say("Paste the session link (https://.../?session=...) or the 32-character session id.");
            return false;
        }

        if (linkServer is not null && !string.Equals(linkServer, server, StringComparison.OrdinalIgnoreCase))
        {
            if (MessageBox.Show(this, $"The link points to\n{linkServer}\nUse this viewer address?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                server = linkServer;
                _server.Text = linkServer;
            }
        }

        if (server.Length == 0)
        {
            Say("The viewer address must start with https://.");
            return false;
        }

        return true;
    }

    private async Task SaveAsync()
    {
        SyncChecks();
        if (!_items.Any(f => f.Enabled))
        {
            Say("Enable at least one folder (use Detect Steam / Xbox or Add folder).");
            return;
        }

        if (!await CheckSessionAsync(showSuccess: false) || !TryReadSession(out var server, out var id))
        {
            return;
        }

        var wasSession = _store.Read(s => s.Session);
        _store.Update(s =>
        {
            s.Server = server;
            s.Session = id;
            s.Folders = _items.Select(f => new WatchedFolder { Path = f.Path, Enabled = f.Enabled, Origin = f.Origin }).ToList();
            s.StartWithWindows = _startWithWindows.Checked;
            s.AskedAboutAutostart = true;
        });
        _autostart.Set(_startWithWindows.Checked);
        if (wasSession != id)
        {
            _engine.ResetRejected();
        }

        Saved = true;
        Close();
    }

    private void Say(string text) => _message.Text = text;
}
