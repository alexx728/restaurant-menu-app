namespace Restaurant.Domain.Models;

public sealed class Order
{
    private readonly List<OrderLine> _lines = [];

    public Guid Id { get; }
    public int TableNumber { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public OrderStatus Status { get; private set; } = OrderStatus.New;
    public IReadOnlyList<OrderLine> Lines { get; }
    public decimal Total => _lines.Sum(line => line.Total);

    public Order(int tableNumber, Guid? id = null, DateTimeOffset? createdAt = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Ідентифікатор замовлення не може бути порожнім.", nameof(id));
        if (createdAt == default(DateTimeOffset))
            throw new ArgumentException("Потрібно вказати час замовлення.", nameof(createdAt));

        ChangeTable(tableNumber);
        Id = id ?? Guid.NewGuid();
        CreatedAt = createdAt ?? DateTimeOffset.Now;
        Lines = _lines.AsReadOnly();
    }

    public void ChangeTable(int tableNumber)
    {
        EnsureEditable();
        if (tableNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(tableNumber), "Номер столу має бути додатним.");

        TableNumber = tableNumber;
    }

    public void AddLine(MenuItem item, int quantity)
    {
        EnsureEditable();
        ArgumentNullException.ThrowIfNull(item);
        if (!item.IsAvailable)
            throw new InvalidOperationException("Позиція меню недоступна.");
        if (item is ComboSet combo && combo.Items.Any(part => !part.IsAvailable))
            throw new InvalidOperationException("Один зі складників комбо недоступний.");

        var line = new OrderLine(Guid.NewGuid(), item.Id, item.Name, item.Category, item.CalculatePrice(), quantity);
        _ = checked(Total + line.Total);
        _lines.Add(line);
    }

    public void ChangeQuantity(Guid lineId, int quantity)
    {
        EnsureEditable();
        int index = FindLine(lineId);
        OrderLine replacement = _lines[index].WithQuantity(quantity);
        _ = _lines.Select((line, i) => i == index ? replacement.Total : line.Total).Sum();
        _lines[index] = replacement;
    }

    public void RemoveLine(Guid lineId)
    {
        EnsureEditable();
        _lines.RemoveAt(FindLine(lineId));
    }

    public void ChangeStatus(OrderStatus next)
    {
        bool allowed = Status switch
        {
            OrderStatus.New => next is OrderStatus.Preparing or OrderStatus.Cancelled,
            OrderStatus.Preparing => next is OrderStatus.Served or OrderStatus.Cancelled,
            OrderStatus.Served => next == OrderStatus.Paid,
            _ => false
        };
        if (!allowed)
            throw new InvalidOperationException($"Перехід зі статусу {Status} у {next} заборонений.");
        if (next == OrderStatus.Preparing && _lines.Count == 0)
            throw new InvalidOperationException("Не можна почати приготування порожнього замовлення.");

        Status = next;
    }

    internal void RestoreLines(IEnumerable<OrderLine> lines, OrderStatus status)
    {
        ArgumentNullException.ThrowIfNull(lines);
        if (!Enum.IsDefined(status))
            throw new ArgumentException("Невідомий статус замовлення.", nameof(status));

        var restored = new List<OrderLine>();
        var ids = new HashSet<Guid>();
        decimal total = 0m;
        foreach (OrderLine line in lines)
        {
            if (line is null || !ids.Add(line.Id))
                throw new ArgumentException("Рядок відсутній або його ідентифікатор повторюється.", nameof(lines));
            total = checked(total + line.Total);
            restored.Add(line);
        }

        if (restored.Count == 0 && status is not OrderStatus.New and not OrderStatus.Cancelled)
            throw new ArgumentException("Замовлення в цьому статусі має містити позиції.", nameof(lines));

        _lines.Clear();
        _lines.AddRange(restored);
        Status = status;
    }

    private int FindLine(Guid id)
    {
        int index = _lines.FindIndex(line => line.Id == id);
        if (index < 0)
            throw new KeyNotFoundException("Рядок замовлення не знайдено.");
        return index;
    }

    private void EnsureEditable()
    {
        if (Status != OrderStatus.New)
            throw new InvalidOperationException("Змінювати склад і стіл можна лише в новому замовленні.");
    }
}
