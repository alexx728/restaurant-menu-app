using Restaurant.Domain.Models;

namespace Restaurant.Domain.Services;
public sealed class DemoDataGenerator
{
    public RestaurantData Generate(int orderCount = 100, CancellationToken token = default)
    {
        if (orderCount < 0 || orderCount > 100_000)
            throw new ArgumentOutOfRangeException(nameof(orderCount));
        var menu = new List<MenuItem>();
        for (int i = 1; i <= 50; i++)
            menu.Add(i <= 30 ? new Dish($"Страва {i:00}", i <= 15 ? "Основні страви" : "Десерти", 40m + i * 3, 100 + i) : new Drink($"Напій {i:00}", "Напої", 20m + i, 250));
        menu.Add(new ComboSet("Обідній набір", "Комбо", 150m, [menu[0], menu[30]], 10));
        var orders = new List<Order>();
        for (int i = 0; i < orderCount; i++)
        {
            token.ThrowIfCancellationRequested();
            var order = new Order(i % 20 + 1, createdAt: DateTimeOffset.UtcNow.AddMinutes(-orderCount + i));
            order.AddLine(menu[i % menu.Count], i % 3 + 1);
            order.AddLine(menu[(i + 7) % menu.Count], 1);
            if (i % 10 != 0)
            {
                order.ChangeStatus(OrderStatus.Preparing);
                order.ChangeStatus(OrderStatus.Served);
                order.ChangeStatus(OrderStatus.Paid);
            }

            orders.Add(order);
        }

        return new(menu.AsReadOnly(), orders.AsReadOnly());
    }
}
