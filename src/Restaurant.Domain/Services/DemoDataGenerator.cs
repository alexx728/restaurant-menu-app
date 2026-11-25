using Restaurant.Domain.Models;

namespace Restaurant.Domain.Services;

public sealed class DemoDataGenerator
{
    public RestaurantData Generate(int orderCount = 100, CancellationToken token = default)
    {
        if (orderCount is < 0 or > 100_000)
            throw new ArgumentOutOfRangeException(nameof(orderCount), "Кількість замовлень має бути від 0 до 100 000.");
        token.ThrowIfCancellationRequested();

        var menu = new List<MenuItem>();
        for (int number = 1; number <= 30; number++)
            menu.Add(new Dish($"Страва {number:00}", number <= 15 ? "Основні страви" : "Десерти",
                40m + number * 3m, 100m + number));
        for (int number = 31; number <= 50; number++)
            menu.Add(new Drink($"Напій {number:00}", "Напої", 20m + number, 250m));
        menu.Add(new ComboSet("Обідній набір", "Комбо", 150m, [menu[0], menu[30]], 10m));

        var orders = new List<Order>(orderCount);
        DateTimeOffset start = DateTimeOffset.UtcNow.AddMinutes(-orderCount);
        for (int index = 0; index < orderCount; index++)
        {
            token.ThrowIfCancellationRequested();
            var order = new Order(index % 20 + 1, createdAt: start.AddMinutes(index));
            order.AddLine(menu[index % menu.Count], index % 3 + 1);
            order.AddLine(menu[(index + 7) % menu.Count], 1);
            if (index % 10 != 0)
            {
                order.ChangeStatus(OrderStatus.Preparing);
                order.ChangeStatus(OrderStatus.Served);
                order.ChangeStatus(OrderStatus.Paid);
            }
            orders.Add(order);
        }
        return new RestaurantData(menu.AsReadOnly(), orders.AsReadOnly());
    }
}
