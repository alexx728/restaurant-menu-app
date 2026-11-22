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
                    throw new Exception(label + " — " + vm.Message);
                count++;
                Console.WriteLine("OK: " + label);
            }

            void Run(AsyncCommand command)
            {
                command.Execute(null);
                var end = DateTime.UtcNow.AddSeconds(30);
                while (vm.IsBusy && DateTime.UtcNow < end)
                {
                    Dispatcher.UIThread.RunJobs();
                    Thread.Sleep(5);
                }

                Dispatcher.UIThread.RunJobs();
                if (vm.IsBusy)
                    throw new TimeoutException("Команда не завершилася.");
            }

            void Capture(string name)
            {
                Dispatcher.UIThread.RunJobs();
                using var frame = window.CaptureRenderedFrame() ?? throw new Exception("Немає кадру Avalonia.");
                frame.Save(Path.Combine(directory, name + ".png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
            }

            Check("Початкові дані в таблиці", vm.MenuRows.Count == 51 && vm.OrderRows.Count == 10);
            Capture("menu");
            Run(vm.NewMenuCommand);
            window.FindControl<TextBox>("NameInput")!.Text = "Перевірка форми";
            window.FindControl<TextBox>("PriceInput")!.Text = "75,50";
            vm.MenuEditor.Category = "Тест";
            Dispatcher.UIThread.RunJobs();
            Check("Прив'язка поля назви до редактора", vm.MenuEditor.Name == "Перевірка форми");
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
            var partsList = window.GetVisualDescendants().OfType<ListBox>().Single();
            partsList.SelectedItems!.Add(vm.Components[0]);
            partsList.SelectedItems.Add(vm.Components[1]);
            Dispatcher.UIThread.RunJobs();
            Run(vm.SaveMenuCommand);
            Check("Вибір складників комбо у формі", vm.MenuRows.Any(x => x.Name == "Тестове комбо" && x.FinalPrice == 90m));
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
            Capture("orders");
            tabs.SelectedIndex = 2;
            Dispatcher.UIThread.RunJobs();
            Run(vm.ReportCommand);
            Check("Фоновий звіт у GUI", vm.ReportRows.Count > 0 && vm.Progress == 100);
            Capture("report");
            vm.JsonPath = Path.Combine(directory, "roundtrip.json");
            Run(vm.ExportCommand);
            Check("Експорт із GUI", File.Exists(vm.JsonPath));
            int menuCount = vm.MenuRows.Count;
            Run(vm.ImportCommand);
            Check("Імпорт із GUI", vm.MenuRows.Count == menuCount && !vm.Message.StartsWith("Помилка:"));
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
