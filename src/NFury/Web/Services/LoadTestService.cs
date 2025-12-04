using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using NFury.Web.Data;
using NFury.Web.Hubs;

namespace NFury.Web.Services;

/// <summary>
/// Core service for executing load tests against API endpoints
/// </summary>
/// <remarks>
/// Handles concurrent HTTP request execution, real-time metrics collection,
/// authentication, and SignalR-based progress updates.
/// </remarks>
public class LoadTestService : IDisposable
{
    private readonly IHubContext<LoadTestHub> _hubContext;
    private readonly ExecutionService _executionService;
    private readonly ProjectService _projectService;
    private readonly ConcurrentBag<ResponseData> _responses = [];
    private readonly ConcurrentQueue<DateTime> _recentRequestTimes = new();
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _isRunning;
    private string _currentTestId = string.Empty;
    private int? _currentEndpointId;
    private readonly object _lockObject = new();
    private string? _authToken;
    private int _metricCounter;
    private double _peakRps;
    private const double RpsWindowSeconds = 1.0;

    /// <summary>
    /// Gets whether a test is currently running
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoadTestService"/> class
    /// </summary>
    /// <param name="hubContext">The SignalR hub context for sending real-time updates</param>
    /// <param name="executionService">The execution service for persisting test data</param>
    /// <param name="projectService">The project service for accessing project configuration</param>
    public LoadTestService(IHubContext<LoadTestHub> hubContext, ExecutionService executionService, ProjectService projectService)
    {
        _hubContext = hubContext;
        _executionService = executionService;
        _projectService = projectService;
    }

    /// <summary>
    /// Authenticates against an endpoint to obtain a token for subsequent requests
    /// </summary>
    /// <param name="config">The authentication configuration</param>
    /// <param name="insecure">Whether to skip SSL certificate validation</param>
    /// <returns>The authentication result with token if successful</returns>
    public async Task<AuthenticationResult> AuthenticateAsync(AuthenticationConfig config, bool insecure)
    {
        try
        {
            using var httpClient = GenerateHttpClient(insecure);
            using var request = new HttpRequestMessage(GetMethod(config.Method), config.Url);

            if (!string.IsNullOrWhiteSpace(config.Body))
            {
                request.Content = new StringContent(config.Body, Encoding.UTF8, config.ContentType);
            }

            if (config.Headers != null)
            {
                foreach (var header in config.Headers)
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            var response = await httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                return new AuthenticationResult
                {
                    Success = false,
                    Error = $"Authentication failed with status {(int)response.StatusCode}"
                };
            }

            var content = await response.Content.ReadAsStringAsync();
            var token = ExtractToken(content, config.TokenPath);

            if (string.IsNullOrEmpty(token))
            {
                return new AuthenticationResult
                {
                    Success = false,
                    Error = $"Could not extract token from path '{config.TokenPath}'"
                };
            }

            return new AuthenticationResult { Success = true, Token = token };
        }
        catch (Exception ex)
        {
            return new AuthenticationResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Extracts a token from JSON response using a dot-notation path
    /// </summary>
    /// <param name="json">The JSON response content</param>
    /// <param name="tokenPath">The dot-notation path to the token (e.g., "access_token" or "data.token")</param>
    /// <returns>The extracted token or null if not found</returns>
    private static string? ExtractToken(string json, string tokenPath)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var pathParts = tokenPath.Split('.');
            JsonElement current = doc.RootElement;

            foreach (var part in pathParts)
            {
                if (current.TryGetProperty(part, out var next))
                {
                    current = next;
                }
                else
                {
                    return null;
                }
            }

            return current.GetString() ?? current.GetRawText().Trim('"');
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Starts a load test for a specific endpoint linked to a project
    /// </summary>
    /// <param name="endpointId">The endpoint identifier</param>
    /// <param name="usersOverride">Optional override for the number of concurrent users</param>
    /// <returns>The unique test identifier</returns>
    /// <exception cref="InvalidOperationException">Thrown when a test is already running or endpoint not found</exception>
    public async Task<string> StartEndpointTestAsync(int endpointId, int? usersOverride = null)
    {
        if (_isRunning)
        {
            throw new InvalidOperationException("A test is already running");
        }

        var endpoint = await _projectService.GetEndpointByIdAsync(endpointId);
        if (endpoint == null)
        {
            throw new InvalidOperationException($"Endpoint {endpointId} not found");
        }

        _currentTestId = Guid.NewGuid().ToString();
        _currentEndpointId = endpointId;
        _cancellationTokenSource = new CancellationTokenSource();
        _responses.Clear();
        _recentRequestTimes.Clear();
        _isRunning = true;
        _authToken = null;
        _metricCounter = 0;
        _peakRps = 0;

        await _executionService.CreateExecutionAsync(endpointId, _currentTestId, usersOverride);

        AuthenticationConfig? authConfig = null;
        if (!string.IsNullOrEmpty(endpoint.AuthenticationJson))
        {
            authConfig = JsonSerializer.Deserialize<AuthenticationConfig>(endpoint.AuthenticationJson, AppJsonContext.Default.AuthenticationConfig);
        }

        var targetRequests = endpoint.Requests;
        var targetDuration = endpoint.Duration;
        if (!targetRequests.HasValue && !targetDuration.HasValue)
        {
            targetRequests = 100;
        }

        var request = new LoadTestRequest
        {
            Url = endpoint.Url,
            Method = endpoint.Method,
            Users = usersOverride ?? endpoint.Users,
            Requests = targetRequests,
            Duration = targetDuration,
            Headers = !string.IsNullOrEmpty(endpoint.HeadersJson)
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(endpoint.HeadersJson, AppJsonContext.Default.DictionaryStringString)
                : null,
            Body = endpoint.Body,
            ContentType = endpoint.ContentType ?? "application/json",
            Insecure = endpoint.Insecure,
            Authentication = authConfig
        };

        if (request.Authentication != null && !string.IsNullOrWhiteSpace(request.Authentication.Url))
        {
            await _hubContext.Clients.All.SendAsync("AuthenticationStarted", new SignalRTestIdMessage { TestId = _currentTestId });

            var authResult = await AuthenticateAsync(request.Authentication, request.Insecure);

            if (!authResult.Success)
            {
                _isRunning = false;
                await _executionService.FailExecutionAsync(_currentTestId, $"Authentication failed: {authResult.Error}");
                await _hubContext.Clients.All.SendAsync("AuthenticationFailed", new SignalRTestErrorMessage { TestId = _currentTestId, Error = authResult.Error ?? "Unknown error" });
                throw new InvalidOperationException($"Authentication failed: {authResult.Error}");
            }

            _authToken = $"{request.Authentication.HeaderPrefix}{authResult.Token}";
            await _hubContext.Clients.All.SendAsync("AuthenticationSuccess", new SignalRTestIdMessage { TestId = _currentTestId });
        }

        _ = Task.Run(() => ExecuteTestAsync(request, _currentTestId, _cancellationTokenSource.Token));

        return _currentTestId;
    }

    /// <summary>
    /// Starts an ad-hoc load test not linked to any endpoint or project
    /// </summary>
    /// <param name="request">The load test configuration</param>
    /// <returns>The unique test identifier</returns>
    /// <exception cref="InvalidOperationException">Thrown when a test is already running</exception>
    public async Task<string> StartAdHocTestAsync(LoadTestRequest request)
    {
        if (_isRunning)
        {
            throw new InvalidOperationException("A test is already running");
        }

        _currentTestId = Guid.NewGuid().ToString();
        _currentEndpointId = null;
        _cancellationTokenSource = new CancellationTokenSource();
        _responses.Clear();
        _recentRequestTimes.Clear();
        _isRunning = true;
        _authToken = null;
        _metricCounter = 0;
        _peakRps = 0;

        await _executionService.CreateAdHocExecutionAsync(request, _currentTestId);

        if (request.Authentication != null && !string.IsNullOrWhiteSpace(request.Authentication.Url))
        {
            await _hubContext.Clients.All.SendAsync("AuthenticationStarted", new SignalRTestIdMessage { TestId = _currentTestId });

            var authResult = await AuthenticateAsync(request.Authentication, request.Insecure);

            if (!authResult.Success)
            {
                _isRunning = false;
                await _executionService.FailExecutionAsync(_currentTestId, $"Authentication failed: {authResult.Error}");
                await _hubContext.Clients.All.SendAsync("AuthenticationFailed", new SignalRTestErrorMessage { TestId = _currentTestId, Error = authResult.Error ?? "Unknown error" });
                throw new InvalidOperationException($"Authentication failed: {authResult.Error}");
            }

            _authToken = $"{request.Authentication.HeaderPrefix}{authResult.Token}";
            await _hubContext.Clients.All.SendAsync("AuthenticationSuccess", new SignalRTestIdMessage { TestId = _currentTestId });
        }

        _ = Task.Run(() => ExecuteTestAsync(request, _currentTestId, _cancellationTokenSource.Token));

        return _currentTestId;
    }

    /// <summary>
    /// Stops the currently running test
    /// </summary>
    public async Task StopTestAsync()
    {
        _cancellationTokenSource?.Cancel();
        if (!string.IsNullOrEmpty(_currentTestId))
        {
            await _executionService.CancelExecutionAsync(_currentTestId);
        }
    }

    /// <summary>
    /// Executes the load test with the specified configuration
    /// </summary>
    /// <param name="request">The load test configuration</param>
    /// <param name="testId">The unique test identifier</param>
    /// <param name="cancellationToken">Cancellation token for stopping the test</param>
    private async Task ExecuteTestAsync(LoadTestRequest request, string testId, CancellationToken cancellationToken)
    {
        var startTime = Stopwatch.GetTimestamp();
        long totalRequestsTarget = request.Requests ?? 0;
        var requestsPerUser = request.Requests.HasValue ? request.Requests.Value / request.Users : 0;

        try
        {
            using var httpClient = GenerateHttpClient(request.Insecure);
            var tasks = new List<Task>();

            if (request.Duration.HasValue)
            {
                var stopTime = DateTime.Now.AddSeconds(request.Duration.Value);

                for (var i = 0; i < request.Users; i++)
                {
                    tasks.Add(RunUserForDurationAsync(httpClient, request, testId, stopTime, cancellationToken));
                }
            }
            else
            {
                for (var i = 0; i < request.Users; i++)
                {
                    tasks.Add(RunUserForRequestsAsync(httpClient, request, testId, requestsPerUser, cancellationToken));
                }
            }

            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"Test {testId} was cancelled");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Test {testId} failed with error: {ex.Message}");
            await _executionService.FailExecutionAsync(testId, ex.Message);
            await _hubContext.Clients.All.SendAsync("TestError", new SignalRTestErrorMessage { TestId = testId, Error = ex.Message }, CancellationToken.None);
        }
        finally
        {
            try
            {
                var totalElapsedTime = Stopwatch.GetElapsedTime(startTime).TotalMilliseconds;
                Console.WriteLine($"[Finally] Calculating final results for {testId}...");
                Console.Out.Flush();

                var result = CalculateFinalResults(testId, totalElapsedTime);

                Console.WriteLine($"[Finally] Test {testId} completed. Total requests: {result.TotalRequests}");
                Console.Out.Flush();

                try
                {
                    Console.WriteLine($"[Finally] Saving to database...");
                    Console.Out.Flush();
                    await _executionService.CompleteExecutionAsync(testId, result);
                    Console.WriteLine($"[Finally] Execution saved to database for {testId}");
                    Console.Out.Flush();
                }
                catch (Exception dbEx)
                {
                    Console.WriteLine($"[Finally] Error saving to database: {dbEx.Message}");
                    Console.WriteLine($"[Finally] Stack trace: {dbEx.StackTrace}");
                    Console.Out.Flush();
                }

                Console.WriteLine($"[Finally] Sending TestCompleted event for {testId}...");
                Console.Out.Flush();
                await _hubContext.Clients.All.SendAsync("TestCompleted", result, CancellationToken.None);
                Console.WriteLine($"[Finally] TestCompleted event sent for {testId}");
                Console.Out.Flush();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Finally] Error in finally block: {ex.Message}");
                Console.WriteLine($"[Finally] Stack trace: {ex.StackTrace}");
                Console.Out.Flush();
            }
            finally
            {
                _isRunning = false;
                Console.WriteLine($"[Finally] Test cleanup complete, _isRunning = false");
                Console.Out.Flush();
            }
        }
    }

    /// <summary>
    /// Runs a virtual user that makes requests until the specified stop time
    /// </summary>
    /// <param name="client">The HTTP client to use</param>
    /// <param name="request">The load test configuration</param>
    /// <param name="testId">The unique test identifier</param>
    /// <param name="stopTime">The time to stop making requests</param>
    /// <param name="cancellationToken">Cancellation token</param>
    private async Task RunUserForDurationAsync(HttpClient client, LoadTestRequest request, string testId, DateTime stopTime, CancellationToken cancellationToken)
    {
        while (DateTime.Now < stopTime && !cancellationToken.IsCancellationRequested)
        {
            await SendRequestAndNotifyAsync(client, request, testId, cancellationToken);
        }
    }

    /// <summary>
    /// Runs a virtual user that makes a specific number of requests
    /// </summary>
    /// <param name="client">The HTTP client to use</param>
    /// <param name="request">The load test configuration</param>
    /// <param name="testId">The unique test identifier</param>
    /// <param name="requestCount">The number of requests to make</param>
    /// <param name="cancellationToken">Cancellation token</param>
    private async Task RunUserForRequestsAsync(HttpClient client, LoadTestRequest request, string testId, int requestCount, CancellationToken cancellationToken)
    {
        for (int i = 0; i < requestCount && !cancellationToken.IsCancellationRequested; i++)
        {
            await SendRequestAndNotifyAsync(client, request, testId, cancellationToken);
        }
    }

    /// <summary>
    /// Sends a single HTTP request and broadcasts metrics via SignalR
    /// </summary>
    /// <param name="client">The HTTP client to use</param>
    /// <param name="request">The load test configuration</param>
    /// <param name="testId">The unique test identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    private async Task SendRequestAndNotifyAsync(HttpClient client, LoadTestRequest request, string testId, CancellationToken cancellationToken)
    {
        var startTime = Stopwatch.GetTimestamp();
        HttpStatusCode statusCode = HttpStatusCode.InternalServerError;
        bool isSuccess = false;

        try
        {
            using var httpRequest = GenerateHttpRequest(request);
            using var response = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            statusCode = response.StatusCode;
            isSuccess = response.IsSuccessStatusCode;
        }
        catch (TaskCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            statusCode = HttpStatusCode.ServiceUnavailable;
            isSuccess = false;
        }

        var elapsedMs = (long)(Stopwatch.GetElapsedTime(startTime).TotalMilliseconds);
        var responseData = new ResponseData(Guid.NewGuid(), elapsedMs, statusCode, DateTime.UtcNow);
        _responses.Add(responseData);

        var now = DateTime.UtcNow;
        _recentRequestTimes.Enqueue(now);

        var windowStart = now.AddSeconds(-RpsWindowSeconds);
        while (_recentRequestTimes.TryPeek(out var oldest) && oldest < windowStart)
        {
            _recentRequestTimes.TryDequeue(out _);
        }

        var allResponses = _responses.ToArray();
        var successCount = allResponses.Count(r => (int)r.StatusCode >= 200 && (int)r.StatusCode < 300);
        var failedCount = allResponses.Length - successCount;
        var avgResponseTime = allResponses.Length > 0 ? allResponses.Average(r => r.ElapsedTime) : 0;

        var currentRps = _recentRequestTimes.Count / RpsWindowSeconds;

        if (currentRps > _peakRps)
        {
            _peakRps = currentRps;
        }

        var metric = new RealTimeMetric
        {
            TestId = testId,
            Timestamp = DateTime.UtcNow,
            ResponseTime = elapsedMs,
            StatusCode = (int)statusCode,
            IsSuccess = isSuccess,
            TotalRequests = allResponses.Length,
            SuccessfulRequests = successCount,
            FailedRequests = failedCount,
            CurrentRps = currentRps,
            AverageResponseTime = avgResponseTime
        };

        _metricCounter++;
        if (_metricCounter % 10 == 0)
        {
            _ = _executionService.AddMetricSnapshotAsync(testId, metric);
        }

        await _hubContext.Clients.All.SendAsync("MetricReceived", metric, cancellationToken);
    }

    /// <summary>
    /// Calculates final test results from all collected response data
    /// </summary>
    /// <param name="testId">The unique test identifier</param>
    /// <param name="totalElapsedTime">The total elapsed time in milliseconds</param>
    /// <returns>The final load test results</returns>
    private LoadTestResult CalculateFinalResults(string testId, double totalElapsedTime)
    {
        var allResponses = _responses.ToArray();
        if (allResponses.Length == 0)
        {
            return new LoadTestResult { TestId = testId };
        }

        var values = allResponses.Select(r => r.ElapsedTime).ToList();
        var successCount = allResponses.Count(r => (int)r.StatusCode >= 200 && (int)r.StatusCode < 300);
        var failedCount = allResponses.Length - successCount;

        var statusCodeGroups = allResponses
            .GroupBy(r => (int)r.StatusCode)
            .ToDictionary(
                g => g.Key,
                g => new StatusCodeResult
                {
                    StatusCode = g.Key,
                    Count = g.Count(),
                    MinResponseTime = g.Min(r => r.ElapsedTime),
                    AvgResponseTime = g.Average(r => r.ElapsedTime),
                    MaxResponseTime = g.Max(r => r.ElapsedTime),
                    Percentile50 = CalculatePercentile(g.Select(r => r.ElapsedTime).ToList(), 50),
                    Percentile75 = CalculatePercentile(g.Select(r => r.ElapsedTime).ToList(), 75),
                    Percentile90 = CalculatePercentile(g.Select(r => r.ElapsedTime).ToList(), 90),
                    Percentile95 = CalculatePercentile(g.Select(r => r.ElapsedTime).ToList(), 95),
                    Percentile99 = CalculatePercentile(g.Select(r => r.ElapsedTime).ToList(), 99)
                }
            );

        return new LoadTestResult
        {
            TestId = testId,
            TotalRequests = allResponses.Length,
            SuccessfulRequests = successCount,
            FailedRequests = failedCount,
            RequestsPerSecond = _peakRps,
            AverageResponseTime = values.Average(),
            MinResponseTime = values.Min(),
            MaxResponseTime = values.Max(),
            Percentile50 = CalculatePercentile(values, 50),
            Percentile75 = CalculatePercentile(values, 75),
            Percentile90 = CalculatePercentile(values, 90),
            Percentile95 = CalculatePercentile(values, 95),
            Percentile99 = CalculatePercentile(values, 99),
            TotalElapsedTime = (long)totalElapsedTime,
            StatusCodes = statusCodeGroups
        };
    }

    /// <summary>
    /// Creates an HTTP client with optional SSL certificate validation bypass
    /// </summary>
    /// <param name="insecure">Whether to skip SSL certificate validation</param>
    /// <returns>The configured HTTP client</returns>
    private static HttpClient GenerateHttpClient(bool insecure)
    {
        if (insecure)
        {
            var handler = new HttpClientHandler
            {
                ClientCertificateOptions = ClientCertificateOption.Manual,
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true
            };
            return new HttpClient(handler);
        }

        return new HttpClient();
    }

    /// <summary>
    /// Creates an HTTP request message from the load test configuration
    /// </summary>
    /// <param name="request">The load test configuration</param>
    /// <returns>The configured HTTP request message</returns>
    private HttpRequestMessage GenerateHttpRequest(LoadTestRequest request)
    {
        var httpRequest = new HttpRequestMessage(GetMethod(request.Method), request.Url);

        if (!string.IsNullOrWhiteSpace(request.Body))
        {
            httpRequest.Content = new StringContent(request.Body, Encoding.UTF8, request.ContentType);
        }

        if (request.Headers != null)
        {
            foreach (var header in request.Headers)
            {
                httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        if (!string.IsNullOrEmpty(_authToken) && request.Authentication != null)
        {
            httpRequest.Headers.TryAddWithoutValidation(request.Authentication.HeaderName, _authToken);
        }

        return httpRequest;
    }

    /// <summary>
    /// Converts a string HTTP method to the corresponding HttpMethod object
    /// </summary>
    /// <param name="method">The HTTP method string (GET, POST, etc.)</param>
    /// <returns>The corresponding HttpMethod object</returns>
    private static HttpMethod GetMethod(string method)
    {
        return method.ToUpperInvariant() switch
        {
            "GET" => HttpMethod.Get,
            "POST" => HttpMethod.Post,
            "PUT" => HttpMethod.Put,
            "DELETE" => HttpMethod.Delete,
            "PATCH" => HttpMethod.Patch,
            "HEAD" => HttpMethod.Head,
            "OPTIONS" => HttpMethod.Options,
            _ => HttpMethod.Get
        };
    }

    /// <summary>
    /// Calculates a specific percentile from a list of response times
    /// </summary>
    /// <param name="values">The list of response times in milliseconds</param>
    /// <param name="percentile">The percentile to calculate (0-100)</param>
    /// <returns>The calculated percentile value</returns>
    private static double CalculatePercentile(List<long> values, int percentile)
    {
        if (values.Count == 0)
            return 0;

        values.Sort();

        if (percentile is < 0 or > 100)
        {
            return 0;
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
            return values[intIndex] + fraction * (values[intIndex + 1] - values[intIndex]);
        }
    }

    /// <summary>
    /// Disposes the resources used by this service
    /// </summary>
    public void Dispose()
    {
        _cancellationTokenSource?.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Internal record for storing individual response data
    /// </summary>
    /// <param name="Id">Unique identifier for the response</param>
    /// <param name="ElapsedTime">Response time in milliseconds</param>
    /// <param name="StatusCode">HTTP status code</param>
    /// <param name="Timestamp">When the response was received</param>
    private record ResponseData(Guid Id, long ElapsedTime, HttpStatusCode StatusCode, DateTime Timestamp);
}
