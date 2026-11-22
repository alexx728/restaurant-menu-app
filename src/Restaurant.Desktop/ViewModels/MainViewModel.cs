using System.Collections.ObjectModel;
using Avalonia.Threading;
using Restaurant.Domain.Models;
using Restaurant.Domain.Persistence;
using Restaurant.Domain.Services;
using Serilog;

namespace Restaurant.Desktop.ViewModels;
public sealed partial class MainViewModel : ViewModelBase
{
    private readonly RestaurantService _service = new();
    private readonly IRestaurantRepository _repository = new JsonRestaurantRepository();
    private readonly Func<string, Task<bool>> _confirm;
    private readonly List<AsyncCommand> _commands = [];
    private CancellationTokenSource? _cancellation;
    private bool _isBusy;
    private string _message = "Завантажено демонстраційні дані. Збережіть їх у JSON або імпортуйте власний файл.";
    private int _progress;
    private long _operation;
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            Set(ref _isBusy, value);
            Notify(nameof(IsIdle));
            foreach (var c in _commands)
                c.Refresh();
            CancelCommand.Refresh();
        }
    }

    public bool IsIdle => !IsBusy;
    public string Message { get => _message; private set => Set(ref _message, value); }
    public int Progress { get => _progress; private set => Set(ref _progress, value); }
    public string JsonPath { get; set; } = Path.Combine("data", "restaurant.json");
    public string XmlPath { get; set; } = Path.Combine("data", "sales-report.xml");
    public bool ConfirmUnsaved { get; private set; }
    public ObservableCollection<MenuRow> MenuRows { get; } = [];
    public ObservableCollection<OrderRow> OrderRows { get; } = [];
    public ObservableCollection<OrderLine> Lines { get; } = [];
    public ObservableCollection<CategorySales> ReportRows { get; } = [];
    public AsyncCommand CancelCommand { get; }

    public MainViewModel(Func<string, Task<bool>> confirm)
    {
        _confirm = confirm;
        CancelCommand = new(() =>
        {
            _cancellation?.Cancel();
            return Task.CompletedTask;
        }, () => IsBusy, ShowError);
        InitializeMenuCommands();
        InitializeOrderCommands();
        InitializeFileCommands();
        _service.Replace(new DemoDataGenerator().Generate(10), _service.Revision);
        ConfirmUnsaved = true;
        Refresh();
    }

    private AsyncCommand Command(Func<Task> action)
    {
        var command = new AsyncCommand(() => RunAsync(action), () => !IsBusy, ShowError);
        _commands.Add(command);
        return command;
    }

    private AsyncCommand Command(Action action) => Command(() =>
    {
        action();
        return Task.CompletedTask;
    });
    private async Task RunAsync(Func<Task> action)
    {
        _cancellation = new();
        IsBusy = true;
        Progress = 0;
        _operation++;
        try
        {
            await action();
        }
        catch (OperationCanceledException)
        {
            Message = "Операцію скасовано. Незавершений результат не застосовано.";
            Log.Information("Операцію скасовано");
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            _operation++;
            IsBusy = false;
            _cancellation.Dispose();
            _cancellation = null;
        }
    }

    private void ShowError(Exception ex)
    {
        Message = "Помилка: " + ex.Message;
        Log.Error(ex, "Помилка операції");
    }

    private CancellationToken Token => _cancellation?.Token ?? CancellationToken.None;

    private IProgress<OperationProgress> Reporter()
    {
        long operation = _operation;
        return new Progress<OperationProgress>(p => Dispatcher.UIThread.Post(() =>
        {
            if (operation != _operation)
                return;
            Progress = p.Percent;
        }));
    }

    private void Changed(string message)
    {
        ConfirmUnsaved = true;
        ReportRows.Clear();
        ReportSummary = "Дані змінено. Сформуйте звіт повторно.";
        Refresh();
        Message = message;
        Log.Information("{Operation}", message);
    }

    private void Refresh()
    {
        Guid? menuId = _selectedMenu?.Item.Id, orderId = _selectedOrder?.Item.Id;
        var snapshot = _service.Snapshot();
        Components = snapshot.Menu.Where(x => x is Dish or Drink).ToArray();
        Notify(nameof(Components));
        AvailableItems = snapshot.Menu.ToArray();
        Notify(nameof(AvailableItems));
        MenuRows.Clear();
        foreach (var item in _service.FindMenu(SearchText, CategoryFilter, PriceDescending))
            MenuRows.Add(new(item));
        OrderRows.Clear();
        foreach (var order in _service.FindOrders(StatusFilter?.Value, SortOrdersByTotal, OrdersDescending))
            OrderRows.Add(new(order));
        SelectedMenu = MenuRows.FirstOrDefault(x => x.Item.Id == menuId);
        SelectedOrder = OrderRows.FirstOrDefault(x => x.Item.Id == orderId);
    }
}
