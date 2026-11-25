using System.Xml.Serialization;
using System.Text.Json.Nodes;
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
        Reject<ArgumentException>("Порожній ID меню", () => new Dish("Суп", "Страви", 1, 1, id: Guid.Empty));
        Reject<ArgumentException>("Порожній час замовлення", () => new Order(1, createdAt: default(DateTimeOffset)));
        Reject<ArgumentNullException>("Відсутня позиція замовлення", () => draft.AddLine(null!, 1));
        Reject<KeyNotFoundException>("Зміна відсутнього рядка", () => draft.ChangeQuantity(Guid.NewGuid(), 1));
        Reject<KeyNotFoundException>("Видалення відсутнього рядка", () => draft.RemoveLine(Guid.NewGuid()));

        var largeOrder = new Order(1);
        var expensive = new Dish("Велика ціна", "Тест", decimal.MaxValue / 2 - 1m, 1);
        largeOrder.AddLine(expensive, 1);
        largeOrder.AddLine(expensive, 1);
        decimal largeTotal = largeOrder.Total;
        OrderLine firstLine = largeOrder.Lines[0];
        Reject<OverflowException>("Переповнення загальної суми при додаванні", () => largeOrder.AddLine(expensive, 1));
        Reject<OverflowException>("Переповнення загальної суми при зміні кількості", () => largeOrder.ChangeQuantity(firstLine.Id, 2));
        Check("Переповнення не змінює рядки й суму", largeOrder.Total == largeTotal && largeOrder.Lines.Count == 2 && ReferenceEquals(firstLine, largeOrder.Lines[0]));
        largeOrder.ChangeQuantity(firstLine.Id, 1);
        Check("Заміна рядка зберігає ID та історичні значення", largeOrder.Lines[0].Id == firstLine.Id && largeOrder.Lines[0].Name == firstLine.Name && largeOrder.Lines[0].UnitPrice == firstLine.UnitPrice);
        largeOrder.RemoveLine(firstLine.Id);
        Check("Сума після видалення великого рядка", largeOrder.Total == expensive.Price);

        var allowedTransitions = new HashSet<(OrderStatus, OrderStatus)>
        {
            (OrderStatus.New, OrderStatus.Preparing),
            (OrderStatus.New, OrderStatus.Cancelled),
            (OrderStatus.Preparing, OrderStatus.Served),
            (OrderStatus.Preparing, OrderStatus.Cancelled),
            (OrderStatus.Served, OrderStatus.Paid)
        };
        foreach (OrderStatus current in Enum.GetValues<OrderStatus>())
        {
            foreach (OrderStatus next in Enum.GetValues<OrderStatus>().Append((OrderStatus)99))
            {
                var sample = new Order(1);
                sample.AddLine(drink, 1);
                var saved = RestaurantMapper.ToDocument(new RestaurantData([drink], [sample]));
                saved.Orders[0].Status = current;
                Order restoredOrder = RestaurantMapper.FromDocument(saved).Orders[0];
                if (allowedTransitions.Contains((current, next)))
                {
                    restoredOrder.ChangeStatus(next);
                    Check($"Дозволений перехід {current} → {next}", restoredOrder.Status == next);
                }
                else
                {
                    Reject<InvalidOperationException>($"Заборонений перехід {current} → {next}", () => restoredOrder.ChangeStatus(next));
                    Check("Статус зберігається після відхилення", restoredOrder.Status == current);
                }
            }
        }

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
        updatedDish.UpdateDetails("Зовнішня зміна", "Тест", 1m, 1m);
        Check("Збережена позиція незалежна від аргументу", service.Snapshot().Menu[0].Price == 220m);
        Guid freshOrder = service.CreateOrder(3);
        service.AddLine(freshOrder, drink.Id, 2);
        OrderLine savedLine = service.Snapshot().Orders.Single(x => x.Id == freshOrder).Lines.Single();
        service.ChangeQuantity(freshOrder, savedLine.Id, 3);
        service.ChangeTable(freshOrder, 5);
        Check("Редагування замовлення через сервіс", service.Snapshot().Orders.Single(x => x.Id == freshOrder) is { TableNumber: 5, Total: 150m });
        revision = service.Revision;
        Reject<InvalidOperationException>("Відхилення статусу через сервіс", () => service.ChangeStatus(freshOrder, OrderStatus.Paid));
        Check("Невдале редагування не змінює ревізію", service.Revision == revision);
        service.RemoveLine(freshOrder, savedLine.Id);
        Check("Видалення рядка через сервіс", service.Snapshot().Orders.Single(x => x.Id == freshOrder).Total == 0m);
        service.DeleteOrder(freshOrder);
        Check("Видалення нового замовлення через сервіс", service.Snapshot().Orders.All(x => x.Id != freshOrder));
        Reject<KeyNotFoundException>("Відсутнє замовлення через сервіс", () => service.ChangeTable(freshOrder, 2));
        Check("Сортування меню за спаданням ціни", service.FindMenu(descending: true).Select(x => x.CalculatePrice()).SequenceEqual(new[] { 220m, 70m, 50m }));
        service.Replace(data, service.Revision);
        draft.AddLine(drink, 1);
        Check("Імпортований стан незалежний від аргументу", service.Snapshot().Orders.Single(x => x.Id == draft.Id).Lines.Count == 0);
        draft.RemoveLine(draft.Lines[0].Id);
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
        string validJson = await File.ReadAllTextAsync(json);
        foreach (string section in new[] { "root", "menu", "orders", "lines" })
        {
            JsonNode source = JsonNode.Parse(validJson)!;
            JsonObject entry = section switch
            {
                "root" => source.AsObject(),
                "menu" => source["menu"]![0]!.AsObject(),
                "orders" => source["orders"]![0]!.AsObject(),
                _ => source["orders"]![0]!["lines"]![0]!.AsObject()
            };
            foreach (string property in entry.Select(pair => pair.Key).ToArray())
            {
                JsonNode? value = entry[property];
                entry.Remove(property);
                string incomplete = Path.Combine(directory, "incomplete.json");
                await File.WriteAllTextAsync(incomplete, source.ToJsonString());
                await RejectAsync<InvalidDataException>($"Обов'язкове поле {section}.{property}", () => repository.LoadAsync(incomplete));
                entry.Add(property, value);
            }
        }
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
        document = RestaurantMapper.ToDocument(data);
        document.Orders[0].Lines.Add(document.Orders[0].Lines[0]);
        Reject<ArgumentException>("Повторений ID рядка при відновленні", () => RestaurantMapper.FromDocument(document));
        document = RestaurantMapper.ToDocument(data);
        document.Orders[0].Lines.Clear();
        Reject<ArgumentException>("Порожнє оплачене замовлення при відновленні", () => RestaurantMapper.FromDocument(document));
        document = RestaurantMapper.ToDocument(data);
        document.Orders[0].Status = (OrderStatus)99;
        Reject<ArgumentException>("Невідомий статус при відновленні", () => RestaurantMapper.FromDocument(document));
        document = RestaurantMapper.ToDocument(data);
        document.Orders[0].Lines[0].UnitPrice = -1m;
        Reject<ArgumentOutOfRangeException>("Від'ємна історична ціна", () => RestaurantMapper.FromDocument(document));
        document = RestaurantMapper.ToDocument(data);
        document.Menu[2].Id = document.Menu[0].Id;
        Reject<InvalidDataException>("Повторений ID між комбо й стравою", () => RestaurantMapper.FromDocument(document));
        document = RestaurantMapper.ToDocument(data);
        document.Menu.Reverse();
        Check("Комбо відновлюється перед складниками у файлі", RestaurantMapper.FromDocument(document).Menu[0] is ComboSet { Items.Count: 2 });
        document = RestaurantMapper.ToDocument(data);
        document.Menu[2].ItemIds[0] = document.Menu[2].Id;
        Reject<InvalidDataException>("Самопосилання комбо відхиляється", () => RestaurantMapper.FromDocument(document));
        document = RestaurantMapper.ToDocument(data);
        document.Menu[0].DiscountPercent = 10;
        Reject<InvalidDataException>("Знижка страви у JSON відхиляється", () => RestaurantMapper.FromDocument(document));
        document = RestaurantMapper.ToDocument(data);
        document.Menu[0].Kind = "Unknown";
        Reject<InvalidDataException>("Невідомий тип позиції у JSON", () => RestaurantMapper.FromDocument(document));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        string original = await File.ReadAllTextAsync(json);
        await RejectAsync<OperationCanceledException>("Скасування експорту", () => repository.SaveAsync(json, data, token: cts.Token));
        Check("Скасований експорт зберігає файл", await File.ReadAllTextAsync(json) == original);
        await RejectAsync<OperationCanceledException>("Скасування імпорту", () => repository.LoadAsync(json, token: cts.Token));
        using var importCancel = new CancellationTokenSource();
        await RejectAsync<OperationCanceledException>("Скасування після читання JSON", () => repository.LoadAsync(json,
            new ImmediateProgress(p => { if (p.Percent == 50) importCancel.Cancel(); }), importCancel.Token));
        using var exportCancel = new CancellationTokenSource();
        await RejectAsync<OperationCanceledException>("Скасування перед записом JSON", () => repository.SaveAsync(json, data,
            new ImmediateProgress(_ => exportCancel.Cancel()), exportCancel.Token));
        Check("Скасований запис не змінює наявний JSON", await File.ReadAllTextAsync(json) == original);
        bool lockedWriteRejected = false;
        using (var lockedFile = new FileStream(json, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            try
            {
                await repository.SaveAsync(json, data);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                lockedWriteRejected = true;
            }
        }
        Check("Заблокований файл не перезаписується", lockedWriteRejected);
        Check("Невдалий запис зберігає JSON і прибирає тимчасовий файл", await File.ReadAllTextAsync(json) == original && Directory.GetFiles(directory, "*.tmp").Length == 0);
        var reports = new SalesReportService();
        var report = await reports.CalculateAsync(restored.Orders, false);
        Check("Звіт враховує лише оплачені", report.PaidOrders == 1 && report.Total == 126m && report.Categories.Single().Category == "Комбо");
        await new XmlSalesReportExporter().ExportAsync(xml, report);
        using (var stream = File.OpenRead(xml))
        {
            var exported = (SalesReport)new XmlSerializer(typeof(SalesReport)).Deserialize(stream)!;
            Check("XML відтворює виторг за категоріями", exported.Total == 126m && exported.Categories.Single().Revenue == 126m);
        }

        string originalXml = await File.ReadAllTextAsync(xml);
        await RejectAsync<OperationCanceledException>("Скасування XML", () => new XmlSalesReportExporter().ExportAsync(xml, report, cts.Token));
        Check("Скасування XML зберігає файл", await File.ReadAllTextAsync(xml) == originalXml);
        var unstableReport = new SalesReport
        {
            PaidOrders = 1,
            Total = 12.5m,
            Categories = [new CategorySales { Category = "Напої", Quantity = 5, Revenue = 12.5m }]
        };
        string snapshotXml = Path.Combine(directory, "snapshot.xml");
        Task savingXml = new XmlSalesReportExporter().ExportAsync(snapshotXml, unstableReport);
        unstableReport.Total = 0;
        unstableReport.Categories.Clear();
        await savingXml;
        using (var stream = File.OpenRead(snapshotXml))
        {
            var snapshot = (SalesReport)new XmlSerializer(typeof(SalesReport)).Deserialize(stream)!;
            Check("XML використовує незалежний знімок", snapshot.Total == 12.5m && snapshot.Categories.Single().Quantity == 5);
        }

        unstableReport.Categories.Add(new CategorySales { Category = "\u0001" });
        await RejectAsync<InvalidOperationException>("Помилка XML-серіалізації", () => new XmlSalesReportExporter().ExportAsync(xml, unstableReport));
        Check("Невдалий XML не пошкоджує звіт", await File.ReadAllTextAsync(xml) == originalXml && Directory.GetFiles(directory, "*.tmp").Length == 0);

        Reject<ArgumentOutOfRangeException>("Від'ємна кількість демонстраційних замовлень", () => new DemoDataGenerator().Generate(-1));
        Reject<ArgumentOutOfRangeException>("Перевищення межі генератора", () => new DemoDataGenerator().Generate(100_001));
        Reject<OperationCanceledException>("Скасування генератора без замовлень", () => new DemoDataGenerator().Generate(0, cts.Token));
        Check("Генератор порожнього списку зберігає меню", new DemoDataGenerator().Generate(0) is { Menu.Count: 51, Orders.Count: 0 });

        var big = new DemoDataGenerator().Generate(10_000);
        Check("Генератор 50+ позицій і 10 000 замовлень", big.Menu.Count >= 50 && big.Orders.Count == 10_000);
        Check("Час демонстраційних замовлень зростає з кроком хвилина", big.Orders.Zip(big.Orders.Skip(1)).All(pair => pair.Second.CreatedAt - pair.First.CreatedAt == TimeSpan.FromMinutes(1)));
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
        var mixedOrders = new List<Order>();
        for (int i = 0; i < 600; i++)
        {
            var sale = new Order(1);
            sale.AddLine(new Drink("Чай", i % 2 == 0 ? "Напої" : "напої", 1.25m, 100m), 2);
            sale.ChangeStatus(OrderStatus.Preparing);
            sale.ChangeStatus(OrderStatus.Served);
            sale.ChangeStatus(OrderStatus.Paid);
            mixedOrders.Add(sale);
        }

        foreach (bool mode in new[] { false, true })
        {
            var percentages = new List<int>();
            var grouped = await reports.CalculateAsync(mixedOrders, mode, new ImmediateProgress(p => percentages.Add(p.Percent)));
            Check("Категорії різного регістру об'єднуються між частинами", grouped.PaidOrders == 600 && grouped.Total == 1500m && grouped.Categories.Single() is { Category: "Напої", Quantity: 1200, Revenue: 1500m });
            Check("Прогрес не зменшується й завершується на 100", percentages[0] == 0 && percentages[^1] == 100 && percentages.SequenceEqual(percentages.Order()));
        }

        Task<SalesReport> capturedReport = reports.CalculateAsync(mixedOrders, true);
        mixedOrders.Clear();
        Check("Звіт використовує знімок вхідної колекції", (await capturedReport).PaidOrders == 600);
        using var finishCancel = new CancellationTokenSource();
        await RejectAsync<OperationCanceledException>("Скасування при завершенні звіту", () => reports.CalculateAsync([], false,
            new ImmediateProgress(p => { if (p.Percent == 100) finishCancel.Cancel(); }), finishCancel.Token));

        var sampleData = await repository.LoadAsync(Path.Combine("data", "restaurant.json"));
        var loadData = await repository.LoadAsync(Path.Combine("data", "restaurant-10000.json"));
        Check("Сумісність наявних файлів даних", sampleData.Menu.Count >= 50 && loadData.Menu.Count >= 50 && loadData.Orders.Count >= 10_000);
        var benchmark = await new BenchmarkService().RunAsync(big.Orders);
        Check("Вимірювання містить п'ять запусків і додатні часи", benchmark.Runs == 5 && benchmark.SequentialMilliseconds > 0 && benchmark.ParallelMilliseconds > 0 && double.IsFinite(benchmark.Speedup));
        await RejectAsync<OperationCanceledException>("Скасування порівняння часу", () => new BenchmarkService().RunAsync(big.Orders, cts.Token));
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
