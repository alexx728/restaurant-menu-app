using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Restaurant.Desktop.ViewModels;
using Restaurant.Desktop.Views;

namespace Restaurant.Desktop;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
        {
            var mainWindow = new MainWindow();
            mainWindow.DataContext = new MainViewModel(new DialogService(mainWindow).ConfirmAsync);
            lifetime.MainWindow = mainWindow;
        }
        base.OnFrameworkInitializationCompleted();
    }
}
