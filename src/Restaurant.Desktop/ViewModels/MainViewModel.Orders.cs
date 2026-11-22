using Restaurant.Domain.Models;

namespace Restaurant.Desktop.ViewModels;
public sealed partial class MainViewModel
{
    private OrderRow? _selectedOrder;
    private string _tableNumber = "1";
    private string _quantity = "1";
    public OrderRow? SelectedOrder
    {
        get => _selectedOrder;
        set
        {
            Set(ref _selectedOrder, value);
            Lines.Clear();
            SelectedLine = null;
            Notify(nameof(SelectedLine));
            if (value is not null)
            {
                TableNumber = value.Table.ToString();
                foreach (var line in value.Item.Lines)
                    Lines.Add(line);
            }
        }
    }

    public OrderLine? SelectedLine { get; set; }
    public MenuItem? SelectedOrderItem { get; set; }
    public IReadOnlyList<MenuItem> AvailableItems { get; private set; } = [];
    public string TableNumber { get => _tableNumber; set => Set(ref _tableNumber, value); }
    public string Quantity { get => _quantity; set => Set(ref _quantity, value); }
    public StatusChoice[] Statuses => StatusChoice.All;
    public StatusChoice[] StatusFilters { get; } = [new(null, "Усі статуси"), ..StatusChoice.All];
    public StatusChoice? StatusFilter { get; set; }
    public StatusChoice? NextStatus { get; set; }
    public bool SortOrdersByTotal { get; set; }
    public bool OrdersDescending { get; set; } = true;
    public AsyncCommand NewOrderCommand { get; private set; } = null!;
    public AsyncCommand ChangeTableCommand { get; private set; } = null!;
    public AsyncCommand AddLineCommand { get; private set; } = null!;
    public AsyncCommand ChangeQuantityCommand { get; private set; } = null!;
    public AsyncCommand RemoveLineCommand { get; private set; } = null!;
    public AsyncCommand ChangeStatusCommand { get; private set; } = null!;
    public AsyncCommand DeleteOrderCommand { get; private set; } = null!;
    private Guid OrderId => SelectedOrder?.Item.Id ?? throw new InvalidOperationException("Виберіть замовлення.");
    private Guid LineId => SelectedLine?.Id ?? throw new InvalidOperationException("Виберіть позицію замовлення.");

    private static int Number(string text) => int.TryParse(text, out int number) ? number : throw new ArgumentException("Введіть ціле число.");
    private void InitializeOrderCommands()
    {
        NewOrderCommand = Command(() =>
        {
            Guid id = _service.CreateOrder(Number(TableNumber));
            StatusFilter = StatusFilters[0];
            Notify(nameof(StatusFilter));
            Changed("Створено нове замовлення.");
            SelectedOrder = OrderRows.First(x => x.Item.Id == id);
        });
        ChangeTableCommand = Command(() =>
        {
            _service.ChangeTable(OrderId, Number(TableNumber));
            Changed("Номер столу змінено.");
        });
        AddLineCommand = Command(() =>
        {
            Guid itemId = SelectedOrderItem?.Id ?? throw new InvalidOperationException("Виберіть позицію меню.");
            _service.AddLine(OrderId, itemId, Number(Quantity));
            Changed("Позицію додано до замовлення.");
        });
        ChangeQuantityCommand = Command(() =>
        {
            _service.ChangeQuantity(OrderId, LineId, Number(Quantity));
            Changed("Кількість змінено.");
        });
        RemoveLineCommand = Command(() =>
        {
            _service.RemoveLine(OrderId, LineId);
            Changed("Позицію вилучено із замовлення.");
        });
        ChangeStatusCommand = Command(() =>
        {
            var status = NextStatus?.Value ?? throw new InvalidOperationException("Виберіть новий статус.");
            _service.ChangeStatus(OrderId, status);
            Changed("Статус змінено.");
        });
        DeleteOrderCommand = Command(async () =>
        {
            Guid id = OrderId;
            if (!await _confirm("Видалити вибране замовлення?"))
                return;
            _service.DeleteOrder(id);
            Changed("Замовлення видалено.");
        });
    }
}
