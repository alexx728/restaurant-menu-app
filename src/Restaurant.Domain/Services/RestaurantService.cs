using Restaurant.Domain.Models;
using Restaurant.Domain.Persistence;

namespace Restaurant.Domain.Services;
public sealed class RestaurantService
{
    private readonly object _gate = new();
    private RestaurantDocument _document = new();
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
            return RestaurantMapper.FromDocument(_document);
    }

    public void Replace(RestaurantData data, long expectedRevision)
    {
        var next = RestaurantMapper.ToDocument(data);
        _ = RestaurantMapper.FromDocument(next);
        lock (_gate)
        {
            if (_revision != expectedRevision)
                throw new InvalidOperationException("Дані змінилися під час імпорту. Повторіть операцію.");
            _document = next;
            _revision++;
        }
    }

    public void SaveMenu(MenuItem item) => Mutate(document =>
    {
        int index = document.Menu.FindIndex(x => x.Id == item.Id);
        var savedItem = RestaurantMapper.ToItem(item);
        if (index < 0)
            document.Menu.Add(savedItem);
        else
            document.Menu[index] = savedItem;
    });
    public void DeleteMenu(Guid id) => Mutate(document =>
    {
        if (document.Menu.Any(x => x.ItemIds.Contains(id)))
            throw new InvalidOperationException("Позиція входить до комбо. Спочатку змініть склад комбо.");
        if (document.Menu.RemoveAll(x => x.Id == id) == 0)
            throw new KeyNotFoundException("Позицію не знайдено.");
    });
    public Guid CreateOrder(int table)
    {
        var order = new Order(table);
        Mutate(document => document.Orders.Add(RestaurantMapper.ToOrder(order)));
        return order.Id;
    }

    public void ChangeTable(Guid orderId, int table) => EditOrder(orderId, (order, _) => order.ChangeTable(table));
    public void AddLine(Guid orderId, Guid itemId, int quantity) => EditOrder(orderId, (order, data) => order.AddLine(data.Menu.FirstOrDefault(x => x.Id == itemId) ?? throw new KeyNotFoundException("Позицію меню не знайдено."), quantity));
    public void ChangeQuantity(Guid orderId, Guid lineId, int quantity) => EditOrder(orderId, (order, _) => order.ChangeQuantity(lineId, quantity));
    public void RemoveLine(Guid orderId, Guid lineId) => EditOrder(orderId, (order, _) => order.RemoveLine(lineId));
    public void ChangeStatus(Guid orderId, OrderStatus status) => EditOrder(orderId, (order, _) => order.ChangeStatus(status));
    public void DeleteOrder(Guid id) => Mutate(document =>
    {
        var order = document.Orders.FirstOrDefault(x => x.Id == id) ?? throw new KeyNotFoundException("Замовлення не знайдено.");
        if (order.Status is not OrderStatus.New and not OrderStatus.Cancelled)
            throw new InvalidOperationException("Видаляти можна лише нові або скасовані замовлення.");
        document.Orders.Remove(order);
    });
    public IReadOnlyList<MenuItem> FindMenu(string text = "", string? category = null, bool descending = false)
    {
        var query = Snapshot().Menu.Where(x => x.Name.Contains(text.Trim(), StringComparison.OrdinalIgnoreCase) && (string.IsNullOrWhiteSpace(category) || string.Equals(x.Category, category.Trim(), StringComparison.OrdinalIgnoreCase)));
        return (descending ? query.OrderByDescending(x => x.CalculatePrice()) : query.OrderBy(x => x.CalculatePrice())).ToArray();
    }

    public IReadOnlyList<Order> FindOrders(OrderStatus? status = null, bool byTotal = false, bool descending = false)
    {
        var query = Snapshot().Orders.Where(x => status is null || x.Status == status);
        if (byTotal)
            return (descending ? query.OrderByDescending(x => x.Total) : query.OrderBy(x => x.Total)).ToArray();
        return (descending ? query.OrderByDescending(x => x.CreatedAt) : query.OrderBy(x => x.CreatedAt)).ToArray();
    }

    private void EditOrder(Guid id, Action<Order, RestaurantData> action) => Mutate(document =>
    {
        var data = RestaurantMapper.FromDocument(document);
        var order = data.Orders.FirstOrDefault(x => x.Id == id) ?? throw new KeyNotFoundException("Замовлення не знайдено.");
        action(order, data);
        document.Orders[document.Orders.FindIndex(x => x.Id == id)] = RestaurantMapper.ToOrder(order);
    });
    private void Mutate(Action<RestaurantDocument> action)
    {
        lock (_gate)
        {
            var candidate = RestaurantMapper.ToDocument(RestaurantMapper.FromDocument(_document));
            action(candidate);
            _ = RestaurantMapper.FromDocument(candidate);
            _document = candidate;
            _revision++;
        }
    }
}
