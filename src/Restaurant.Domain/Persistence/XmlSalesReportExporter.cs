using System.Xml.Serialization;
using Restaurant.Domain.Models;

namespace Restaurant.Domain.Persistence;
public sealed class XmlSalesReportExporter
{
    public Task ExportAsync(string path, SalesReport report, CancellationToken token = default) => Task.Run(async () =>
    {
        token.ThrowIfCancellationRequested();
        await AtomicFile.WriteAsync(path, stream =>
        {
            new XmlSerializer(typeof(SalesReport)).Serialize(stream, report);
            return Task.CompletedTask;
        }, token);
    }, token);
}
