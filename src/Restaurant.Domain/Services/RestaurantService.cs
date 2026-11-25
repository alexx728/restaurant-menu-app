using Restaurant.Domain.Models;
using Restaurant.Domain.Persistence;

namespace Restaurant.Domain.Services;

public sealed class RestaurantService
{
    private readonly object _gate = new();
    private RestaurantData _data = new([], []);
    private long _revision;

    public long Revision
    {
        get
        {
            lock (_gate)
                return _revision;
        }
    }

    public RestaurantData Snapshot()
    {
        lock (_gate)
            return RestaurantMapper.FromDocument(RestaurantMapper.ToDocument(_data));
    }

    public void Replace(RestaurantData data, long expectedRevision)
    {
        ArgumentNullException.ThrowIfNull(data);
        RestaurantData replacement = RestaurantMapper.FromDocument(RestaurantMapper.ToDocument(data));
        lock (_gate)
        {
            if (expectedRevision != _revision)
                throw new InvalidOperationException("Дані змінилися під час імпорту. Повторіть операцію.");

            _data = replacement;
            _revision++;
        }
    }

    public void SaveMenu(MenuItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        Mutate(document =>
        {
            MenuItemData entry = RestaurantMapper.ToItem(item);
            int index = document.Menu.FindIndex(existing => existing.Id == entry.Id);
            if (index == -1)
                document.Menu.Add(entry);
            else
                document.Menu[index] = entry;
        });
    }

    public void DeleteMenu(Guid id) => Mutate(document =>
    {
        MenuItemData item = document.Menu.FirstOrDefault(item => item.Id == id)
            ?? throw new KeyNotFoundException("Позицію меню не знайдено.");
        if (document.Menu.Any(combo => combo.ItemIds.Contains(id)))
            throw new InvalidOperationException("Спочатку вилучіть позицію зі складу комбо.");

        document.Menu.Remove(item);
    });

    public Guid CreateOrder(int table)
    {
        var order = new Order(table);
        Mutate(document => document.Orders.Add(RestaurantMapper.ToOrder(order)));
        return order.Id;
    }

    public void ChangeTable(Guid orderId, int table) =>
        EditOrder(orderId, (order, _) => order.ChangeTable(table));

    public void AddLine(Guid orderId, Guid itemId, int quantity) =>
        EditOrder(orderId, (order, data) =>
        {
            MenuItem item = data.Menu.FirstOrDefault(item => item.Id == itemId)
                ?? throw new KeyNotFoundException("Позицію меню не знайдено.");
            order.AddLine(item, quantity);
        });

    public void ChangeQuantity(Guid orderId, Guid lineId, int quantity) =>
        EditOrder(orderId, (order, _) => order.ChangeQuantity(lineId, quantity));

    public void RemoveLine(Guid orderId, Guid lineId) =>
        EditOrder(orderId, (order, _) => order.RemoveLine(lineId));

    public void ChangeStatus(Guid orderId, OrderStatus status) =>
        EditOrder(orderId, (order, _) => order.ChangeStatus(status));

    public void DeleteOrder(Guid id) => Mutate(document =>
    {
        OrderData order = document.Orders.FirstOrDefault(order => order.Id == id)
            ?? throw new KeyNotFoundException("Замовлення не знайдено.");
        if (order.Status is not OrderStatus.New and not OrderStatus.Cancelled)
            throw new InvalidOperationException("Видаляти можна лише нові або скасовані замовлення.");

        document.Orders.Remove(order);
    });

    public IReadOnlyList<MenuItem> FindMenu(string text = "", string? category = null, bool descending = false)
    {
        ArgumentNullException.ThrowIfNull(text);
        string name = text.Trim();
        string? filter = category?.Trim();
        IEnumerable<MenuItem> items = Snapshot().Menu.Where(item =>
            item.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(filter))
            items = items.Where(item => string.Equals(item.Category, filter, StringComparison.OrdinalIgnoreCase));

        return (descending
            ? items.OrderByDescending(item => item.CalculatePrice())
            : items.OrderBy(item => item.CalculatePrice())).ToArray();
    }

    public IReadOnlyList<Order> FindOrders(OrderStatus? status = null, bool byTotal = false, bool descending = false)
    {
        IEnumerable<Order> orders = Snapshot().Orders;
        if (status.HasValue)
            orders = orders.Where(order => order.Status == status.Value);

        if (byTotal)
            return (descending
                ? orders.OrderByDescending(order => order.Total)
                : orders.OrderBy(order => order.Total)).ToArray();

        return (descending
            ? orders.OrderByDescending(order => order.CreatedAt)
            : orders.OrderBy(order => order.CreatedAt)).ToArray();
    }

    private void EditOrder(Guid id, Action<Order, RestaurantData> edit) => Mutate(document =>
    {
        int index = document.Orders.FindIndex(order => order.Id == id);
        if (index == -1)
            throw new KeyNotFoundException("Замовлення не знайдено.");

        RestaurantData working = RestaurantMapper.FromDocument(document);
        Order order = working.Orders[index];
        edit(order, working);
        document.Orders[index] = RestaurantMapper.ToOrder(order);
    });

    private void Mutate(Action<RestaurantDocument> change)
    {
        lock (_gate)
        {
            RestaurantDocument document = RestaurantMapper.ToDocument(_data);
            change(document);
            RestaurantData replacement = RestaurantMapper.FromDocument(document);
            _data = replacement;
            _revision++;
        }
    }
}
