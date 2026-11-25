using Restaurant.Domain.Models;

Console.OutputEncoding = System.Text.Encoding.UTF8;
int passed = 0;
try
{
    var dish = new Dish(" Борщ ", " Перші страви ", 120m, 300m);
    var drink = new Drink("Узвар", "Напої", 55m, 250m, false);
    MenuItem[] menu = [dish, drink];
    Check("Поліморфний розрахунок ціни", menu.Sum(item => item.CalculatePrice()) == 175m);
    Check("Одиниці виходу порції", dish.PortionUnit == "г" && drink.PortionUnit == "мл");
    Check("Обрізання крайніх пробілів", dish.Name == "Борщ" && dish.Category == "Перші страви");
    Check("Окремі непорожні ідентифікатори", dish.Id != Guid.Empty && dish.Id != drink.Id);
    Check("Початкова доступність", dish.IsAvailable && !drink.IsAvailable);
    Reject<ArgumentException>("Порожня назва", () => new Dish("  ", "Супи", 1m, 1m));
    Reject<ArgumentException>("Відсутня назва", () => new Drink(null!, "Напої", 1m, 1m));
    Reject<ArgumentException>("Порожня категорія", () => new Drink("Чай", "", 1m, 1m));
    Reject<ArgumentException>("Відсутня категорія", () => new Dish("Суп", null!, 1m, 1m));
    Reject<ArgumentOutOfRangeException>("Нульова ціна", () => new Dish("Суп", "Супи", 0m, 1m));
    Reject<ArgumentOutOfRangeException>("Від'ємна ціна", () => new Drink("Чай", "Напої", -1m, 1m));
    Reject<ArgumentOutOfRangeException>("Нульовий вихід", () => new Dish("Суп", "Супи", 1m, 0m));
    Reject<ArgumentOutOfRangeException>("Від'ємний вихід", () => new Drink("Чай", "Напої", 1m, -1m));
    Check("Додатні дробові значення", new Dish("Соус", "Соуси", 0.01m, 0.5m).CalculatePrice() == 0.01m);
    Reject<ArgumentOutOfRangeException>("Відхилення некоректного редагування", () => dish.UpdateDetails("Інша назва", "Інша категорія", 99m, -1m));
    Check("Збереження всіх даних після відхилення", dish.Name == "Борщ" && dish.Category == "Перші страви" && dish.Price == 120m && dish.PortionSize == 300m);
    Guid originalId = dish.Id;
    dish.UpdateDetails("Бульйон", "Супи", 90m, 200m);
    Check("Коректне редагування", dish.Name == "Бульйон" && dish.Category == "Супи" && dish.CalculatePrice() == 90m && dish.PortionSize == 200m && dish.Id == originalId);
    drink.SetAvailability(true);
    Check("Зміна доступності", drink.IsAvailable);
    Check("Рядок для налагодження", drink.ToString().Contains("Узвар") && drink.ToString().Contains("мл"));
    var ingredients = new List<MenuItem>
    {
        dish,
        drink
    };
    var combo = new ComboSet("Обід", "Комбо", 150m, ingredients, 10m);
    MenuItem comboAsMenuItem = combo;
    IDiscountable discountable = combo;
    Check("Поліморфна ціна комбо зі знижкою", comboAsMenuItem.CalculatePrice() == 135m);
    Check("Базова ціна не змінюється знижкою", combo.Price == 150m);
    Check("Вихід комбо", combo.PortionSize == 1m && combo.PortionUnit == "набір");
    ingredients.Clear();
    Check("Копія складу незалежна від вхідного списку", combo.Items.Count == 2);
    Reject<NotSupportedException>("Склад не можна змінити через IList", () => ((IList<MenuItem>)combo.Items).Clear());
    discountable.SetDiscount(0m);
    Check("Нульова знижка через інтерфейс", discountable.DiscountPercent == 0m && combo.CalculatePrice() == 150m);
    discountable.SetDiscount(100m);
    Check("Повна знижка", combo.CalculatePrice() == 0m);
    Reject<ArgumentOutOfRangeException>("Від'ємна знижка", () => discountable.SetDiscount(-0.01m));
    Reject<ArgumentOutOfRangeException>("Знижка понад 100", () => discountable.SetDiscount(100.01m));
    Check("Знижка збережена після помилки", discountable.DiscountPercent == 100m);
    Reject<ArgumentOutOfRangeException>("Некоректна знижка конструктора", () => new ComboSet("Обід", "Комбо", 150m, [dish, drink], -1m));
    Reject<ArgumentNullException>("Відсутній склад", () => new ComboSet("Обід", "Комбо", 150m, null!));
    Reject<ArgumentException>("Порожній склад", () => new ComboSet("Обід", "Комбо", 150m, []));
    Reject<ArgumentException>("Один складник", () => new ComboSet("Обід", "Комбо", 150m, [dish]));
    Reject<ArgumentException>("Порожній складник", () => new ComboSet("Обід", "Комбо", 150m, [dish, null!]));
    Reject<ArgumentException>("Вкладене комбо", () => new ComboSet("Обід", "Комбо", 150m, [dish, combo]));
    Reject<ArgumentOutOfRangeException>("Нульова базова ціна комбо", () => new ComboSet("Обід", "Комбо", 0m, [dish, drink]));
    Check("Округлення половини копійки вгору", new ComboSet("Обід", "Комбо", 10.05m, [dish, drink], 50m).CalculatePrice() == 5.03m);
    Check("Велика ціна без переповнення проміжного добутку", new ComboSet("Обід", "Комбо", decimal.MaxValue, [dish, drink], 0m).CalculatePrice() == decimal.MaxValue);
    combo.SetDiscount(10m);
    combo.UpdateDetails("Великий обід", "Набори", 200m, 1m);
    Check("Редагування базової ціни зі збереженням знижки", combo.CalculatePrice() == 180m);
    dish.UpdateDetails("Бульйон", "Супи", 100m, 200m);
    Check("Ціна складника не змінює ціну комбо", combo.CalculatePrice() == 180m);
    Check("Повторений складник означає дві порції", new ComboSet("Два супи", "Комбо", 150m, [dish, dish]).Items.Count == 2);
    foreach (MenuItem item in menu.Append(combo))
        Console.WriteLine(item);
    passed += await IntegrationChecks.RunAsync();
    Console.WriteLine($"Успішно: {passed} перевірок.");
}
catch (Exception error)
{
    Console.Error.WriteLine(error);
    Environment.ExitCode = 1;
}

void Check(string name, bool condition)
{
    if (!condition)
        throw new InvalidOperationException($"Не пройдено: {name}");
    passed++;
    Console.WriteLine($"OK: {name}");
}

void Reject<TException>(string name, Action action)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        Check(name, true);
        return;
    }

    throw new InvalidOperationException($"Не пройдено: {name}; очікувався {typeof(TException).Name}");
}
