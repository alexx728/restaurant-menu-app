using Avalonia;
using Serilog;

namespace Restaurant.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        string logPath = Path.Combine("logs", "restaurant-.log");
        Log.Logger = new LoggerConfiguration()
            .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
            .CreateLogger();
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception error)
        {
            Log.Fatal(error, "Застосунок завершився з помилкою");
            throw;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect();
}
