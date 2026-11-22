using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Restaurant.Desktop.ViewModels;
using Restaurant.Desktop.Views;

namespace Restaurant.Desktop;
public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            var dialogs = new DialogService(window);
            window.DataContext = new MainViewModel(dialogs.ConfirmAsync);
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
