namespace Restaurant.Domain.Models;
public sealed class Order
{
    private readonly List<OrderLine> _lines = [];
    public Guid Id { get; }
    public int TableNumber { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public OrderStatus Status { get; private set; }
    public IReadOnlyList<OrderLine> Lines => _lines.AsReadOnly();
    public decimal Total => _lines.Sum(line => line.Total);

    public Order(int tableNumber, Guid? id = null, DateTimeOffset? createdAt = null)
    {
        Id = id ?? Guid.NewGuid();
        if (Id == Guid.Empty)
            throw new ArgumentException("Порожній ідентифікатор.");
        CreatedAt = createdAt ?? DateTimeOffset.Now;
        if (CreatedAt == default)
            throw new ArgumentException("Час замовлення обов'язковий.");
        ChangeTable(tableNumber);
    }

    public void ChangeTable(int tableNumber)
    {
        EnsureEditable();
        if (tableNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(tableNumber), "Номер столу має бути додатним.");
        TableNumber = tableNumber;
    }

    public void AddLine(MenuItem item, int quantity)
    {
        EnsureEditable();
        ArgumentNullException.ThrowIfNull(item);
        if (!item.IsAvailable || item is ComboSet combo && combo.Items.Any(x => !x.IsAvailable))
            throw new InvalidOperationException("Позиція або складник набору недоступні.");
        var line = new OrderLine(Guid.NewGuid(), item.Id, item.Name, item.Category, item.CalculatePrice(), quantity);
        _ = checked(Total + line.Total);
        _lines.Add(line);
    }

    public void ChangeQuantity(Guid lineId, int quantity)
    {
        EnsureEditable();
        int index = FindLine(lineId);
        OrderLine replacement = _lines[index].WithQuantity(quantity);
        _ = checked(Total - _lines[index].Total + replacement.Total);
        _lines[index] = replacement;
    }

    public void RemoveLine(Guid lineId)
    {
        EnsureEditable();
        _lines.RemoveAt(FindLine(lineId));
    }

    public void ChangeStatus(OrderStatus next)
    {
        bool allowed = (Status, next) switch
        {
            (OrderStatus.New, OrderStatus.Preparing or OrderStatus.Cancelled) => true,
            (OrderStatus.Preparing, OrderStatus.Served or OrderStatus.Cancelled) => true,
            (OrderStatus.Served, OrderStatus.Paid) => true,
            _ => false
        };
        if (!allowed)
            throw new InvalidOperationException($"Перехід {Status} → {next} заборонений.");
        if (next != OrderStatus.Cancelled && _lines.Count == 0)
            throw new InvalidOperationException("Замовлення не містить позицій.");
        Status = next;
    }

    internal void RestoreLines(IEnumerable<OrderLine> lines, OrderStatus status)
    {
        if (!Enum.IsDefined(status))
            throw new ArgumentException("Невідомий статус.");
        OrderLine[] snapshot = lines.ToArray();
        if (snapshot.Select(x => x.Id).Distinct().Count() != snapshot.Length)
            throw new ArgumentException("Повторені ідентифікатори позицій.");
        if (status is not OrderStatus.New and not OrderStatus.Cancelled && snapshot.Length == 0)
            throw new ArgumentException("Завершене або активне замовлення порожнє.");
        _ = snapshot.Sum(x => x.Total);
        _lines.AddRange(snapshot);
        Status = status;
    }

    private int FindLine(Guid id)
    {
        int index = _lines.FindIndex(x => x.Id == id);
        return index >= 0 ? index : throw new KeyNotFoundException("Позицію не знайдено.");
    }

    private void EnsureEditable()
    {
        if (Status != OrderStatus.New)
            throw new InvalidOperationException("Редагувати можна лише нове замовлення.");
    }
}
