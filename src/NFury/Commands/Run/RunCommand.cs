using Spectre.Console;
using Spectre.Console.Cli;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace NFury.Commands.Run;

public class RunCommand : AsyncCommand<RunSettings>
{
    private readonly ConcurrentBag<Response> _responses = [];
    private ProgressTask? _task;

    public override async Task<int> ExecuteAsync(CommandContext context, RunSettings settings)
    {
        List<Task> tasks = new(settings.VirtualUsers);
        long totalElapsedTime = 0;
        double progress = 1 / (double)settings.Requests * 100;

        AnsiConsole.Write(new Markup("[bold green]Initializing the test...[/]"));

        using var httpClient = new HttpClient();
        {
            for (var i = 0; i < settings.VirtualUsers; i++)
            {
                tasks.Add(SendRequests(settings.Url!, settings.Method!, httpClient, settings.Requests / settings.VirtualUsers, progress));
            }

            await AnsiConsole.Progress()
                .AutoRefresh(true)
                .AutoClear(true)
                 .Columns(
                    [
                        new TaskDescriptionColumn(),
                        new ProgressBarColumn(),
                        new PercentageColumn(),
                        new SpinnerColumn(),
                    ])
                .StartAsync(async ctx =>
                {
                    _task = ctx.AddTask("[green]Testing...[/]");
                    var startTime = Stopwatch.GetTimestamp();
                    await Task.WhenAll(tasks);
                    totalElapsedTime = Stopwatch.GetElapsedTime(startTime).Ticks / 10000L;
                });
        }

        ShowResults(settings, totalElapsedTime);

        return await Task.FromResult(0);
    }

    private async Task SendRequests(string url, string method, HttpClient client,
        int numberOfRequests, double progress)
    {
        for (int i = 0; i < numberOfRequests; i++)
        {
            using var request = new HttpRequestMessage(GetMethod(method), url);
            var startTime = Stopwatch.GetTimestamp();
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            long elapsedMilliseconds = Stopwatch.GetElapsedTime(startTime).Ticks / 10000L;
            _responses.Add(new Response(Guid.NewGuid(), elapsedMilliseconds, response.StatusCode));
            _task?.Increment(progress);
        }
    }

    private void ShowResults(RunSettings settings, long totalElapsedTime)
    {
        long totalTime = _responses.Sum(p => p.ElapsedTime);
        var values = _responses.Select(p => p.ElapsedTime).ToList();

        double averageResponseTime = (double)totalTime / _responses.Count;

        var statusCodes = _responses
                            .GroupBy(r => r.StatusCode)
                            .Select(cl => new
                            {
                                StatusCode = cl.Key,
                                Total = (int)cl.Count(),
                                MinElapsedTime = cl.Min(c => c.ElapsedTime),
                                AvgElapsedTime = cl.Average(c => c.ElapsedTime),
                                MaxElapsedTime = cl.Max(c => c.ElapsedTime)
                            }).ToList();

        var chart = new BreakdownChart()
            .Width(60);

        var statusCodeResult = new Table
        {
            Title = new TableTitle("Results per Status Code")
        };

        statusCodeResult.AddColumn("Status Code");
        statusCodeResult.AddColumn("Min (ms)");
        statusCodeResult.AddColumn("Avg (ms)");
        statusCodeResult.AddColumn("Max (ms)");
        statusCodeResult.AddColumn("Pct 50");
        statusCodeResult.AddColumn("Pct 75");
        statusCodeResult.AddColumn("Pct 90");
        statusCodeResult.AddColumn("Pct 95");
        statusCodeResult.AddColumn("Pct 99");

        foreach (var statusCode in statusCodes)
        {
            var statusCodeString = statusCode.StatusCode.ToString();
            chart.AddItem(statusCodeString, (int)statusCode.Total, Color.FromInt32(Random.Shared.Next(256)));
            statusCodeResult.AddRow(statusCodeString,
                    statusCode.MinElapsedTime.ToString("F2"),
                    statusCode.AvgElapsedTime.ToString("F2"),
                    statusCode.MaxElapsedTime.ToString("F2"),
                    CalculatePercentile(_responses.Where(p => p.StatusCode == statusCode.StatusCode)
                        .Select(p => p.ElapsedTime).ToList(), 50).ToString("F2"),
                    CalculatePercentile(_responses.Where(p => p.StatusCode == statusCode.StatusCode)
                        .Select(p => p.ElapsedTime).ToList(), 75).ToString("F2"),
                    CalculatePercentile(_responses.Where(p => p.StatusCode == statusCode.StatusCode)
                     .Select(p => p.ElapsedTime).ToList(), 90).ToString("F2"),
                    CalculatePercentile(_responses.Where(p => p.StatusCode == statusCode.StatusCode)
                        .Select(p => p.ElapsedTime).ToList(), 95).ToString("F2"),
                    CalculatePercentile(_responses.Where(p => p.StatusCode == statusCode.StatusCode)
                        .Select(p => p.ElapsedTime).ToList(), 99).ToString("F2")
                    );
        }
       

        var globalResults = new Table
        {
            Title = new TableTitle("Results")
        };

        globalResults.AddColumn("Metric");
        globalResults.AddColumn(new TableColumn("Value").RightAligned());
        globalResults.AddColumn(new TableColumn("Unit").Centered());

        globalResults.AddRow("Test duration", totalElapsedTime.ToString("F2"), "ms");
        globalResults.AddRow("Requests", (settings.Requests / ((double)totalElapsedTime / 1000)).ToString("F1"), "req/s");
        globalResults.AddRow("Avg. Response Time", averageResponseTime.ToString("F2"), "ms");
        globalResults.AddRow("Pct 50", CalculatePercentile(values, 50).ToString("F2"), "ms");
        globalResults.AddRow("Pct 75", CalculatePercentile(values, 75).ToString("F2"), "ms");
        globalResults.AddRow("Pct 90", CalculatePercentile(values, 90).ToString("F2"), "ms");
        globalResults.AddRow("Pct 95", CalculatePercentile(values, 95).ToString("F2"), "ms");
        globalResults.AddRow("Pct 99", CalculatePercentile(values, 99).ToString("F2"), "ms");

        AnsiConsole.Write(chart);
        AnsiConsole.Write(globalResults);
        AnsiConsole.Write(statusCodeResult);

        if (AnsiConsole.Confirm("Show requests?"))
        {
            foreach (var response in _responses)
            {
                Console.WriteLine(response);
            }
        }
    }

    private static HttpMethod GetMethod(string method)
    {
        return method.ToUpperInvariant() switch
        {
            "GET" => HttpMethod.Get,
            "POST" => HttpMethod.Post,
            _ => throw new ArgumentOutOfRangeException(nameof(method), method, null)
        };
    }

    private static long CalculatePercentile(List<long> values, int percentile)
    {
        values.Sort();

        if (values.Count == 0)
        {
            throw new InvalidOperationException("The list is empty.");
        }

        if (percentile is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException("percentile", "The percentile value must be between 0 and 100.");
        }

        int n = values.Count;
        double position = (n + 1) * percentile / 100.0;
        double index = position - 1;
        int intIndex = (int)index;
        double fraction = index - intIndex;

        if (intIndex < 0)
        {
            return values[0];
        }
        else if (intIndex >= n - 1)
        {
            return values[n - 1];
        }
        else
        {
            return (long)(values[intIndex] + fraction * (values[intIndex + 1] - values[intIndex]));
        }
    }
}