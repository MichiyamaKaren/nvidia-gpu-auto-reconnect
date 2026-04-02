namespace GpuAutoReconnect.Models;

public class AppSettings
{
    public ResetCondition ResetCondition { get; set; } = ResetCondition.PState;
    public PState PStateThreshold { get; set; } = PState.P8;
    public int PowerThresholdWatts { get; set; } = 100;
    public int CheckIntervalSeconds { get; set; } = 30;
    public int DebounceIntervalSeconds { get; set; } = 5;
    public bool AutoResetEnabled { get; set; } = true;
    public int ConsecutiveChecksBeforeReset { get; set; } = 3;
    public int DeviceReEnableDelaySeconds { get; set; } = 10;
    public bool RunAtStartup { get; set; } = false;
}
