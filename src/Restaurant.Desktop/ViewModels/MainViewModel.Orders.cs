using Restaurant.Domain.Models;

namespace Restaurant.Desktop.ViewModels;

public sealed partial class MainViewModel
{
    private OrderRow? _selectedOrder;
    private OrderLine? _selectedLine;
    private MenuItem? _selectedOrderItem;
    private StatusChoice? _statusFilter;
    private StatusChoice? _nextStatus;
    private string _tableNumber = "1";
    private string _quantity = "1";
    private bool _sortOrdersByTotal;
    private bool _ordersDescending = true;

    public IReadOnlyList<MenuItem> AvailableItems { get; private set; } = [];
    public IReadOnlyList<StatusChoice> Statuses => StatusChoice.All;
    public IReadOnlyList<StatusChoice> StatusFilters { get; } = [new(null, "Усі статуси"), ..StatusChoice.All];
    public MenuItem? SelectedOrderItem { get => _selectedOrderItem; set => Set(ref _selectedOrderItem, value); }
    public StatusChoice? StatusFilter { get => _statusFilter; set => Set(ref _statusFilter, value); }
    public StatusChoice? NextStatus { get => _nextStatus; set => Set(ref _nextStatus, value); }
    public string TableNumber { get => _tableNumber; set => Set(ref _tableNumber, value); }
    public string Quantity { get => _quantity; set => Set(ref _quantity, value); }
    public bool SortOrdersByTotal { get => _sortOrdersByTotal; set => Set(ref _sortOrdersByTotal, value); }
    public bool OrdersDescending { get => _ordersDescending; set => Set(ref _ordersDescending, value); }
    public bool CanEditOrder => SelectedOrder?.Item.Status == OrderStatus.New;
    public string OrderHeading => SelectedOrder is null ? "Виберіть замовлення" : $"Стіл {SelectedOrder.Table} · {SelectedOrder.Status}";
    public string OrderTotal => $"Разом: {SelectedOrder?.Total ?? 0m:0.00} грн";

    public OrderRow? SelectedOrder
    {
        get => _selectedOrder;
        set
        {
            Set(ref _selectedOrder, value);
            Lines.Clear();
            SelectedLine = null;
            if (value is not null)
            {
                TableNumber = value.Table.ToString();
                foreach (OrderLine line in value.Item.Lines)
                    Lines.Add(line);
            }
            OrderStatus? next = value?.Item.Status switch
            {
                OrderStatus.New => OrderStatus.Preparing,
                OrderStatus.Preparing => OrderStatus.Served,
                OrderStatus.Served => OrderStatus.Paid,
                _ => null
            };
            NextStatus = Statuses.FirstOrDefault(choice => choice.Value == next);
            Notify(nameof(CanEditOrder));
            Notify(nameof(OrderHeading));
            Notify(nameof(OrderTotal));
            RefreshCommands();
        }
    }

    public OrderLine? SelectedLine
    {
        get => _selectedLine;
        set
        {
            Set(ref _selectedLine, value);
            if (value is not null)
                Quantity = value.Quantity.ToString();
            RefreshCommands();
        }
    }

    public AsyncCommand NewOrderCommand { get; private set; } = null!;
    public AsyncCommand ChangeTableCommand { get; private set; } = null!;
    public AsyncCommand AddLineCommand { get; private set; } = null!;
    public AsyncCommand ChangeQuantityCommand { get; private set; } = null!;
    public AsyncCommand RemoveLineCommand { get; private set; } = null!;
    public AsyncCommand ChangeStatusCommand { get; private set; } = null!;
    public AsyncCommand DeleteOrderCommand { get; private set; } = null!;

    private Guid OrderId => SelectedOrder?.Item.Id ?? throw new InvalidOperationException("Виберіть замовлення.");
    private Guid LineId => SelectedLine?.Id ?? throw new InvalidOperationException("Виберіть рядок замовлення.");

    private void InitializeOrderCommands()
    {
        NewOrderCommand = Command(CreateOrder);
        ChangeTableCommand = Command(() =>
        {
            _service.ChangeTable(OrderId, Number(TableNumber));
            Changed("Номер столу змінено.");
        }, () => CanEditOrder);
        AddLineCommand = Command(() =>
        {
            Guid itemId = SelectedOrderItem?.Id ?? throw new InvalidOperationException("Виберіть позицію меню.");
            _service.AddLine(OrderId, itemId, Number(Quantity));
            Changed("Позицію додано до замовлення.");
        }, () => CanEditOrder);
        ChangeQuantityCommand = Command(() =>
        {
            _service.ChangeQuantity(OrderId, LineId, Number(Quantity));
            Changed("Кількість змінено.");
        }, () => CanEditOrder && SelectedLine is not null);
        RemoveLineCommand = Command(() =>
        {
            _service.RemoveLine(OrderId, LineId);
            Changed("Рядок вилучено із замовлення.");
        }, () => CanEditOrder && SelectedLine is not null);
        ChangeStatusCommand = Command(() =>
        {
            OrderStatus status = NextStatus?.Value ?? throw new InvalidOperationException("Виберіть новий статус.");
            _service.ChangeStatus(OrderId, status);
            Changed("Статус замовлення змінено.");
        }, () => SelectedOrder?.Item.Status is OrderStatus.New or OrderStatus.Preparing or OrderStatus.Served);
        DeleteOrderCommand = Command(DeleteOrderAsync,
            () => SelectedOrder?.Item.Status is OrderStatus.New or OrderStatus.Cancelled);
    }

    private void CreateOrder()
    {
        Guid id = _service.CreateOrder(Number(TableNumber));
        StatusFilter = StatusFilters[0];
        Changed("Створено нове замовлення.");
        SelectedOrder = OrderRows.Single(row => row.Item.Id == id);
    }

    private async Task DeleteOrderAsync()
    {
        Guid id = OrderId;
        if (await ConfirmAsync("Видалити вибране замовлення?"))
        {
            _service.DeleteOrder(id);
            Changed("Замовлення видалено.");
        }
    }

    private static int Number(string text)
    {
        if (!int.TryParse(text, out int value))
            throw new ArgumentException("Введіть ціле число.");
        return value;
    }
}
