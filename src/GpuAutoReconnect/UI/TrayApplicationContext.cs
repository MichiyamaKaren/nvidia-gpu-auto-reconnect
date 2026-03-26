using System.Drawing.Drawing2D;
using GpuAutoReconnect.Models;
using GpuAutoReconnect.Services;

namespace GpuAutoReconnect.UI;

public class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly ContextMenuStrip _contextMenu;
    private readonly GpuMonitorService _monitor;
    private readonly SettingsService _settings;
    private readonly LogService _log;
    private readonly GpuDeviceService _deviceService;

    private readonly ToolStripMenuItem _pauseMenuItem;
    private readonly ToolStripMenuItem _statusMenuItem;
    private SettingsForm? _settingsForm;
    private LogViewerForm? _logViewerForm;

    private readonly Icon _iconNormal;
    private readonly Icon _iconPaused;
    private readonly Icon _iconError;

    public TrayApplicationContext()
    {
        _iconNormal = CreateIcon(Color.FromArgb(76, 175, 80));   // green
        _iconPaused = CreateIcon(Color.FromArgb(255, 193, 7));   // yellow
        _iconError = CreateIcon(Color.FromArgb(244, 67, 54));    // red

        _log = new LogService();
        _log.Initialize();

        _settings = new SettingsService();
        _settings.Load();

        _deviceService = new GpuDeviceService();

        _log.Info("Application started.");

        var gpuId = _deviceService.FindNvidiaDeviceInstanceId();
        if (gpuId != null)
            _log.Info($"Found NVIDIA GPU: {_deviceService.DeviceName} ({gpuId})");
        else
            _log.Error("No NVIDIA GPU found.");

        // Build context menu
        _statusMenuItem = new ToolStripMenuItem("Initializing...")
        {
            Enabled = false
        };
        _pauseMenuItem = new ToolStripMenuItem("Pause Monitoring", null, OnPauseResume);

        _contextMenu = new ContextMenuStrip();
        _contextMenu.Items.Add(_statusMenuItem);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(_pauseMenuItem);
        _contextMenu.Items.Add("View Log...", null, OnViewLog);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add("Exit", null, OnExit);

        // Create tray icon
        _trayIcon = new NotifyIcon
        {
            Icon = gpuId != null ? _iconNormal : _iconError,
            Text = "GPU Auto Reconnect",
            ContextMenuStrip = _contextMenu,
            Visible = true
        };
        _trayIcon.MouseClick += OnTrayIconClick;

        if (gpuId == null)
        {
            _trayIcon.BalloonTipTitle = "GPU Auto Reconnect";
            _trayIcon.BalloonTipText = "No NVIDIA GPU found! Monitoring disabled.";
            _trayIcon.BalloonTipIcon = ToolTipIcon.Error;
            _trayIcon.ShowBalloonTip(5000);
            _statusMenuItem.Text = "No NVIDIA GPU found";
        }

        // Start monitoring
        _monitor = new GpuMonitorService(_settings, _log, _deviceService);
        _monitor.GpuStatusChecked += OnGpuStatusChecked;
        _monitor.GpuResetStarted += OnGpuResetStarted;
        _monitor.GpuResetCompleted += OnGpuResetCompleted;
        _monitor.MonitorError += OnMonitorError;
        _monitor.Start();
    }

    private void OnGpuStatusChecked(GpuStatus status)
    {
        if (_trayIcon.ContextMenuStrip?.InvokeRequired == true)
            _trayIcon.ContextMenuStrip.BeginInvoke(() => UpdateStatus(status));
        else
            UpdateStatus(status);
    }

    private void UpdateStatus(GpuStatus status)
    {
        var power = PowerService.IsOnACPower() ? "AC" : "Battery";
        _trayIcon.Text = $"GPU Auto Reconnect - {status.PState} | {status.RatedPower:F0}W ({power})";
        _statusMenuItem.Text = $"{status.PState} | Rated: {status.RatedPower:F0}W | {power}";
    }

    private void OnGpuResetStarted()
    {
        ShowBalloon("GPU Reset", "Disabling and re-enabling NVIDIA GPU...", ToolTipIcon.Warning);
    }

    private void OnGpuResetCompleted(bool success)
    {
        if (success)
            ShowBalloon("GPU Reset", "GPU reset completed successfully.", ToolTipIcon.Info);
        else
            ShowBalloon("GPU Reset", "GPU reset failed. Check log for details.", ToolTipIcon.Error);
    }

    private void OnMonitorError(string error)
    {
        if (_trayIcon.ContextMenuStrip?.InvokeRequired == true)
            _trayIcon.ContextMenuStrip.BeginInvoke(() =>
            {
                _trayIcon.Icon = _iconError;
                _statusMenuItem.Text = $"Error: {error}";
            });
        else
        {
            _trayIcon.Icon = _iconError;
            _statusMenuItem.Text = $"Error: {error}";
        }
    }

    private void ShowBalloon(string title, string text, ToolTipIcon icon)
    {
        _trayIcon.BalloonTipTitle = title;
        _trayIcon.BalloonTipText = text;
        _trayIcon.BalloonTipIcon = icon;
        _trayIcon.ShowBalloonTip(3000);
    }

    private void OnTrayIconClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
            OnSettings(sender, e);
    }

    private void OnPauseResume(object? sender, EventArgs e)
    {
        if (_monitor.IsPaused)
        {
            _monitor.Resume();
            _pauseMenuItem.Text = "Pause Monitoring";
            _trayIcon.Icon = _iconNormal;
        }
        else
        {
            _monitor.Pause();
            _pauseMenuItem.Text = "Resume Monitoring";
            _trayIcon.Icon = _iconPaused;
            _trayIcon.Text = "GPU Auto Reconnect - Paused";
            _statusMenuItem.Text = "Monitoring paused";
        }
    }

    private void OnSettings(object? sender, EventArgs e)
    {
        if (_settingsForm == null || _settingsForm.IsDisposed)
        {
            _settingsForm = new SettingsForm(_settings);
            _settingsForm.SettingsSaved += () => _log.Info("Settings updated.");
            _settingsForm.Show();
        }
        else
        {
            _settingsForm.BringToFront();
        }
    }

    private void OnViewLog(object? sender, EventArgs e)
    {
        if (_logViewerForm == null || _logViewerForm.IsDisposed)
        {
            _logViewerForm = new LogViewerForm(_log);
            _logViewerForm.Show();
        }
        else
        {
            _logViewerForm.BringToFront();
        }
    }

    private void OnExit(object? sender, EventArgs e)
    {
        _monitor.Dispose();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _log.Info("Application exiting.");
        _log.Dispose();
        Application.Exit();
    }

    private static Icon CreateIcon(Color color)
    {
        using var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(color);
        g.FillEllipse(brush, 1, 1, 14, 14);
        using var pen = new Pen(Color.FromArgb(60, 0, 0, 0), 1f);
        g.DrawEllipse(pen, 1, 1, 14, 14);
        return Icon.FromHandle(bmp.GetHicon());
    }
}
