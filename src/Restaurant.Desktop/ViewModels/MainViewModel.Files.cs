using Restaurant.Domain.Models;
using Restaurant.Domain.Persistence;
using Restaurant.Domain.Services;

namespace Restaurant.Desktop.ViewModels;
public sealed partial class MainViewModel
{
    private SalesReport? _report;
    private string _reportSummary = "Звіт ще не сформовано.";
    public string ReportSummary { get => _reportSummary; private set => Set(ref _reportSummary, value); }
    public AsyncCommand ImportCommand { get; private set; } = null!;
    public AsyncCommand ExportCommand { get; private set; } = null!;
    public AsyncCommand GenerateCommand { get; private set; } = null!;
    public AsyncCommand ReportCommand { get; private set; } = null!;
    public AsyncCommand XmlCommand { get; private set; } = null!;
    public AsyncCommand BenchmarkCommand { get; private set; } = null!;

    private void InitializeFileCommands()
    {
        ImportCommand = Command(async () =>
        {
            if (!await _confirm("Імпорт замінить поточні дані. Незбережені зміни буде втрачено. Продовжити?"))
                return;
            long revision = _service.Revision;
            var data = await _repository.LoadAsync(JsonPath, Reporter(), Token);
            Token.ThrowIfCancellationRequested();
            _service.Replace(data, revision);
            _report = null;
            Changed("Дані імпортовано з JSON.");
            ConfirmUnsaved = false;
            Progress = 100;
        });
        ExportCommand = Command(async () =>
        {
            if (File.Exists(JsonPath) && !await _confirm("Перезаписати вибраний JSON-файл?"))
                return;
            await _repository.SaveAsync(JsonPath, _service.Snapshot(), Reporter(), Token);
            ConfirmUnsaved = false;
            Progress = 100;
            Message = "JSON збережено: " + Path.GetFullPath(JsonPath);
        });
        GenerateCommand = Command(async () =>
        {
            if (!await _confirm("Замінити поточні дані демонстраційним меню та 10 000 замовленнями?"))
                return;
            long revision = _service.Revision;
            var data = await Task.Run(() => new DemoDataGenerator().Generate(10_000, Token), Token);
            Token.ThrowIfCancellationRequested();
            _service.Replace(data, revision);
            _report = null;
            Changed("Створено 51 позицію меню та 10 000 синтетичних замовлень.");
            Progress = 100;
        });
        ReportCommand = Command(async () =>
        {
            var report = await new SalesReportService().CalculateAsync(_service.Snapshot().Orders, true, Reporter(), Token);
            Token.ThrowIfCancellationRequested();
            _report = report;
            ReportRows.Clear();
            foreach (var row in report.Categories)
                ReportRows.Add(row);
            ReportSummary = $"Оплачених замовлень: {report.PaidOrders}; виторг: {report.Total:0.00} грн.";
            Message = "Звіт сформовано.";
            Progress = 100;
        });
        XmlCommand = Command(async () =>
        {
            if (_report is null || ReportRows.Count == 0)
                throw new InvalidOperationException("Спочатку сформуйте звіт з оплаченими позиціями.");
            if (File.Exists(XmlPath) && !await _confirm("Перезаписати XML-звіт?"))
                return;
            await new XmlSalesReportExporter().ExportAsync(XmlPath, _report, Token);
            Message = "XML-звіт збережено: " + Path.GetFullPath(XmlPath);
            Progress = 100;
        });
        BenchmarkCommand = Command(async () =>
        {
            var result = await new BenchmarkService().RunAsync(_service.Snapshot().Orders, Token);
            Message = $"Середнє за {result.Runs} запусків: T1 = {result.SequentialMilliseconds:0.000} мс; Tn = {result.ParallelMilliseconds:0.000} мс; S = {result.Speedup:0.00}.";
            Progress = 100;
        });
    }
}
