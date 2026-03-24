namespace GpuAutoReconnect.Models;

public class AppSettings
{
    public PState PStateThreshold { get; set; } = PState.P8;
    public int CheckIntervalSeconds { get; set; } = 30;
    public bool AutoResetEnabled { get; set; } = true;
    public int ConsecutiveChecksBeforeReset { get; set; } = 3;
    public int DeviceReEnableDelaySeconds { get; set; } = 10;
    public bool RunAtStartup { get; set; } = false;
}
