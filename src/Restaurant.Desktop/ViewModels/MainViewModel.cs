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
    private long _operation;
    private bool _isBusy;
    private bool _confirmUnsaved;
    private int _progress;
    private string _message = "Відкрито демонстраційні дані. Можна змінити їх або імпортувати власний файл.";
    private string _jsonPath = Path.Combine("data", "restaurant.json");
    private string _xmlPath = Path.Combine("data", "sales-report.xml");

    public MainViewModel(Func<string, Task<bool>> confirm)
    {
        ArgumentNullException.ThrowIfNull(confirm);
        _confirm = confirm;
        CancelCommand = new AsyncCommand(() =>
        {
            _cancellation?.Cancel();
            return Task.CompletedTask;
        }, () => IsBusy, ShowError);
        InitializeMenuCommands();
        InitializeOrderCommands();
        InitializeFileCommands();
        _service.Replace(new DemoDataGenerator().Generate(10), 0);
        ConfirmUnsaved = true;
        Refresh();
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (Set(ref _isBusy, value))
            {
                Notify(nameof(IsIdle));
                RefreshCommands();
            }
        }
    }

    public bool IsIdle => !IsBusy;
    public bool ConfirmUnsaved
    {
        get => _confirmUnsaved;
        private set
        {
            if (Set(ref _confirmUnsaved, value))
                Notify(nameof(SaveState));
        }
    }

    public string SaveState => ConfirmUnsaved ? "Є незбережені зміни" : "Дані збережено";
    public string Message { get => _message; private set => Set(ref _message, value); }
    public int Progress { get => _progress; private set => Set(ref _progress, value); }
    public string JsonPath { get => _jsonPath; set => Set(ref _jsonPath, value); }
    public string XmlPath { get => _xmlPath; set => Set(ref _xmlPath, value); }
    public ObservableCollection<MenuRow> MenuRows { get; } = [];
    public ObservableCollection<OrderRow> OrderRows { get; } = [];
    public ObservableCollection<OrderLine> Lines { get; } = [];
    public ObservableCollection<CategorySales> ReportRows { get; } = [];
    public AsyncCommand CancelCommand { get; }
    private CancellationToken Token => _cancellation?.Token ?? CancellationToken.None;

    private AsyncCommand Command(Func<Task> action, Func<bool>? enabled = null)
    {
        var command = new AsyncCommand(() => RunAsync(action),
            () => IsIdle && (enabled?.Invoke() ?? true), ShowError);
        _commands.Add(command);
        return command;
    }

    private AsyncCommand Command(Action action, Func<bool>? enabled = null) =>
        Command(() => { action(); return Task.CompletedTask; }, enabled);

    private void RefreshCommands()
    {
        foreach (AsyncCommand command in _commands)
            command.Refresh();
        CancelCommand.Refresh();
    }

    private async Task RunAsync(Func<Task> action)
    {
        using var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        _operation++;
        Progress = 0;
        IsBusy = true;
        try
        {
            await action();
        }
        catch (OperationCanceledException)
        {
            Message = "Операцію скасовано.";
            Log.Information("Операцію скасовано");
        }
        finally
        {
            _operation++;
            _cancellation = null;
            IsBusy = false;
        }
    }

    private void ShowError(Exception error)
    {
        Message = "Помилка: " + error.Message;
        Log.Error(error, "Помилка операції");
    }

    private async Task<bool> ConfirmAsync(string question)
    {
        bool accepted = await _confirm(question);
        Token.ThrowIfCancellationRequested();
        return accepted;
    }

    private IProgress<OperationProgress> Reporter()
    {
        long operation = _operation;
        return new Progress<OperationProgress>(update => Dispatcher.UIThread.Post(() =>
        {
            if (operation == _operation && IsBusy)
            {
                Progress = update.Percent;
                Message = update.Message;
            }
        }));
    }

    private void Changed(string message)
    {
        ConfirmUnsaved = true;
        _report = null;
        ReportRows.Clear();
        ReportSummary = "Дані змінилися. Сформуйте звіт повторно.";
        Refresh();
        Message = message;
        Log.Information("{Operation}", message);
    }

    private void Refresh()
    {
        Guid? menuId = SelectedMenu?.Item.Id;
        Guid? orderId = SelectedOrder?.Item.Id;
        Guid? lineId = SelectedLine?.Id;
        Guid? itemId = SelectedOrderItem?.Id;
        RestaurantData data = _service.Snapshot();
        Components = data.Menu.Where(item => item is Dish or Drink).ToArray();
        AvailableItems = data.Menu.Where(item => item.IsAvailable &&
            (item is not ComboSet combo || combo.Items.All(part => part.IsAvailable))).ToArray();
        Notify(nameof(Components));
        Notify(nameof(AvailableItems));
        SelectedOrderItem = AvailableItems.FirstOrDefault(item => item.Id == itemId);

        MenuRows.Clear();
        foreach (MenuItem item in _service.FindMenu(SearchText, CategoryFilter, PriceDescending))
            MenuRows.Add(new MenuRow(item));
        OrderRows.Clear();
        foreach (Order order in _service.FindOrders(StatusFilter?.Value, SortOrdersByTotal, OrdersDescending))
            OrderRows.Add(new OrderRow(order));
        SelectedMenu = MenuRows.FirstOrDefault(row => row.Item.Id == menuId);
        SelectedOrder = OrderRows.FirstOrDefault(row => row.Item.Id == orderId);
        SelectedLine = Lines.FirstOrDefault(line => line.Id == lineId);
        RefreshCommands();
    }
}
