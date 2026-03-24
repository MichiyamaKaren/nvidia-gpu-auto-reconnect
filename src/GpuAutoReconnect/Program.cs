using GpuAutoReconnect.UI;

namespace GpuAutoReconnect;

static class Program
{
    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, "GpuAutoReconnect_SingleInstance", out bool createdNew);
        if (!createdNew)
            return;

        Application.ThreadException += (_, e) =>
        {
            try
            {
                var logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "GpuAutoReconnect", "logs");
                Directory.CreateDirectory(logDir);
                File.AppendAllText(
                    Path.Combine(logDir, "crash.log"),
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] FATAL: {e.Exception}\n");
            }
            catch { }

            MessageBox.Show(
                $"An unexpected error occurred:\n{e.Exception.Message}",
                "GPU Auto Reconnect",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            try
            {
                var logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "GpuAutoReconnect", "logs");
                Directory.CreateDirectory(logDir);
                File.AppendAllText(
                    Path.Combine(logDir, "crash.log"),
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] FATAL: {e.ExceptionObject}\n");
            }
            catch { }
        };

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext());
    }
}
