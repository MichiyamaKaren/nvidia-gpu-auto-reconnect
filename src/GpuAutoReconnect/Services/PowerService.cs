namespace GpuAutoReconnect.Services;

public static class PowerService
{
    public static bool IsOnACPower()
    {
        return SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Online;
    }
}
