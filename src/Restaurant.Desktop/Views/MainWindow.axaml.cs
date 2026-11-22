using Avalonia.Controls;
using Restaurant.Desktop.ViewModels;

namespace Restaurant.Desktop.Views;
public partial class MainWindow : Window
{
    private bool _closingApproved;
    private bool _asking;
    public MainWindow()
    {
        InitializeComponent();
        Closing += async (_, e) =>
        {
            if (_closingApproved || DataContext is not MainViewModel vm)
                return;
            if (vm.IsBusy)
            {
                e.Cancel = true;
                return;
            }

            if (!vm.ConfirmUnsaved)
                return;
            e.Cancel = true;
            if (_asking)
                return;
            _asking = true;
            try
            {
                if (await new DialogService(this).ConfirmAsync("Є незбережені дані. Закрити застосунок без збереження?"))
                {
                    _closingApproved = true;
                    Close();
                }
            }
            finally
            {
                _asking = false;
            }
        };
    }
}
