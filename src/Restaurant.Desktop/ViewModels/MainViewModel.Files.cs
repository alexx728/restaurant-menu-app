using Restaurant.Domain.Models;
using Restaurant.Domain.Persistence;
using Restaurant.Domain.Services;

namespace Restaurant.Desktop.ViewModels;

public sealed partial class MainViewModel
{
    private SalesReport? _report;
    private string _reportSummary = "Сформуйте звіт, щоб побачити виторг за оплаченими замовленнями.";

    public string ReportSummary { get => _reportSummary; private set => Set(ref _reportSummary, value); }
    public AsyncCommand ImportCommand { get; private set; } = null!;
    public AsyncCommand ExportCommand { get; private set; } = null!;
    public AsyncCommand GenerateCommand { get; private set; } = null!;
    public AsyncCommand ReportCommand { get; private set; } = null!;
    public AsyncCommand XmlCommand { get; private set; } = null!;
    public AsyncCommand BenchmarkCommand { get; private set; } = null!;

    private void InitializeFileCommands()
    {
        ImportCommand = Command(ImportAsync);
        ExportCommand = Command(ExportAsync);
        GenerateCommand = Command(GenerateAsync);
        ReportCommand = Command(CreateReportAsync);
        XmlCommand = Command(ExportReportAsync, () => _report is not null);
        BenchmarkCommand = Command(BenchmarkAsync);
    }

    private async Task ImportAsync()
    {
        if (!await ConfirmAsync("Імпорт замінить поточне меню та замовлення. Продовжити?"))
            return;

        long revision = _service.Revision;
        RestaurantData data = await _repository.LoadAsync(JsonPath, Reporter(), Token);
        Token.ThrowIfCancellationRequested();
        _service.Replace(data, revision);
        Changed("Дані імпортовано.");
        ConfirmUnsaved = false;
        Progress = 100;
    }

    private async Task ExportAsync()
    {
        if (File.Exists(JsonPath) && !await ConfirmAsync("Замінити наявний файл JSON?"))
            return;

        await _repository.SaveAsync(JsonPath, _service.Snapshot(), Reporter(), Token);
        ConfirmUnsaved = false;
        Progress = 100;
        Message = "Дані збережено: " + Path.GetFullPath(JsonPath);
    }

    private async Task GenerateAsync()
    {
        if (!await ConfirmAsync("Замінити поточні дані демонстраційним меню та 10 000 замовленнями?"))
            return;

        long revision = _service.Revision;
        CancellationToken token = Token;
        RestaurantData data = await Task.Run(() => new DemoDataGenerator().Generate(10_000, token), token);
        token.ThrowIfCancellationRequested();
        _service.Replace(data, revision);
        Changed("Створено 51 позицію меню та 10 000 демонстраційних замовлень.");
        Progress = 100;
    }

    private async Task CreateReportAsync()
    {
        SalesReport report = await new SalesReportService()
            .CalculateAsync(_service.Snapshot().Orders, true, Reporter(), Token);
        Token.ThrowIfCancellationRequested();
        _report = report;
        ReportRows.Clear();
        foreach (CategorySales row in report.Categories)
            ReportRows.Add(row);
        ReportSummary = $"Оплачених замовлень: {report.PaidOrders} · Виторг: {report.Total:0.00} грн";
        Message = "Звіт сформовано.";
        Progress = 100;
    }

    private async Task ExportReportAsync()
    {
        SalesReport report = _report ?? throw new InvalidOperationException("Спочатку сформуйте звіт.");
        if (File.Exists(XmlPath) && !await ConfirmAsync("Замінити наявний звіт XML?"))
            return;

        await new XmlSalesReportExporter().ExportAsync(XmlPath, report, Token);
        Message = "Звіт збережено: " + Path.GetFullPath(XmlPath);
        Progress = 100;
    }

    private async Task BenchmarkAsync()
    {
        BenchmarkResult result = await new BenchmarkService().RunAsync(_service.Snapshot().Orders, Token);
        Message = $"Середнє за {result.Runs} запусків: T1 = {result.SequentialMilliseconds:0.000} мс; " +
            $"Tn = {result.ParallelMilliseconds:0.000} мс; S = {result.Speedup:0.00}.";
        Progress = 100;
    }
}
