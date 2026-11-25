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
        ArgumentNullException.ThrowIfNull(orders);
        var reports = new SalesReportService();
        await reports.CalculateAsync(orders, false, token: token);
        await reports.CalculateAsync(orders, true, token: token);

        const int runs = 5;
        double[] elapsed = new double[2];
        for (int run = 0; run < runs; run++)
        {
            for (int turn = 0; turn < 2; turn++)
            {
                token.ThrowIfCancellationRequested();
                int mode = (run + turn) % 2;
                long started = Stopwatch.GetTimestamp();
                await reports.CalculateAsync(orders, mode == 1, token: token);
                elapsed[mode] += Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            }
        }
        return new BenchmarkResult(elapsed[0] / runs, elapsed[1] / runs, runs);
    }
}
