using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using Spectre.Console;
using Spectre.Console.Cli;

namespace NFury.Commands;

public class RunCommand : AsyncCommand<RunCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("-u|--virtual-users")]
        [DefaultValue(10)]
        [Description("Define a number of concurrency users. Default is 10.")]
        public int VirtualUsers { get; set; }
        
        [CommandOption("-r|--requests")]
        [DefaultValue(100)]
        [Description("Define a number of requests. Default is 100.")]
        public int Requests { get; set; }
        
        [CommandArgument(0, "[URL]")]
        public string Url { get; set; }
        
        [CommandOption("-m|--method <METHOD>")]
        [DefaultValue("GET")]
        public string Method { get; set; }
    }


    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var numberOfUsers = settings.VirtualUsers;
        var totalRequests = settings.Requests;
        ConcurrentBag<long> responseTimes = new();

        List<Task> tasks = new (numberOfUsers);
        long totalElapsedTime = 0;
        using var httpClient = new HttpClient();
        {
            for (var i = 0; i < numberOfUsers; i++)
            {
                tasks.Add(SendRequests(settings.Url, settings.Method, httpClient, totalRequests / numberOfUsers, responseTimes));
            }

            var startTime = Stopwatch.GetTimestamp();
            await Task.WhenAll(tasks);
            totalElapsedTime = Stopwatch.GetElapsedTime(startTime).Ticks / 10000L;
        }

        long totalTime = 0;
        foreach (var time in responseTimes)
        {
            totalTime += time;
        }

        var values = responseTimes.ToList();
        var pct50 = CalculatePercentile(values, 50);
        var pct99 = CalculatePercentile(values, 99);

        double averageResponseTime = (double)totalTime / responseTimes.Count;
        Console.WriteLine($"Test duration: {totalElapsedTime} ms");
        Console.WriteLine($"Request/sec: {totalElapsedTime / settings.Requests}");
        Console.WriteLine($"Tempo médio de resposta: {averageResponseTime} milissegundos");
        Console.WriteLine($"Percentile 50: {pct50} milissegundos");
        Console.WriteLine($"Percentile 99: {pct99} milissegundos");
        Console.WriteLine("Teste de carga concluído!");

        return await Task.FromResult(0);
    }

    private async Task SendRequests(string url, string method, HttpClient client, int numberOfRequests, ConcurrentBag<long> responseTimes)
    {
        for (int i = 0; i < numberOfRequests; i++)
        {
            var request = new HttpRequestMessage(GetMethod(method), url);
            var startTime = Stopwatch.GetTimestamp();
            HttpResponseMessage response = await client.SendAsync(request);
            long elapsedMilliseconds = Stopwatch.GetElapsedTime(startTime).Ticks / 10000L;
            responseTimes.Add(elapsedMilliseconds);
            Console.WriteLine($"Resposta: {response.StatusCode} - Tempo de Resposta: {elapsedMilliseconds}ms - {DateTime.Now}");
        }
    }

    private HttpMethod GetMethod(string method)
    {
        return method.ToUpperInvariant() switch
        {
            "GET" => HttpMethod.Get,
            "POST" => HttpMethod.Post,
            _ => throw new ArgumentOutOfRangeException(nameof(method), method, null)
        };
    }

    static long CalculatePercentile(List<long> values, int percentile)
    {
        values.Sort(); // Classifique a lista de tempos em ordem crescente

        if (values.Count == 0)
        {
            throw new InvalidOperationException("A lista está vazia.");
        }

        if (percentile is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException("percentile", "O valor do percentil deve estar entre 0 e 100.");
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