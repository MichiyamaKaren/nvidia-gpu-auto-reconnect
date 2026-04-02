using System.Diagnostics;
using GpuAutoReconnect.Models;

namespace GpuAutoReconnect.Services;

public class GpuMonitorService : IDisposable
{
    private readonly SettingsService _settings;
    private readonly LogService _log;
    private readonly GpuDeviceService _deviceService;
    private System.Threading.Timer? _timer;
    private bool _paused;
    private int _consecutiveBadChecks;
    private bool _resetInProgress;
    private bool _disposed;

    public bool IsPaused => _paused;
    public GpuStatus? LastStatus { get; private set; }

    public event Action<GpuStatus>? GpuStatusChecked;
    public event Action? GpuResetStarted;
    public event Action<bool>? GpuResetCompleted;
    public event Action<string>? MonitorError;

    public GpuMonitorService(SettingsService settings, LogService log, GpuDeviceService deviceService)
    {
        _settings = settings;
        _log = log;
        _deviceService = deviceService;
    }

    public void Start()
    {
        _timer = new System.Threading.Timer(OnTimerCallback, null, TimeSpan.Zero, Timeout.InfiniteTimeSpan);
    }

    public void Pause()
    {
        _paused = true;
        _log.Info("Monitoring paused.");
    }

    public void Resume()
    {
        _paused = false;
        _consecutiveBadChecks = 0;
        _log.Info("Monitoring resumed.");
        RearmTimer();
    }

    private async void OnTimerCallback(object? state)
    {
        if (_paused || _resetInProgress)
        {
            RearmTimer();
            return;
        }

        try
        {
            var status = await QueryGpuStatus();
            _log.Debug($"GPU status detected: PState: {status.PState}, Power Capacity: {status.RatedPower} W.");
            LastStatus = status;
            GpuStatusChecked?.Invoke(status);

            var isLow = IsLowPerformance(status);
            var isOnAC = PowerService.IsOnACPower();

            if (isLow && isOnAC && _settings.Current.AutoResetEnabled)
            {
                _consecutiveBadChecks++;
                var reason = DescribeLowPerformance(status);
                _log.Info($"Low performance detected: {reason} (check {_consecutiveBadChecks}/{_settings.Current.ConsecutiveChecksBeforeReset})");

                if (_consecutiveBadChecks >= _settings.Current.ConsecutiveChecksBeforeReset)
                {
                    await PerformGpuReset();
                    _consecutiveBadChecks = 0;
                }
            }
            else
            {
                if (_consecutiveBadChecks > 0)
                    _log.Info($"GPU recovered: {status.PState}, rated {status.RatedPower:F0}W. Counter reset.");
                _consecutiveBadChecks = 0;
            }
        }
        catch (Exception ex)
        {
            _log.Error($"Monitor error: {ex.Message}");
            MonitorError?.Invoke(ex.Message);
        }
        finally
        {
            RearmTimer();
        }
    }

    private bool IsLowPerformance(GpuStatus status)
    {
        var cfg = _settings.Current;
        var pstateBad = (int)status.PState >= (int)cfg.PStateThreshold;
        var powerBad = status.RatedPower > 0 && status.RatedPower < cfg.PowerThresholdWatts;

        return cfg.ResetCondition switch
        {
            ResetCondition.PState => pstateBad,
            ResetCondition.Power => powerBad,
            ResetCondition.Either => pstateBad || powerBad,
            _ => pstateBad
        };
    }

    private string DescribeLowPerformance(GpuStatus status)
    {
        var cfg = _settings.Current;
        var parts = new List<string>();

        if ((int)status.PState >= (int)cfg.PStateThreshold)
            parts.Add($"{status.PState} (threshold: {cfg.PStateThreshold})");

        if (status.RatedPower > 0 && status.RatedPower < cfg.PowerThresholdWatts)
            parts.Add($"rated power {status.RatedPower:F0}W < threshold {cfg.PowerThresholdWatts}W");

        return string.Join(", ", parts);
    }

    private async Task PerformGpuReset()
    {
        var instanceId = _deviceService.FindNvidiaDeviceInstanceId();
        if (instanceId == null)
        {
            _log.Error("Cannot reset: NVIDIA GPU device not found.");
            return;
        }

        _resetInProgress = true;
        GpuResetStarted?.Invoke();

        try
        {
            _log.Info($"Disabling GPU: {instanceId}");
            var disabled = await _deviceService.DisableDevice(instanceId);
            if (!disabled)
            {
                _log.Error("Failed to disable GPU device.");
                GpuResetCompleted?.Invoke(false);
                return;
            }

            var delay = _settings.Current.DeviceReEnableDelaySeconds;
            _log.Info($"Waiting {delay} seconds before re-enabling...");
            await Task.Delay(TimeSpan.FromSeconds(delay));

            _log.Info("Enabling GPU device...");
            var enabled = await _deviceService.EnableDevice(instanceId);

            if (enabled)
            {
                _log.Info("GPU reset completed successfully.");
                _deviceService.ClearCache();
            }
            else
            {
                _log.Error("Failed to re-enable GPU device.");
            }

            GpuResetCompleted?.Invoke(enabled);
        }
        finally
        {
            _resetInProgress = false;
        }
    }

    private async Task<GpuStatus> QueryGpuStatus()
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "nvidia-smi",
            Arguments = "--query-gpu=pstate,enforced.power.limit --format=csv,noheader,nounits",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill();
            throw new TimeoutException("nvidia-smi timed out");
        }

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"nvidia-smi exited with code {process.ExitCode}");

        var line = output.Trim();
        // Handle multi-GPU: take the first line
        if (line.Contains('\n'))
            line = line.Split('\n')[0].Trim();

        // Format: "P8, 170.00"
        var parts = line.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
            throw new FormatException($"Unexpected nvidia-smi output: '{line}'");

        if (!Enum.TryParse<PState>(parts[0], true, out var pstate))
            throw new FormatException($"Could not parse P-state from: '{parts[0]}'");

        double.TryParse(parts[1], System.Globalization.CultureInfo.InvariantCulture, out var ratedPower);

        return new GpuStatus
        {
            PState = pstate,
            RatedPower = ratedPower
        };
    }

    private void RearmTimer()
    {
        if (_disposed) return;
        var interval = _consecutiveBadChecks > 0
            ? TimeSpan.FromSeconds(_settings.Current.DebounceIntervalSeconds)
            : TimeSpan.FromSeconds(_settings.Current.CheckIntervalSeconds);
        _timer?.Change(interval, Timeout.InfiniteTimeSpan);
    }

    public void Dispose()
    {
        _disposed = true;
        _timer?.Dispose();
        _timer = null;
    }
}
