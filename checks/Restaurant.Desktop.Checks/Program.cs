using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Restaurant.Desktop;
using Restaurant.Desktop.ViewModels;
using Restaurant.Desktop.Views;
using Restaurant.Domain.Models;
using Serilog;

internal static class Program
{
    [STAThread]
    public static int Main()
    {
        string directory = Path.Combine("tmp", "desktop-checks");
        Directory.CreateDirectory(directory);
        Log.Logger = new LoggerConfiguration().WriteTo.File(Path.Combine(directory, "checks.log")).CreateLogger();
        try
        {
            AppBuilder.Configure<App>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
            bool confirm = true;
            var vm = new MainViewModel(_ => Task.FromResult(confirm));
            var window = new MainWindow
            {
                DataContext = vm
            };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            int count = 0;
            void Check(string label, bool condition)
            {
                if (!condition)
                    throw new Exception(label + " - " + vm.Message);
                count++;
                Console.WriteLine("OK: " + label);
            }

            void Wait(Task task)
            {
                var end = DateTime.UtcNow.AddSeconds(30);
                while (!task.IsCompleted && DateTime.UtcNow < end)
                {
                    Dispatcher.UIThread.RunJobs();
                    Thread.Sleep(5);
                }

                Dispatcher.UIThread.RunJobs();
                if (!task.IsCompleted)
                    throw new TimeoutException("Команда не завершилася.");
                task.GetAwaiter().GetResult();
            }

            void Run(AsyncCommand command) => Wait(command.ExecuteAsync());

            void Capture(string name)
            {
                Dispatcher.UIThread.RunJobs();
                using var frame = window.CaptureRenderedFrame() ?? throw new Exception("Немає кадру Avalonia.");
                frame.Save(Path.Combine(directory, name + ".png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
            }

            Check("Початкові дані в таблиці", vm.MenuRows.Count == 51 && vm.OrderRows.Count == 10);
            Check("Команди без вибраного замовлення недоступні", !vm.AddLineCommand.CanExecute(null) && !vm.DeleteOrderCommand.CanExecute(null));
            Check("Поля комбо приховані для страви", !window.FindControl<StackPanel>("ComboFields")!.IsVisible);
            Check("XML недоступний до формування звіту", !vm.XmlCommand.CanExecute(null));
            Capture("menu");
            Run(vm.NewMenuCommand);
            window.FindControl<TextBox>("NameInput")!.Text = "Перевірка форми";
            window.FindControl<TextBox>("PriceInput")!.Text = "75,50";
            vm.MenuEditor.Category = "Тест";
            Dispatcher.UIThread.RunJobs();
            Check("Прив'язка поля назви до редактора", vm.MenuEditor.Name == "Перевірка форми");
            vm.MenuEditor.Name = "Зміна з моделі";
            Dispatcher.UIThread.RunJobs();
            Check("Зміни редактора відображаються у полі", window.FindControl<TextBox>("NameInput")!.Text == "Зміна з моделі");
            vm.MenuEditor.Name = "Перевірка форми";
            Run(vm.SaveMenuCommand);
            Check("Додавання через команду GUI", vm.MenuRows.Any(x => x.Name == "Перевірка форми" && x.Price == 75.50m));
            vm.MenuEditor.Price = "80";
            Run(vm.SaveMenuCommand);
            Check("Повторне збереження редагує додану позицію", vm.MenuRows.Count(x => x.Name == "Перевірка форми") == 1 && vm.SelectedMenu?.Price == 80m);
            vm.SelectedMenu = vm.MenuRows.First(x => x.Name == "Перевірка форми");
            vm.MenuEditor.Price = "-1";
            Run(vm.SaveMenuCommand);
            Check("Помилка форми без пошкодження даних", vm.Message.StartsWith("Помилка:") && vm.MenuRows.Any(x => x.Name == "Перевірка форми" && x.Price == 80m));
            confirm = false;
            Run(vm.DeleteMenuCommand);
            Check("Відмова від видалення", vm.MenuRows.Any(x => x.Name == "Перевірка форми"));
            confirm = true;
            Run(vm.DeleteMenuCommand);
            Check("Підтверджене видалення", !vm.MenuRows.Any(x => x.Name == "Перевірка форми"));
            Run(vm.NewMenuCommand);
            vm.MenuEditor.Kind = "Комбо";
            vm.MenuEditor.Name = "Тестове комбо";
            vm.MenuEditor.Category = "Комбо";
            vm.MenuEditor.Price = "100";
            vm.MenuEditor.Discount = "10";
            Dispatcher.UIThread.RunJobs();
            Check("Поля комбо з'являються після зміни типу", window.FindControl<StackPanel>("ComboFields")!.IsVisible);
            var partsList = window.GetVisualDescendants().OfType<ListBox>().Single();
            partsList.SelectedItems!.Add(vm.Components[0]);
            partsList.SelectedItems.Add(vm.Components[1]);
            Dispatcher.UIThread.RunJobs();
            Run(vm.SaveMenuCommand);
            Check("Вибір складників комбо у формі", vm.MenuRows.Any(x => x.Name == "Тестове комбо" && x.FinalPrice == 90m));
            Capture("combo");
            window.Width = window.MinWidth;
            window.Height = window.MinHeight;
            Capture("combo-small");
            var editorScroll = window.FindControl<ScrollViewer>("MenuEditorScroll")!;
            editorScroll.Offset = new Vector(0, editorScroll.Extent.Height);
            Capture("combo-small-bottom");
            window.Width = 1320;
            window.Height = 900;
            var tabs = window.GetVisualDescendants().OfType<TabControl>().Single();
            tabs.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();
            vm.TableNumber = "4";
            Run(vm.NewOrderCommand);
            Check("Створення замовлення через GUI", vm.SelectedOrder?.Table == 4);
            vm.SelectedOrderItem = vm.AvailableItems[0];
            vm.Quantity = "2";
            Run(vm.AddLineCommand);
            Check("Додавання позиції до замовлення", vm.Lines.Count == 1 && vm.Lines[0].Quantity == 2);
            vm.Quantity = "1";
            vm.SelectedLine = vm.Lines[0];
            Check("Вибір рядка заповнює кількість", vm.Quantity == "2");
            vm.Quantity = "3";
            Run(vm.ChangeQuantityCommand);
            Check("Зміна кількості зберігає вибір рядка", vm.SelectedLine?.Quantity == 3 && vm.Lines[0].Quantity == 3);
            vm.Quantity = "2";
            Run(vm.ChangeQuantityCommand);
            foreach (var status in new[]
            {
                OrderStatus.Preparing,
                OrderStatus.Served,
                OrderStatus.Paid
            }

            )
            {
                vm.NextStatus = vm.Statuses.First(x => x.Value == status);
                Run(vm.ChangeStatusCommand);
            }

            Check("Зміна статусів через GUI", vm.SelectedOrder?.Item.Status == OrderStatus.Paid);
            Check("Оплачене замовлення захищене від редагування", !vm.CanEditOrder && !vm.AddLineCommand.CanExecute(null) && !vm.ChangeStatusCommand.CanExecute(null));
            Capture("orders");
            window.Width = window.MinWidth;
            window.Height = window.MinHeight;
            Capture("orders-small");
            window.Width = 1320;
            window.Height = 900;
            tabs.SelectedIndex = 2;
            Dispatcher.UIThread.RunJobs();
            Run(vm.ReportCommand);
            Check("Фоновий звіт у GUI", vm.ReportRows.Count > 0 && vm.Progress == 100);
            Capture("report");
            window.Width = window.MinWidth;
            window.Height = window.MinHeight;
            Capture("report-small");
            window.Width = 1320;
            window.Height = 900;
            vm.JsonPath = Path.Combine(directory, "roundtrip.json");
            Run(vm.ExportCommand);
            Check("Експорт із GUI", File.Exists(vm.JsonPath));
            Check("Збереження прибирає позначку незбережених змін", !vm.ConfirmUnsaved && vm.SaveState == "Дані збережено");
            int menuCount = vm.MenuRows.Count;
            Run(vm.ImportCommand);
            Check("Імпорт із GUI", vm.MenuRows.Count == menuCount && !vm.Message.StartsWith("Помилка:"));
            Check("Імпорт скидає попередній звіт", vm.ReportRows.Count == 0 && !vm.XmlCommand.CanExecute(null));
            Run(vm.ReportCommand);
            vm.XmlPath = Path.Combine(directory, "report.xml");
            Run(vm.XmlCommand);
            Check("XML із GUI", File.Exists(vm.XmlPath));
            string bad = Path.Combine(directory, "bad.json");
            File.WriteAllText(bad, "{broken");
            vm.JsonPath = bad;
            Run(vm.ImportCommand);
            Check("Пошкоджений файл не очищує GUI", vm.MenuRows.Count == menuCount && vm.Message.StartsWith("Помилка:"));
            vm.GenerateCommand.Execute(null);
            vm.CancelCommand.Execute(null);
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (vm.IsBusy && DateTime.UtcNow < deadline)
            {
                Dispatcher.UIThread.RunJobs();
                Thread.Sleep(5);
            }

            Check("Скасування фонової генерації", !vm.IsBusy && vm.MenuRows.Count == menuCount);

            var completion = new TaskCompletionSource();
            int executions = 0;
            var command = new AsyncCommand(async () => { executions++; await completion.Task; }, () => true, error => throw error);
            Task running = command.ExecuteAsync();
            Run(command);
            Check("Команда не запускається повторно під час виконання", executions == 1 && !command.CanExecute(null));
            completion.SetResult();
            Wait(running);
            Check("Команда знову доступна після завершення", command.CanExecute(null));
            Exception? commandError = null;
            var failed = new AsyncCommand(() => throw new IOException("Помилка перевірки"), () => true, error => commandError = error);
            Run(failed);
            Check("Помилка команди обробляється без блокування", commandError is IOException && failed.CanExecute(null));

            var decision = new TaskCompletionSource<bool>();
            var pendingModel = new MainViewModel(_ => decision.Task);
            pendingModel.SelectedMenu = pendingModel.MenuRows.First(row => row.Item is Drink);
            Guid keptId = pendingModel.SelectedMenu.Item.Id;
            Task deletion = pendingModel.DeleteMenuCommand.ExecuteAsync();
            Check("Інші команди заблоковані під час підтвердження", pendingModel.IsBusy && !pendingModel.FilterCommand.CanExecute(null));
            Run(pendingModel.CancelCommand);
            decision.SetResult(true);
            Wait(deletion);
            Check("Скасування під час підтвердження зберігає дані", pendingModel.MenuRows.Any(row => row.Item.Id == keptId) && !pendingModel.IsBusy);

            string emptyFile = Path.Combine(directory, "empty.json");
            File.WriteAllText(emptyFile, "{\"version\":1,\"menu\":[],\"orders\":[]}");
            vm.JsonPath = emptyFile;
            Run(vm.ImportCommand);
            Run(vm.ReportCommand);
            Check("Порожній звіт можна експортувати", vm.ReportRows.Count == 0 && vm.XmlCommand.CanExecute(null));
            vm.XmlPath = Path.Combine(directory, "empty.xml");
            Run(vm.XmlCommand);
            Check("Експорт порожнього звіту", File.Exists(vm.XmlPath) && !vm.Message.StartsWith("Помилка:"));
            Log.CloseAndFlush();
            Check("Serilog записує помилки у файл", File.ReadAllText(Path.Combine(directory, "checks.log")).Contains("Помилка операції"));
            window.Close();
            Console.WriteLine($"GUI: успішно {count} перевірок.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
