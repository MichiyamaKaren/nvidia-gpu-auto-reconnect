using GpuAutoReconnect.Services;

namespace GpuAutoReconnect.UI;

public class LogViewerForm : Form
{
    private readonly LogService _log;
    private TextBox _textBox = null!;

    public LogViewerForm(LogService log)
    {
        _log = log;
        InitializeComponents();
        LoadEntries();
        _log.EntryAdded += OnEntryAdded;
    }

    private void InitializeComponents()
    {
        Text = "GPU Auto Reconnect - Log";
        ClientSize = new Size(700, 450);
        StartPosition = FormStartPosition.CenterScreen;

        _textBox = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 9f),
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.FromArgb(220, 220, 220),
            WordWrap = false
        };

        var buttonPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            Dock = DockStyle.Bottom,
            Height = 40,
            Padding = new Padding(8, 4, 8, 4)
        };

        var openFolderBtn = new Button { Text = "Open Log Folder", AutoSize = true };
        openFolderBtn.Click += (_, _) =>
        {
            try
            {
                System.Diagnostics.Process.Start("explorer.exe", _log.LogDirectory);
            }
            catch { }
        };

        var clearBtn = new Button { Text = "Clear", AutoSize = true };
        clearBtn.Click += (_, _) => _textBox.Clear();

        buttonPanel.Controls.Add(openFolderBtn);
        buttonPanel.Controls.Add(clearBtn);

        Controls.Add(_textBox);
        Controls.Add(buttonPanel);
    }

    private void LoadEntries()
    {
        var entries = _log.GetRecentEntries();
        _textBox.Text = string.Join(Environment.NewLine, entries);
        ScrollToEnd();
    }

    private void OnEntryAdded(string entry)
    {
        if (IsDisposed) return;

        if (InvokeRequired)
        {
            BeginInvoke(() => AppendEntry(entry));
        }
        else
        {
            AppendEntry(entry);
        }
    }

    private void AppendEntry(string entry)
    {
        _textBox.AppendText(entry + Environment.NewLine);
        ScrollToEnd();
    }

    private void ScrollToEnd()
    {
        _textBox.SelectionStart = _textBox.TextLength;
        _textBox.ScrollToCaret();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _log.EntryAdded -= OnEntryAdded;
        base.OnFormClosed(e);
    }
}
