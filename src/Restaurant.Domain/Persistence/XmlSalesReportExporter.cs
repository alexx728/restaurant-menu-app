using System.Xml.Serialization;
using Restaurant.Domain.Models;

namespace Restaurant.Domain.Persistence;

public sealed class XmlSalesReportExporter
{
    public Task ExportAsync(string path, SalesReport report, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(report);
        token.ThrowIfCancellationRequested();
        var snapshot = new SalesReport
        {
            GeneratedAt = report.GeneratedAt,
            PaidOrders = report.PaidOrders,
            Total = report.Total,
            Categories = report.Categories.Select(row => new CategorySales
            {
                Category = row.Category,
                Quantity = row.Quantity,
                Revenue = row.Revenue
            }).ToList()
        };
        return Task.Run(() => AtomicFile.WriteAsync(path, stream =>
        {
            token.ThrowIfCancellationRequested();
            new XmlSerializer(typeof(SalesReport)).Serialize(stream, snapshot);
            return Task.CompletedTask;
        }, token), token);
    }
}
