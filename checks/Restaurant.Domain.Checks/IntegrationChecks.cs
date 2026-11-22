using System.Xml.Serialization;
using Restaurant.Domain.Models;
using Restaurant.Domain.Persistence;
using Restaurant.Domain.Services;

internal static class IntegrationChecks
{
    public static async Task<int> RunAsync()
    {
        int passed = 0;
        void Check(string name, bool ok)
        {
            if (!ok)
                throw new Exception("Не пройдено: " + name);
            Console.WriteLine("OK: " + name);
            passed++;
        }

        void Reject<T>(string name, Action action)
            where T : Exception
        {
            try
            {
                action();
            }
            catch (T)
            {
                Check(name, true);
                return;
            }

            throw new Exception("Очікувався виняток: " + name);
        }

        async Task RejectAsync<T>(string name, Func<Task> action)
            where T : Exception
        {
            try
            {
                await action();
            }
            catch (T)
            {
                Check(name, true);
                return;
            }

            throw new Exception("Очікувався виняток: " + name);
        }

        var dish = new Dish("Суп", "Страви", 100m, 300);
        var drink = new Drink("Чай", "Напої", 50m, 200);
        var combo = new ComboSet("Обід", "Комбо", 140m, [dish, drink], 10m);
        var order = new Order(1);
        Reject<ArgumentOutOfRangeException>("Нульовий стіл", () => new Order(0));
        Reject<ArgumentException>("Порожній ID замовлення", () => new Order(1, Guid.Empty));
        Reject<ArgumentOutOfRangeException>("Кількість 0", () => order.AddLine(dish, 0));
        Reject<ArgumentOutOfRangeException>("Кількість 51", () => order.AddLine(dish, 51));
        Reject<InvalidOperationException>("Початок порожнього замовлення", () => order.ChangeStatus(OrderStatus.Preparing));
        order.AddLine(combo, 2);
        Check("Сума зі знижкою комбо", order.Total == 252m);
        combo.SetDiscount(50);
        dish.UpdateDetails("Новий суп", "Інша категорія", 200m, 300);
        Check("Знімок ціни, назви й категорії", order.Total == 252m && order.Lines[0].Name == "Обід" && order.Lines[0].Category == "Комбо");
        order.ChangeQuantity(order.Lines[0].Id, 50);
        Check("Гранична кількість 50", order.Total == 6300m);
        Reject<ArgumentOutOfRangeException>("Некоректне редагування кількості", () => order.ChangeQuantity(order.Lines[0].Id, 51));
        Check("Збереження кількості після помилки", order.Lines[0].Quantity == 50);
        order.ChangeQuantity(order.Lines[0].Id, 1);
        order.ChangeTable(2);
        Check("Зміна столу", order.TableNumber == 2);
        Reject<NotSupportedException>("Захист композиції від зовнішнього видалення", () => ((IList<OrderLine>)order.Lines).Clear());
        Reject<InvalidOperationException>("Пропуск статусу", () => order.ChangeStatus(OrderStatus.Paid));
        order.ChangeStatus(OrderStatus.Preparing);
        Reject<InvalidOperationException>("Редагування після початку приготування", () => order.AddLine(drink, 1));
        Reject<InvalidOperationException>("Зміна кількості після початку приготування", () => order.ChangeQuantity(order.Lines[0].Id, 1));
        Reject<InvalidOperationException>("Зміна столу після початку приготування", () => order.ChangeTable(3));
        order.ChangeStatus(OrderStatus.Served);
        Reject<InvalidOperationException>("Скасування поданого замовлення", () => order.ChangeStatus(OrderStatus.Cancelled));
        order.ChangeStatus(OrderStatus.Paid);
        Reject<InvalidOperationException>("Повторне відкриття оплаченого", () => order.ChangeStatus(OrderStatus.New));
        var cancelled = new Order(1);
        cancelled.ChangeStatus(OrderStatus.Cancelled);
        Reject<InvalidOperationException>("Редагування скасованого", () => cancelled.AddLine(drink, 1));
        var draft = new Order(1);
        drink.SetAvailability(false);
        Reject<InvalidOperationException>("Недоступний напій", () => draft.AddLine(drink, 1));
        Reject<InvalidOperationException>("Недоступний складник комбо", () => draft.AddLine(combo, 1));
        drink.SetAvailability(true);
        draft.AddLine(drink, 1);
        draft.RemoveLine(draft.Lines[0].Id);
        Check("Вилучення позиції", draft.Lines.Count == 0 && draft.Total == 0);
        Reject<OverflowException>("Переповнення суми не додає позицію", () => draft.AddLine(new Dish("Велика", "Тест", decimal.MaxValue, 1), 2));
        Check("Цілісність після переповнення", draft.Lines.Count == 0);
        var data = new RestaurantData([dish, drink, combo], [order, draft, cancelled]);
        var service = new RestaurantService();
        service.Replace(data, 0);
        long revision = service.Revision;
        Reject<InvalidDataException>("Назва меню унікальна без регістру й крайніх пробілів", () => service.SaveMenu(new Drink(" новий СУП ", "Напої", 1, 1)));
        Check("Помилка не змінює ревізію", service.Revision == revision);
        Reject<InvalidOperationException>("Видалення складника комбо", () => service.DeleteMenu(dish.Id));
        Reject<InvalidOperationException>("Захист оплаченої історії", () => service.DeleteOrder(order.Id));
        Reject<InvalidOperationException>("Захист від застарілого імпорту", () => service.Replace(data, 0));
        service.Snapshot().Menu[0].SetAvailability(false);
        Check("Зовнішній знімок не змінює сховище", service.Snapshot().Menu[0].IsAvailable);
        Check("Пошук і категорія", service.FindMenu("чай", "напої").Single().Id == drink.Id);
        Check("Фільтр статусу", service.FindOrders(OrderStatus.Paid).Single().Id == order.Id);
        Check("Сортування за сумою", service.FindOrders(byTotal: true, descending: true)[0].Id == order.Id);
        service.DeleteOrder(cancelled.Id);
        Check("Видалення скасованого замовлення", service.Snapshot().Orders.Count == 2);
        var updatedDish = new Dish("Новий суп", "Інша категорія", 220m, 300, id: dish.Id);
        service.SaveMenu(updatedDish);
        Check("Оновлені посилання складників комбо", ((ComboSet)service.Snapshot().Menu[2]).Items[0].Price == 220m);
        var concurrent = new RestaurantService();
        Parallel.For(0, 20, i => concurrent.SaveMenu(new Dish("Позиція " + i, "Тест", 1, 1)));
        Check("Одночасні зміни не губляться", concurrent.Snapshot().Menu.Count == 20);
        string directory = Path.Combine("tmp", "integration-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string json = Path.Combine(directory, "restaurant.json"), xml = Path.Combine(directory, "report.xml");
        var repository = new JsonRestaurantRepository();
        await repository.SaveAsync(json, data);
        var restored = await repository.LoadAsync(json);
        Check("JSON зберігає ID й час", restored.Menu[0].Id == dish.Id && restored.Orders[0].Id == order.Id && restored.Orders[0].CreatedAt == order.CreatedAt);
        Check("JSON зберігає знімок проданої ціни", restored.Orders[0].Total == 126m && restored.Orders[0].Status == OrderStatus.Paid);
        Check("JSON відновлює спільні посилання", ReferenceEquals(((ComboSet)restored.Menu[2]).Items[0], restored.Menu[0]));
        foreach (string invalid in new[]
        {
            "",
            "{broken",
            "null",
            "{}",
            "{\"version\":2,\"menu\":[],\"orders\":[]}",
            "{\"version\":1,\"menu\":[null],\"orders\":[]}"
        }

        )
        {
            string broken = Path.Combine(directory, "broken.json");
            await File.WriteAllTextAsync(broken, invalid);
            await RejectAsync<InvalidDataException>("Відхилення пошкодженого JSON: " + invalid, () => repository.LoadAsync(broken));
        }

        var document = RestaurantMapper.ToDocument(data);
        document.Menu[0].Price = -1;
        Reject<ArgumentOutOfRangeException>("Валідація ціни після імпорту", () => RestaurantMapper.FromDocument(document));
        document = RestaurantMapper.ToDocument(data);
        document.Menu[2].ItemIds[0] = Guid.NewGuid();
        Reject<InvalidDataException>("Відсутнє посилання комбо", () => RestaurantMapper.FromDocument(document));
        document = RestaurantMapper.ToDocument(data);
        document.Orders[0].Lines[0].Quantity = 51;
        Reject<ArgumentOutOfRangeException>("Кількість при відновленні", () => RestaurantMapper.FromDocument(document));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        string original = await File.ReadAllTextAsync(json);
        await RejectAsync<OperationCanceledException>("Скасування експорту", () => repository.SaveAsync(json, data, token: cts.Token));
        Check("Скасований експорт зберігає файл", await File.ReadAllTextAsync(json) == original);
        await RejectAsync<OperationCanceledException>("Скасування імпорту", () => repository.LoadAsync(json, token: cts.Token));
        var reports = new SalesReportService();
        var report = await reports.CalculateAsync(restored.Orders, false);
        Check("Звіт враховує лише оплачені", report.PaidOrders == 1 && report.Total == 126m && report.Categories.Single().Category == "Комбо");
        await new XmlSalesReportExporter().ExportAsync(xml, report);
        using (var stream = File.OpenRead(xml))
        {
            var exported = (SalesReport)new XmlSerializer(typeof(SalesReport)).Deserialize(stream)!;
            Check("XML відтворює виторг за категоріями", exported.Total == 126m && exported.Categories.Single().Revenue == 126m);
        }

        var big = new DemoDataGenerator().Generate(10_000);
        Check("Генератор 50+ позицій і 10 000 замовлень", big.Menu.Count >= 50 && big.Orders.Count == 10_000);
        var sequential = await reports.CalculateAsync(big.Orders, false);
        var parallel = await reports.CalculateAsync(big.Orders, true);
        Check("Паралельний і послідовний звіти збігаються", sequential.Total == parallel.Total && sequential.PaidOrders == parallel.PaidOrders && sequential.Categories.Select(x => (x.Category, x.Quantity, x.Revenue)).SequenceEqual(parallel.Categories.Select(x => (x.Category, x.Quantity, x.Revenue))));
        await RejectAsync<OperationCanceledException>("Скасування паралельного звіту", () => reports.CalculateAsync(big.Orders, true, token: cts.Token));
        using var midCancel = new CancellationTokenSource();
        var progress = new ImmediateProgress(p =>
        {
            if (p.Percent > 0)
                midCancel.Cancel();
        });
        await RejectAsync<OperationCanceledException>("Скасування під час розрахунку", () => reports.CalculateAsync(big.Orders, true, progress, midCancel.Token));
        var empty = await reports.CalculateAsync([], true);
        Check("Порожній звіт", empty.Total == 0 && empty.PaidOrders == 0 && empty.Categories.Count == 0);
        var benchmark = await new BenchmarkService().RunAsync(big.Orders);
        Console.WriteLine($"Вимірювання 10 000 замовлень, {benchmark.Runs} запусків: T1={benchmark.SequentialMilliseconds:F3} мс; Tn={benchmark.ParallelMilliseconds:F3} мс; S={benchmark.Speedup:F3}");
        if (Environment.GetEnvironmentVariable("RESTAURANT_WRITE_SAMPLES") == "1")
        {
            await repository.SaveAsync(Path.Combine("data", "restaurant.json"), new DemoDataGenerator().Generate(20));
            await repository.SaveAsync(Path.Combine("data", "restaurant-10000.json"), big);
            await new XmlSalesReportExporter().ExportAsync(Path.Combine("data", "sales-report.xml"), parallel);
        }

        return passed;
    }

    private sealed class ImmediateProgress(Action<OperationProgress> action) : IProgress<OperationProgress>
    {
        public void Report(OperationProgress value) => action(value);
    }
}
