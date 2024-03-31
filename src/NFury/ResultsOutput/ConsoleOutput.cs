using Spectre.Console;

namespace NFury.ResultsOutput;

public class ConsoleOutput : IResultsOutput
{
    public void Write(Results results)
    {
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
        
        // TODO: Add results per status code 
        // foreach (var statusCode in statusCodes)
        // {
        //     var statusCodeString = statusCode.StatusCode.ToString();
        //     chart.AddItem(statusCodeString, (int)statusCode.Total, Color.FromInt32(Random.Shared.Next(256)));
        //     statusCodeResult.AddRow(statusCodeString,
        //         statusCode.MinElapsedTime.ToString("F2"),
        //         statusCode.AvgElapsedTime.ToString("F2"),
        //         statusCode.MaxElapsedTime.ToString("F2"),
        //         CalculatePercentile(_responses.Where(p => p.StatusCode == statusCode.StatusCode)
        //             .Select(p => p.ElapsedTime).ToList(), 50).ToString("F2"),
        //         CalculatePercentile(_responses.Where(p => p.StatusCode == statusCode.StatusCode)
        //             .Select(p => p.ElapsedTime).ToList(), 75).ToString("F2"),
        //         CalculatePercentile(_responses.Where(p => p.StatusCode == statusCode.StatusCode)
        //             .Select(p => p.ElapsedTime).ToList(), 90).ToString("F2"),
        //         CalculatePercentile(_responses.Where(p => p.StatusCode == statusCode.StatusCode)
        //             .Select(p => p.ElapsedTime).ToList(), 95).ToString("F2"),
        //         CalculatePercentile(_responses.Where(p => p.StatusCode == statusCode.StatusCode)
        //             .Select(p => p.ElapsedTime).ToList(), 99).ToString("F2")
        //     );
        // }
        
        var globalResults = new Table
        {
            Title = new TableTitle("Results")
        };

        globalResults.AddColumn("Metric");
        globalResults.AddColumn(new TableColumn("Value").RightAligned());
        globalResults.AddColumn(new TableColumn("Unit").Centered());

        globalResults.AddRow("Test duration", results.TotalTime.ToString("F2"), "ms");
        globalResults.AddRow("Requests", (results.TotalRequests / ((double)results.TotalTime / 1000)).ToString("F1"), "req/s");
        globalResults.AddRow("Avg. Response Time", results.AverageResponseTime.ToString("F2"), "ms");
        globalResults.AddRow("Pct 50", results.Pct50.ToString("F2"), "ms");
        globalResults.AddRow("Pct 75", results.Pct75.ToString("F2"), "ms");
        globalResults.AddRow("Pct 90", results.Pct90.ToString("F2"), "ms");
        globalResults.AddRow("Pct 95", results.Pct95.ToString("F2"), "ms");
        globalResults.AddRow("Pct 99", results.Pct99.ToString("F2"), "ms");

        AnsiConsole.Write(chart);
        AnsiConsole.Write(globalResults);
        AnsiConsole.Write(statusCodeResult);
    }
}