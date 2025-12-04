using Spectre.Console;
using Spectre.Console.Cli;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace NFury.Commands.Run;

public class RunCommand : AsyncCommand<RunSettings>
{
    private readonly ConcurrentBag<Response> _responses = [];
    private ProgressTask? _task;

    public override async Task<int> ExecuteAsync(CommandContext context, RunSettings settings)
    {
        List<Task> tasks = new(settings.Users);
        long totalElapsedTime = 0;
        double progress = 0;

        AnsiConsole.Write(new Markup("[bold green]Initializing the test...[/]"));

        using var httpClient = GenerateHttpClient(settings.Insecure);
        {
            if (settings.Duration.HasValue)
            {
                await AnsiConsole.Progress()
                    .AutoRefresh(true)
                    .AutoClear(true)
                     .Columns(
                        [
                            new TaskDescriptionColumn(),
                            new ProgressBarColumn(),
                            new PercentageColumn(),
                            new ElapsedTimeColumn(),
                            new SpinnerColumn(),
                        ])
                    .StartAsync(async ctx =>
                    {
                        _task = ctx.AddTask("[green]Testing...[/]");
                        var startTime = Stopwatch.GetTimestamp();
                        var stopTime = DateTime.Now.AddSeconds(settings.Duration.Value);
                        _task.MaxValue = (double)settings.Duration.Value;
                        progress = 1 / ((double)settings.Duration.Value * 500) / (double)settings.Users * 100;
                        for (var i = 0; i < settings.Users; i++)
                        {
                            tasks.Add(RunUserForDurationTasks(httpClient, progress, CreateRequest(settings), stopTime));
                        }

                        await Task.WhenAll(tasks);
                        totalElapsedTime = Stopwatch.GetElapsedTime(startTime).Ticks / 10000L;
                    });
            }
            else
            {
                progress = 1 / (double)settings.Requests * 100;
                var request = CreateRequest(settings);
                for (var i = 0; i < settings.Users; i++)
                {
                    tasks.Add(RunUserForExecutionTasks(httpClient, progress, CreateRequest(settings)));
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
        }

        ShowResults(settings, totalElapsedTime);

        return await Task.FromResult(0);
    }

    private static HttpClient GenerateHttpClient(bool? insecure)
    {
        if (insecure.HasValue)
        {
            var handler = new HttpClientHandler();
            handler.ClientCertificateOptions = ClientCertificateOption.Manual;
            handler.ServerCertificateCustomValidationCallback =
                (httpRequestMessage, cert, cetChain, policyErrors) =>
                {
                    return true;
                };
            return new HttpClient(handler);
        }

        return new HttpClient();
    }

    private async Task RunUserForDurationTasks(HttpClient client, double progress, Request request, DateTime stopTime)
    {
        while (DateTime.Now < stopTime)
        {
            await SendRequests(client, request);
            _task?.Increment(progress);
        }
    }
    private async Task RunUserForExecutionTasks(HttpClient client, double progress, Request request)
    {
        for (int i = 0; i < request.NumberOfRequests; i++)
        {
            await SendRequests(client, request);
            _task?.Increment(progress);
        }
    }
    private static Request CreateRequest(RunSettings settings)
    {
        return new Request(settings.Url!, settings.Method!, settings?.Body, settings?.ContentType, settings!.Requests / settings.Users);
    }
    private async Task SendRequests(HttpClient client, Request request)
    {
        using var httpRequest = GenerateHttpRequest(request);
        var startTime = Stopwatch.GetTimestamp();
        using var response = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        long elapsedMilliseconds = Stopwatch.GetElapsedTime(startTime).Ticks / 10000L;
        _responses.Add(new Response(Guid.NewGuid(), elapsedMilliseconds, response.StatusCode));
    }
    private static HttpRequestMessage GenerateHttpRequest(Request request)
    {
        var httpRequest = new HttpRequestMessage(GetMethod(request.Method), request.Url);
        if (!string.IsNullOrWhiteSpace(request.Body))
            httpRequest.Content = new StringContent(request.Body, Encoding.UTF8, request.ContentType!);

        return httpRequest;
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
                    statusCode.MinElapsedTime.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                    statusCode.AvgElapsedTime.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                    statusCode.MaxElapsedTime.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                    CalculatePercentile(_responses.Where(p => p.StatusCode == statusCode.StatusCode)
                        .Select(p => p.ElapsedTime).ToList(), 50).ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                    CalculatePercentile(_responses.Where(p => p.StatusCode == statusCode.StatusCode)
                        .Select(p => p.ElapsedTime).ToList(), 75).ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                    CalculatePercentile(_responses.Where(p => p.StatusCode == statusCode.StatusCode)
                     .Select(p => p.ElapsedTime).ToList(), 90).ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                    CalculatePercentile(_responses.Where(p => p.StatusCode == statusCode.StatusCode)
                        .Select(p => p.ElapsedTime).ToList(), 95).ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                    CalculatePercentile(_responses.Where(p => p.StatusCode == statusCode.StatusCode)
                        .Select(p => p.ElapsedTime).ToList(), 99).ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                    );
        }

        var globalResults = new Table
        {
            Title = new TableTitle("Results")
        };

        globalResults.AddColumn("Metric");
        globalResults.AddColumn(new TableColumn("Value").RightAligned());
        globalResults.AddColumn(new TableColumn("Unit").Centered());

        globalResults.AddRow("Test duration", totalElapsedTime.ToString("F2", System.Globalization.CultureInfo.InvariantCulture), "ms");
        if (settings.Duration.HasValue)
        {
            globalResults.AddRow("Total Requests", _responses.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), "req");
            globalResults.AddRow("Requests", (_responses.Count / ((double)totalElapsedTime / 1000)).ToString("F1", System.Globalization.CultureInfo.InvariantCulture), "req/s");
        }
        else
        {
            globalResults.AddRow("Requests", (settings.Requests / ((double)totalElapsedTime / 1000)).ToString("F1", System.Globalization.CultureInfo.InvariantCulture), "req/s");
        }
        globalResults.AddRow("Avg. Response Time", averageResponseTime.ToString("F2", System.Globalization.CultureInfo.InvariantCulture), "ms");
        globalResults.AddRow("Pct 50", CalculatePercentile(values, 50).ToString("F2", System.Globalization.CultureInfo.InvariantCulture), "ms");
        globalResults.AddRow("Pct 75", CalculatePercentile(values, 75).ToString("F2", System.Globalization.CultureInfo.InvariantCulture), "ms");
        globalResults.AddRow("Pct 90", CalculatePercentile(values, 90).ToString("F2", System.Globalization.CultureInfo.InvariantCulture), "ms");
        globalResults.AddRow("Pct 95", CalculatePercentile(values, 95).ToString("F2", System.Globalization.CultureInfo.InvariantCulture), "ms");
        globalResults.AddRow("Pct 99", CalculatePercentile(values, 99).ToString("F2", System.Globalization.CultureInfo.InvariantCulture), "ms");

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
            "PUT" => HttpMethod.Put,
            "DELETE" => HttpMethod.Delete,
            "PATCH" => HttpMethod.Patch,
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
            throw new ArgumentOutOfRangeException(nameof(percentile), "The percentile value must be between 0 and 100.");
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