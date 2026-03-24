using GpuAutoReconnect.Models;
using GpuAutoReconnect.Services;

namespace GpuAutoReconnect.UI;

public class SettingsForm : Form
{
    private readonly SettingsService _settings;

    private ComboBox _pstateCombo = null!;
    private NumericUpDown _intervalUpDown = null!;
    private CheckBox _autoResetCheck = null!;
    private NumericUpDown _consecutiveUpDown = null!;
    private NumericUpDown _delayUpDown = null!;
    private CheckBox _startupCheck = null!;

    public event Action? SettingsSaved;

    public SettingsForm(SettingsService settings)
    {
        _settings = settings;
        InitializeComponents();
        LoadSettings();
    }

    private void InitializeComponents()
    {
        Text = "GPU Auto Reconnect - Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(420, 320);

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 2,
            RowCount = 8,
            AutoSize = true
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));

        // Row 0: P-State Threshold
        table.Controls.Add(new Label
        {
            Text = "P-State Threshold:",
            Anchor = AnchorStyles.Left,
            AutoSize = true
        }, 0, 0);

        _pstateCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Anchor = AnchorStyles.Left | AnchorStyles.Right
        };
        foreach (var ps in Enum.GetValues<PState>())
            _pstateCombo.Items.Add(ps);
        table.Controls.Add(_pstateCombo, 1, 0);

        // Row 1: Check Interval
        table.Controls.Add(new Label
        {
            Text = "Check Interval (seconds):",
            Anchor = AnchorStyles.Left,
            AutoSize = true
        }, 0, 1);

        _intervalUpDown = new NumericUpDown
        {
            Minimum = 5,
            Maximum = 300,
            Anchor = AnchorStyles.Left | AnchorStyles.Right
        };
        table.Controls.Add(_intervalUpDown, 1, 1);

        // Row 2: Auto Reset
        _autoResetCheck = new CheckBox
        {
            Text = "Enable Auto Reset",
            Anchor = AnchorStyles.Left,
            AutoSize = true
        };
        table.Controls.Add(_autoResetCheck, 0, 2);
        table.SetColumnSpan(_autoResetCheck, 2);

        // Row 3: Consecutive Checks
        table.Controls.Add(new Label
        {
            Text = "Consecutive Checks Before Reset:",
            Anchor = AnchorStyles.Left,
            AutoSize = true
        }, 0, 3);

        _consecutiveUpDown = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 20,
            Anchor = AnchorStyles.Left | AnchorStyles.Right
        };
        table.Controls.Add(_consecutiveUpDown, 1, 3);

        // Row 4: Re-enable Delay
        table.Controls.Add(new Label
        {
            Text = "Re-enable Delay (seconds):",
            Anchor = AnchorStyles.Left,
            AutoSize = true
        }, 0, 4);

        _delayUpDown = new NumericUpDown
        {
            Minimum = 5,
            Maximum = 60,
            Anchor = AnchorStyles.Left | AnchorStyles.Right
        };
        table.Controls.Add(_delayUpDown, 1, 4);

        // Row 5: Run at Startup
        _startupCheck = new CheckBox
        {
            Text = "Run at Windows Startup",
            Anchor = AnchorStyles.Left,
            AutoSize = true
        };
        table.Controls.Add(_startupCheck, 0, 5);
        table.SetColumnSpan(_startupCheck, 2);

        // Row 6: spacer
        table.Controls.Add(new Label(), 0, 6);
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

        // Row 7: Buttons
        var buttonPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            AutoSize = true
        };

        var cancelBtn = new Button { Text = "Cancel", Width = 80 };
        cancelBtn.Click += (_, _) => Close();

        var saveBtn = new Button { Text = "Save", Width = 80 };
        saveBtn.Click += OnSave;

        buttonPanel.Controls.Add(cancelBtn);
        buttonPanel.Controls.Add(saveBtn);
        table.Controls.Add(buttonPanel, 0, 7);
        table.SetColumnSpan(buttonPanel, 2);

        Controls.Add(table);

        AcceptButton = saveBtn;
        CancelButton = cancelBtn;
    }

    private void LoadSettings()
    {
        var s = _settings.Current;
        _pstateCombo.SelectedItem = s.PStateThreshold;
        _intervalUpDown.Value = s.CheckIntervalSeconds;
        _autoResetCheck.Checked = s.AutoResetEnabled;
        _consecutiveUpDown.Value = s.ConsecutiveChecksBeforeReset;
        _delayUpDown.Value = s.DeviceReEnableDelaySeconds;
        _startupCheck.Checked = s.RunAtStartup;
    }

    private void OnSave(object? sender, EventArgs e)
    {
        var s = _settings.Current;
        s.PStateThreshold = (PState)_pstateCombo.SelectedItem!;
        s.CheckIntervalSeconds = (int)_intervalUpDown.Value;
        s.AutoResetEnabled = _autoResetCheck.Checked;
        s.ConsecutiveChecksBeforeReset = (int)_consecutiveUpDown.Value;
        s.DeviceReEnableDelaySeconds = (int)_delayUpDown.Value;

        var startupChanged = s.RunAtStartup != _startupCheck.Checked;
        s.RunAtStartup = _startupCheck.Checked;

        _settings.Save();

        if (startupChanged)
        {
            try
            {
                StartupService.SetRunAtStartup(s.RunAtStartup);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to update startup setting: {ex.Message}",
                    "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        SettingsSaved?.Invoke();
        Close();
    }
}
