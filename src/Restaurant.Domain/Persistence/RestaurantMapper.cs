using Restaurant.Domain.Models;

namespace Restaurant.Domain.Persistence;

public static class RestaurantMapper
{
    public static RestaurantDocument ToDocument(RestaurantData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(data.Menu);
        ArgumentNullException.ThrowIfNull(data.Orders);
        return new RestaurantDocument
        {
            Menu = data.Menu.Select(ToItem).ToList(),
            Orders = data.Orders.Select(ToOrder).ToList()
        };
    }

    public static MenuItemData ToItem(MenuItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var entry = new MenuItemData
        {
            Id = item.Id,
            Kind = item switch
            {
                Dish => nameof(Dish),
                Drink => nameof(Drink),
                ComboSet => nameof(ComboSet),
                _ => throw new ArgumentException("Невідомий тип позиції меню.", nameof(item))
            },
            Name = item.Name,
            Category = item.Category,
            Price = item.Price,
            PortionSize = item.PortionSize,
            IsAvailable = item.IsAvailable
        };
        if (item is ComboSet combo)
        {
            entry.DiscountPercent = combo.DiscountPercent;
            entry.ItemIds = combo.Items.Select(part => part.Id).ToList();
        }
        return entry;
    }

    public static OrderData ToOrder(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);
        return new OrderData
        {
            Id = order.Id,
            TableNumber = order.TableNumber,
            CreatedAt = order.CreatedAt,
            Status = order.Status,
            Lines = order.Lines.Select(line => new OrderLineData
            {
                Id = line.Id,
                MenuItemId = line.MenuItemId,
                Name = line.Name,
                Category = line.Category,
                UnitPrice = line.UnitPrice,
                Quantity = line.Quantity
            }).ToList()
        };
    }

    public static RestaurantData FromDocument(RestaurantDocument document, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        token.ThrowIfCancellationRequested();
        if (document.Version != 1 || document.Menu is null || document.Orders is null)
            throw new InvalidDataException("Непідтримувана версія або відсутні списки меню й замовлень.");

        var menu = new Dictionary<Guid, MenuItem>();
        var menuIds = new HashSet<Guid>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (MenuItemData entry in document.Menu)
        {
            token.ThrowIfCancellationRequested();
            if (entry is null || entry.Id == Guid.Empty || !menuIds.Add(entry.Id))
                throw new InvalidDataException("Позиція меню відсутня або має некоректний чи повторений ID.");
            if (string.IsNullOrWhiteSpace(entry.Name) || !names.Add(entry.Name.Trim()))
                throw new InvalidDataException("Назва меню порожня або повторюється.");
            if (entry.ItemIds is null)
                throw new InvalidDataException("Відсутній список складників.");
            if (entry.Kind == nameof(ComboSet))
                continue;
            if (entry.ItemIds.Count != 0 || entry.DiscountPercent != 0m)
                throw new InvalidDataException("Склад і знижка дозволені лише для комбо.");

            menu.Add(entry.Id, entry.Kind switch
            {
                nameof(Dish) => new Dish(entry.Name, entry.Category, entry.Price, entry.PortionSize, entry.IsAvailable, entry.Id),
                nameof(Drink) => new Drink(entry.Name, entry.Category, entry.Price, entry.PortionSize, entry.IsAvailable, entry.Id),
                _ => throw new InvalidDataException("Невідомий тип позиції меню.")
            });
        }

        foreach (MenuItemData entry in document.Menu)
        {
            token.ThrowIfCancellationRequested();
            if (entry.Kind != nameof(ComboSet))
                continue;

            var components = new List<MenuItem>();
            foreach (Guid id in entry.ItemIds)
            {
                token.ThrowIfCancellationRequested();
                if (!menu.TryGetValue(id, out MenuItem? part) || part is ComboSet)
                    throw new InvalidDataException("Складником комбо має бути наявна страва або напій.");
                components.Add(part);
            }
            var combo = new ComboSet(entry.Name, entry.Category, entry.Price, components,
                entry.DiscountPercent, entry.IsAvailable, entry.Id);
            combo.UpdateDetails(entry.Name, entry.Category, entry.Price, entry.PortionSize);
            menu.Add(entry.Id, combo);
        }

        var orders = new List<Order>();
        var orderIds = new HashSet<Guid>();
        foreach (OrderData entry in document.Orders)
        {
            token.ThrowIfCancellationRequested();
            if (entry is null || entry.Lines is null || !orderIds.Add(entry.Id))
                throw new InvalidDataException("Замовлення відсутнє, повторюється або не містить списку рядків.");

            var lines = new List<OrderLine>();
            foreach (OrderLineData line in entry.Lines)
            {
                token.ThrowIfCancellationRequested();
                if (line is null)
                    throw new InvalidDataException("У замовленні є порожній рядок.");
                lines.Add(new OrderLine(line.Id, line.MenuItemId, line.Name, line.Category, line.UnitPrice, line.Quantity));
            }
            var order = new Order(entry.TableNumber, entry.Id, entry.CreatedAt);
            order.RestoreLines(lines, entry.Status);
            orders.Add(order);
        }

        token.ThrowIfCancellationRequested();
        return new RestaurantData(document.Menu.Select(entry => menu[entry.Id]).ToArray(), orders.AsReadOnly());
    }
}
