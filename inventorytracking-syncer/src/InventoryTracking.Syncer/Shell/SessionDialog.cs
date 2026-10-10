namespace InventoryTracking.Syncer.Shell;

/// <summary>A small dialog to paste a new session link or id.</summary>
internal sealed class SessionDialog : Form
{
    private readonly TextBox _text = new() { Dock = DockStyle.Top };
    private readonly Label _message = new() { AutoSize = true, Dock = DockStyle.Top, ForeColor = Color.Firebrick };
    private readonly string _currentServer;

    public SessionDialog(string currentServer)
    {
        _currentServer = currentServer;
        Server = currentServer;
        Text = "Update session";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(520, 130);
        Font = new Font("Segoe UI", 9f);

        var ok = new Button { Text = "Use this session", AutoSize = true, Anchor = AnchorStyles.Bottom | AnchorStyles.Right, Location = new Point(300, 92) };
        var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel, Anchor = AnchorStyles.Bottom | AnchorStyles.Right, Location = new Point(430, 92) };
        ok.Click += (_, _) => Accept();
        CancelButton = cancel;
        AcceptButton = ok;

        var prompt = new Label { Text = "Paste the session link from the viewer page (or the 32-character id):", AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(0, 0, 0, 6) };
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        panel.Controls.Add(_message);
        panel.Controls.Add(_text);
        panel.Controls.Add(prompt);
        Controls.Add(ok);
        Controls.Add(cancel);
        Controls.Add(panel);
        ok.BringToFront();
        cancel.BringToFront();
        if (Clipboard.ContainsText() && SessionLink.TryParse(Clipboard.GetText(), out _, out _))
        {
            _text.Text = Clipboard.GetText().Trim();
        }
    }

    public string SessionId { get; private set; } = "";

    public string Server { get; private set; }

    private void Accept()
    {
        if (!SessionLink.TryParse(_text.Text, out var id, out var server))
        {
            _message.Text = "That is not a session link or id.";
            return;
        }

        SessionId = id;
        Server = server ?? _currentServer;
        DialogResult = DialogResult.OK;
        Close();
    }
}
