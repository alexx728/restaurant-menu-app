using System.Diagnostics;
using Restaurant.Domain.Models;

namespace Restaurant.Domain.Services;
public sealed record BenchmarkResult(double SequentialMilliseconds, double ParallelMilliseconds, int Runs)
{
    public double Speedup => SequentialMilliseconds / ParallelMilliseconds;
}

public sealed class BenchmarkService
{
    public async Task<BenchmarkResult> RunAsync(IReadOnlyList<Order> orders, CancellationToken token = default)
    {
        var service = new SalesReportService();
        await service.CalculateAsync(orders, false, token: token);
        await service.CalculateAsync(orders, true, token: token);
        double sequential = 0, parallel = 0;
        const int runs = 5;
        for (int i = 0; i < runs; i++)
        {
            foreach (bool mode in i % 2 == 0 ? new[]
            {
                false,
                true
            }

            : new[]
            {
                true,
                false
            }

            )
            {
                var timer = Stopwatch.StartNew();
                await service.CalculateAsync(orders, mode, token: token);
                timer.Stop();
                if (mode)
                    parallel += timer.Elapsed.TotalMilliseconds;
                else
                    sequential += timer.Elapsed.TotalMilliseconds;
            }
        }

        return new(sequential / runs, parallel / runs, runs);
    }
}
