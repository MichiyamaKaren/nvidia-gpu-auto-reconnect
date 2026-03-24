using System.Diagnostics;
using System.Management;

namespace GpuAutoReconnect.Services;

public class GpuDeviceService
{
    private string? _cachedInstanceId;
    private string? _cachedDeviceName;

    public string? DeviceName => _cachedDeviceName;

    public string? FindNvidiaDeviceInstanceId()
    {
        if (_cachedInstanceId != null) return _cachedInstanceId;

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT PNPDeviceID, Name FROM Win32_PnPEntity WHERE PNPClass = 'Display'");

            foreach (ManagementObject device in searcher.Get())
            {
                var pnpId = device["PNPDeviceID"]?.ToString();
                var name = device["Name"]?.ToString();

                if (pnpId != null && pnpId.Contains("VEN_10DE", StringComparison.OrdinalIgnoreCase))
                {
                    _cachedInstanceId = pnpId;
                    _cachedDeviceName = name;
                    return pnpId;
                }
            }
        }
        catch
        {
            // WMI query failed
        }

        return null;
    }

    public void ClearCache()
    {
        _cachedInstanceId = null;
        _cachedDeviceName = null;
    }

    public async Task<bool> DisableDevice(string instanceId)
    {
        var result = await RunPnpUtil($"/disable-device \"{instanceId}\" /force");
        return result.ExitCode == 0;
    }

    public async Task<bool> EnableDevice(string instanceId)
    {
        var result = await RunPnpUtil($"/enable-device \"{instanceId}\"");
        return result.ExitCode == 0;
    }

    private static async Task<(int ExitCode, string Output)> RunPnpUtil(string arguments)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "pnputil",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill();
            return (-1, "pnputil timed out");
        }

        var fullOutput = string.IsNullOrEmpty(error) ? output : $"{output}\n{error}";
        return (process.ExitCode, fullOutput.Trim());
    }
}
