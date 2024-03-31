using NFury.Commands.Run;

namespace NFury;

public class Results
{
    private readonly IList<Response> _responses;

    public long TotalTime { get; private set; }
    public long TotalRequests { get; private set;  }
    public double AverageResponseTime { get; private set; }
    public double RequestsPerSecond { get; private set; }
    public long Pct99 { get; private set; }
    public long Pct95 { get; private set; }
    public long Pct90 { get; private set; }
    public long Pct75 { get; private set; }
    public long Pct50 { get; private set; }
    
    public Results(IList<Response> responses)
    {
        _responses = responses;
        TotalRequests = _responses.Count;
        TotalTime = _responses.Sum(r => r.ElapsedTime);
        AverageResponseTime = (double)TotalTime / _responses.Count;
        RequestsPerSecond = _responses.Count / ((double)TotalTime / 1000);
        var elapsedTimes = _responses.Select(r => r.ElapsedTime).ToList();

        Pct50 = CalculatePercentile(elapsedTimes, 50);
        Pct75 = CalculatePercentile(elapsedTimes, 75);
        Pct90 = CalculatePercentile(elapsedTimes, 90);
        Pct95 = CalculatePercentile(elapsedTimes, 95);
        Pct99 = CalculatePercentile(elapsedTimes, 99);
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