using System.Diagnostics;

namespace GpuAutoReconnect.Services;

public static class StartupService
{
    private const string TaskName = "GpuAutoReconnect";

    public static void SetRunAtStartup(bool enabled)
    {
        var exePath = Environment.ProcessPath ?? Application.ExecutablePath;

        if (enabled)
        {
            var args = $"/Create /TN \"{TaskName}\" /TR \"\\\"{exePath}\\\"\" /SC ONLOGON /RL HIGHEST /F";
            RunSchtasks(args);
        }
        else
        {
            RunSchtasks($"/Delete /TN \"{TaskName}\" /F");
        }
    }

    public static bool IsRunAtStartupEnabled()
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "schtasks",
                Arguments = $"/Query /TN \"{TaskName}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            process.Start();
            process.WaitForExit(5000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static void RunSchtasks(string arguments)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "schtasks",
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        process.Start();
        process.WaitForExit(10000);
    }
}
