using Restaurant.Domain.Models;

namespace Restaurant.Domain.Services;

public sealed class SalesReportService
{
    public Task<SalesReport> CalculateAsync(IReadOnlyList<Order> orders, bool parallel,
        IProgress<OperationProgress>? progress = null, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(orders);
        var paid = new List<OrderLine[]>();
        foreach (Order order in orders)
        {
            token.ThrowIfCancellationRequested();
            if (order.Status == OrderStatus.Paid)
                paid.Add(order.Lines.ToArray());
        }

        return Task.Run(() => Calculate(paid, parallel, progress, token), token);
    }

    private static SalesReport Calculate(List<OrderLine[]> orders, bool parallel,
        IProgress<OperationProgress>? progress, CancellationToken token)
    {
        var totals = new Dictionary<string, CategorySales>(StringComparer.OrdinalIgnoreCase);
        var gate = new object();
        int processed = 0;
        progress?.Report(new(0, "Розрахунок звіту"));

        void Process(OrderLine[][] batch)
        {
            var subtotals = new Dictionary<string, CategorySales>(StringComparer.OrdinalIgnoreCase);
            foreach (OrderLine[] order in batch)
            {
                token.ThrowIfCancellationRequested();
                foreach (OrderLine line in order)
                {
                    token.ThrowIfCancellationRequested();
                    Accumulate(subtotals, line.Category, line.Quantity, line.Total);
                }
            }

            lock (gate)
            {
                token.ThrowIfCancellationRequested();
                foreach (CategorySales subtotal in subtotals.Values)
                    Accumulate(totals, subtotal.Category, subtotal.Quantity, subtotal.Revenue);

                processed += batch.Length;
                progress?.Report(new((int)(processed * 100L / orders.Count), "Підсумки за категоріями"));
            }
        }

        var batches = orders.Chunk(256);
        if (parallel)
            Parallel.ForEach(batches, new ParallelOptions { CancellationToken = token }, Process);
        else
            foreach (OrderLine[][] batch in batches)
                Process(batch);

        token.ThrowIfCancellationRequested();
        var categories = totals.Values.OrderBy(row => row.Category, StringComparer.OrdinalIgnoreCase).ToList();
        var report = new SalesReport
        {
            PaidOrders = orders.Count,
            Categories = categories,
            Total = categories.Sum(row => row.Revenue)
        };
        progress?.Report(new(100, "Звіт готовий"));
        token.ThrowIfCancellationRequested();
        return report;
    }

    private static void Accumulate(Dictionary<string, CategorySales> totals,
        string category, long quantity, decimal revenue)
    {
        if (!totals.TryGetValue(category, out CategorySales? row))
        {
            row = new CategorySales { Category = category };
            totals.Add(category, row);
        }

        row.Quantity = checked(row.Quantity + quantity);
        row.Revenue = checked(row.Revenue + revenue);
        if (string.CompareOrdinal(category, row.Category) < 0)
            row.Category = category;
    }
}
