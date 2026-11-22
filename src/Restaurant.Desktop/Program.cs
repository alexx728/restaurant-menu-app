using Avalonia;
using Serilog;

namespace Restaurant.Desktop;
internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration().WriteTo.File(Path.Combine("logs", "restaurant-.log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14).CreateLogger();
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Застосунок завершився з помилкою");
            throw;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect();
}
