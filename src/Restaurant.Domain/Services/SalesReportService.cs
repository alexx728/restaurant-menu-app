using System.Collections.Concurrent;
using Restaurant.Domain.Models;

namespace Restaurant.Domain.Services;
public sealed class SalesReportService
{
    public Task<SalesReport> CalculateAsync(IReadOnlyList<Order> orders, bool parallel, IProgress<OperationProgress>? progress = null, CancellationToken token = default)
    {
        OrderLine[][] paid = orders.Where(x => x.Status == OrderStatus.Paid).Select(x => x.Lines.ToArray()).ToArray();
        return Task.Run(() => Calculate(paid, parallel, progress, token), token);
    }

    private static SalesReport Calculate(OrderLine[][] orders, bool parallel, IProgress<OperationProgress>? progress, CancellationToken token)
    {
        var totals = new Dictionary<string, CategorySales>(StringComparer.OrdinalIgnoreCase);
        object gate = new();
        int processed = 0;
        progress?.Report(new(0, "Розрахунок звіту"));
        void Process(int from, int to)
        {
            var local = new Dictionary<string, CategorySales>(StringComparer.OrdinalIgnoreCase);
            for (int i = from; i < to; i++)
            {
                token.ThrowIfCancellationRequested();
                foreach (var line in orders[i])
                    Add(local, line.Category, line.Quantity, line.Total);
            }

            lock (gate)
            {
                foreach (var row in local.Values)
                    Add(totals, row.Category, row.Quantity, row.Revenue);
                processed += to - from;
                progress?.Report(new(orders.Length == 0 ? 100 : processed * 100 / orders.Length, "Підсумки за категоріями"));
            }
        }

        if (parallel && orders.Length > 0)
            Parallel.ForEach(Partitioner.Create(0, orders.Length, Math.Max(1, orders.Length / Math.Max(1, Environment.ProcessorCount * 4))), new ParallelOptions { CancellationToken = token }, range => Process(range.Item1, range.Item2));
        else
            Process(0, orders.Length);
        token.ThrowIfCancellationRequested();
        var rows = totals.Values.OrderBy(x => x.Category, StringComparer.OrdinalIgnoreCase).ToList();
        progress?.Report(new(100, "Звіт готовий"));
        return new SalesReport
        {
            PaidOrders = orders.Length,
            Categories = rows,
            Total = rows.Sum(x => x.Revenue)
        };
    }

    private static void Add(Dictionary<string, CategorySales> totals, string category, long quantity, decimal revenue)
    {
        if (!totals.TryGetValue(category, out var row))
            totals.Add(category, row = new() { Category = category });
        if (string.CompareOrdinal(category, row.Category) < 0)
            row.Category = category;
        row.Quantity = checked(row.Quantity + quantity);
        row.Revenue = checked(row.Revenue + revenue);
    }
}
