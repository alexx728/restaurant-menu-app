using Restaurant.Domain.Models;

namespace Restaurant.Domain.Persistence;
public static class RestaurantMapper
{
    public static RestaurantDocument ToDocument(RestaurantData data) => new()
    {
        Menu = data.Menu.Select(ToItem).ToList(),
        Orders = data.Orders.Select(ToOrder).ToList()
    };
    public static MenuItemData ToItem(MenuItem item) => new()
    {
        Id = item.Id,
        Kind = item.GetType().Name,
        Name = item.Name,
        Category = item.Category,
        Price = item.Price,
        PortionSize = item.PortionSize,
        IsAvailable = item.IsAvailable,
        DiscountPercent = item is ComboSet combo ? combo.DiscountPercent : 0m,
        ItemIds = item is ComboSet set ? set.Items.Select(x => x.Id).ToList() : []
    };
    public static OrderData ToOrder(Order order) => new()
    {
        Id = order.Id,
        TableNumber = order.TableNumber,
        CreatedAt = order.CreatedAt,
        Status = order.Status,
        Lines = order.Lines.Select(x => new OrderLineData { Id = x.Id, MenuItemId = x.MenuItemId, Name = x.Name, Category = x.Category, UnitPrice = x.UnitPrice, Quantity = x.Quantity }).ToList()
    };
    public static RestaurantData FromDocument(RestaurantDocument document, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Version != 1 || document.Menu is null || document.Orders is null)
            throw new InvalidDataException("Невідома версія або відсутні колекції даних.");
        var menu = new Dictionary<Guid, MenuItem>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (MenuItemData item in document.Menu)
        {
            token.ThrowIfCancellationRequested();
            if (item is null || item.Id == Guid.Empty || !names.Add(item.Name?.Trim() ?? ""))
                throw new InvalidDataException("Некоректна позиція або повторена назва меню.");
            if (item.Kind == nameof(ComboSet))
                continue;
            if (item.ItemIds is null || item.ItemIds.Count != 0 || item.DiscountPercent != 0)
                throw new InvalidDataException("Знижка і склад дозволені лише для комбо.");
            MenuItem model = item.Kind switch
            {
                nameof(Dish) => new Dish(item.Name!, item.Category, item.Price, item.PortionSize, item.IsAvailable, item.Id),
                nameof(Drink) => new Drink(item.Name!, item.Category, item.Price, item.PortionSize, item.IsAvailable, item.Id),
                _ => throw new InvalidDataException("Невідомий тип позиції меню.")};
            if (!menu.TryAdd(item.Id, model))
                throw new InvalidDataException("Повторений ідентифікатор меню.");
        }

        foreach (MenuItemData item in document.Menu.Where(x => x.Kind == nameof(ComboSet)))
        {
            token.ThrowIfCancellationRequested();
            if (item.ItemIds is null)
                throw new InvalidDataException("Відсутній склад комбо.");
            var parts = item.ItemIds.Select(id => menu.TryGetValue(id, out var value) ? value : throw new InvalidDataException("Складник комбо не знайдено."));
            var combo = new ComboSet(item.Name, item.Category, item.Price, parts, item.DiscountPercent, item.IsAvailable, item.Id);
            combo.UpdateDetails(item.Name, item.Category, item.Price, item.PortionSize);
            if (!menu.TryAdd(item.Id, combo))
                throw new InvalidDataException("Повторений ідентифікатор меню.");
        }

        var orders = new List<Order>();
        var ids = new HashSet<Guid>();
        foreach (OrderData item in document.Orders)
        {
            token.ThrowIfCancellationRequested();
            if (item is null || item.Lines is null || !ids.Add(item.Id))
                throw new InvalidDataException("Некоректне або повторене замовлення.");
            var order = new Order(item.TableNumber, item.Id, item.CreatedAt);
            order.RestoreLines(item.Lines.Select(x => x is null ? throw new InvalidDataException("Порожня позиція замовлення.") : new OrderLine(x.Id, x.MenuItemId, x.Name, x.Category, x.UnitPrice, x.Quantity)), item.Status);
            orders.Add(order);
        }

        return new RestaurantData(document.Menu.Select(x => menu[x.Id]).ToArray(), orders.AsReadOnly());
    }
}
