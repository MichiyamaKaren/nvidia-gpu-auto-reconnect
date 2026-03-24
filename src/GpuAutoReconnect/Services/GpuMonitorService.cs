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
    public PState? LastPState { get; private set; }

    public event Action<PState>? PStateChecked;
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
            var pstate = await QueryCurrentPState();
            LastPState = pstate;
            PStateChecked?.Invoke(pstate);

            var isLowPerformance = (int)pstate >= (int)_settings.Current.PStateThreshold;
            var isOnAC = PowerService.IsOnACPower();

            if (isLowPerformance && isOnAC && _settings.Current.AutoResetEnabled)
            {
                _consecutiveBadChecks++;
                _log.Info($"Low performance detected: {pstate} (check {_consecutiveBadChecks}/{_settings.Current.ConsecutiveChecksBeforeReset})");

                if (_consecutiveBadChecks >= _settings.Current.ConsecutiveChecksBeforeReset)
                {
                    await PerformGpuReset();
                    _consecutiveBadChecks = 0;
                }
            }
            else
            {
                if (_consecutiveBadChecks > 0)
                    _log.Info($"P-state recovered to {pstate}, counter reset.");
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

    private async Task<PState> QueryCurrentPState()
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "nvidia-smi",
            Arguments = "--query-gpu=pstate --format=csv,noheader",
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

        var trimmed = output.Trim();
        // Handle multi-GPU: take the first line
        if (trimmed.Contains('\n'))
            trimmed = trimmed.Split('\n')[0].Trim();

        if (!Enum.TryParse<PState>(trimmed, true, out var pstate))
            throw new FormatException($"Could not parse P-state from: '{trimmed}'");

        return pstate;
    }

    private void RearmTimer()
    {
        if (_disposed) return;
        _timer?.Change(
            TimeSpan.FromSeconds(_settings.Current.CheckIntervalSeconds),
            Timeout.InfiniteTimeSpan);
    }

    public void Dispose()
    {
        _disposed = true;
        _timer?.Dispose();
        _timer = null;
    }
}
