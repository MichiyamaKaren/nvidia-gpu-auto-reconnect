using GpuAutoReconnect.Models;
using GpuAutoReconnect.Services;

namespace GpuAutoReconnect.UI;

public class SettingsForm : Form
{
    private readonly SettingsService _settings;

    private ComboBox _conditionCombo = null!;
    private ComboBox _pstateCombo = null!;
    private NumericUpDown _powerThresholdUpDown = null!;
    private NumericUpDown _intervalUpDown = null!;
    private CheckBox _autoResetCheck = null!;
    private NumericUpDown _consecutiveUpDown = null!;
    private NumericUpDown _delayUpDown = null!;
    private CheckBox _startupCheck = null!;

    private Label _pstateLabel = null!;
    private Label _powerLabel = null!;

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
        ClientSize = new Size(420, 390);

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 2,
            RowCount = 10,
            AutoSize = true
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));

        int row = 0;

        // Row 0: Reset Condition
        table.Controls.Add(new Label
        {
            Text = "Reset Condition:",
            Anchor = AnchorStyles.Left,
            AutoSize = true
        }, 0, row);

        _conditionCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Anchor = AnchorStyles.Left | AnchorStyles.Right
        };
        _conditionCombo.Items.AddRange(new object[]
        {
            new ConditionItem(ResetCondition.PState, "P-State"),
            new ConditionItem(ResetCondition.Power, "Power Usage"),
            new ConditionItem(ResetCondition.Either, "Either (P-State or Power)")
        });
        _conditionCombo.SelectedIndexChanged += (_, _) => UpdateConditionVisibility();
        table.Controls.Add(_conditionCombo, 1, row);
        row++;

        // Row 1: P-State Threshold
        _pstateLabel = new Label
        {
            Text = "P-State Threshold:",
            Anchor = AnchorStyles.Left,
            AutoSize = true
        };
        table.Controls.Add(_pstateLabel, 0, row);

        _pstateCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Anchor = AnchorStyles.Left | AnchorStyles.Right
        };
        foreach (var ps in Enum.GetValues<PState>())
            _pstateCombo.Items.Add(ps);
        table.Controls.Add(_pstateCombo, 1, row);
        row++;

        // Row 2: Power Threshold
        _powerLabel = new Label
        {
            Text = "Rated Power Threshold (W):",
            Anchor = AnchorStyles.Left,
            AutoSize = true
        };
        table.Controls.Add(_powerLabel, 0, row);

        _powerThresholdUpDown = new NumericUpDown
        {
            Minimum = 10,
            Maximum = 1000,
            Anchor = AnchorStyles.Left | AnchorStyles.Right
        };
        table.Controls.Add(_powerThresholdUpDown, 1, row);
        row++;

        // Row 3: Check Interval
        table.Controls.Add(new Label
        {
            Text = "Check Interval (seconds):",
            Anchor = AnchorStyles.Left,
            AutoSize = true
        }, 0, row);

        _intervalUpDown = new NumericUpDown
        {
            Minimum = 5,
            Maximum = 300,
            Anchor = AnchorStyles.Left | AnchorStyles.Right
        };
        table.Controls.Add(_intervalUpDown, 1, row);
        row++;

        // Row 4: Auto Reset
        _autoResetCheck = new CheckBox
        {
            Text = "Enable Auto Reset",
            Anchor = AnchorStyles.Left,
            AutoSize = true
        };
        table.Controls.Add(_autoResetCheck, 0, row);
        table.SetColumnSpan(_autoResetCheck, 2);
        row++;

        // Row 5: Consecutive Checks
        table.Controls.Add(new Label
        {
            Text = "Consecutive Checks Before Reset:",
            Anchor = AnchorStyles.Left,
            AutoSize = true
        }, 0, row);

        _consecutiveUpDown = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 20,
            Anchor = AnchorStyles.Left | AnchorStyles.Right
        };
        table.Controls.Add(_consecutiveUpDown, 1, row);
        row++;

        // Row 6: Re-enable Delay
        table.Controls.Add(new Label
        {
            Text = "Re-enable Delay (seconds):",
            Anchor = AnchorStyles.Left,
            AutoSize = true
        }, 0, row);

        _delayUpDown = new NumericUpDown
        {
            Minimum = 5,
            Maximum = 60,
            Anchor = AnchorStyles.Left | AnchorStyles.Right
        };
        table.Controls.Add(_delayUpDown, 1, row);
        row++;

        // Row 7: Run at Startup
        _startupCheck = new CheckBox
        {
            Text = "Run at Windows Startup",
            Anchor = AnchorStyles.Left,
            AutoSize = true
        };
        table.Controls.Add(_startupCheck, 0, row);
        table.SetColumnSpan(_startupCheck, 2);
        row++;

        // Row 8: spacer
        table.Controls.Add(new Label(), 0, row);
        row++;

        // Row styles
        for (int i = 0; i < row; i++)
        {
            if (i == row - 1) // spacer row
                table.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            else if (i == 4 || i == 7) // checkbox rows
                table.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            else
                table.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        }

        // Row 9: Buttons
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
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
        table.Controls.Add(buttonPanel, 0, row);
        table.SetColumnSpan(buttonPanel, 2);

        Controls.Add(table);

        AcceptButton = saveBtn;
        CancelButton = cancelBtn;
    }

    private void UpdateConditionVisibility()
    {
        var selected = ((ConditionItem)_conditionCombo.SelectedItem!).Value;

        var showPState = selected is ResetCondition.PState or ResetCondition.Either;
        var showPower = selected is ResetCondition.Power or ResetCondition.Either;

        _pstateLabel.Visible = showPState;
        _pstateCombo.Visible = showPState;
        _powerLabel.Visible = showPower;
        _powerThresholdUpDown.Visible = showPower;
    }

    private void LoadSettings()
    {
        var s = _settings.Current;

        // Select the matching ConditionItem
        for (int i = 0; i < _conditionCombo.Items.Count; i++)
        {
            if (((ConditionItem)_conditionCombo.Items[i]!).Value == s.ResetCondition)
            {
                _conditionCombo.SelectedIndex = i;
                break;
            }
        }

        _pstateCombo.SelectedItem = s.PStateThreshold;
        _powerThresholdUpDown.Value = s.PowerThresholdWatts;
        _intervalUpDown.Value = s.CheckIntervalSeconds;
        _autoResetCheck.Checked = s.AutoResetEnabled;
        _consecutiveUpDown.Value = s.ConsecutiveChecksBeforeReset;
        _delayUpDown.Value = s.DeviceReEnableDelaySeconds;
        _startupCheck.Checked = s.RunAtStartup;

        UpdateConditionVisibility();
    }

    private void OnSave(object? sender, EventArgs e)
    {
        var s = _settings.Current;
        s.ResetCondition = ((ConditionItem)_conditionCombo.SelectedItem!).Value;
        s.PStateThreshold = (PState)_pstateCombo.SelectedItem!;
        s.PowerThresholdWatts = (int)_powerThresholdUpDown.Value;
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

    private record ConditionItem(ResetCondition Value, string Label)
    {
        public override string ToString() => Label;
    }
}
