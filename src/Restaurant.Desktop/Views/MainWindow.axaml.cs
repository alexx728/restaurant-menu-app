using Avalonia.Controls;
using Restaurant.Desktop.ViewModels;
using Serilog;

namespace Restaurant.Desktop.Views;

public partial class MainWindow : Window
{
    private bool _allowClose;
    private bool _confirmingClose;

    public MainWindow()
    {
        InitializeComponent();
        Closing += ConfirmClose;
    }

    private async void ConfirmClose(object? sender, WindowClosingEventArgs args)
    {
        if (_allowClose || DataContext is not MainViewModel model)
            return;
        if (!model.IsBusy && !model.ConfirmUnsaved)
            return;

        args.Cancel = true;
        if (model.IsBusy || _confirmingClose)
            return;

        _confirmingClose = true;
        try
        {
            bool accepted = await new DialogService(this)
                .ConfirmAsync("Є незбережені зміни. Закрити програму без збереження?");
            if (accepted && !model.IsBusy)
            {
                _allowClose = true;
                Close();
            }
        }
        catch (Exception error)
        {
            Log.Error(error, "Не вдалося підтвердити закриття");
        }
        finally
        {
            _confirmingClose = false;
        }
    }
}
