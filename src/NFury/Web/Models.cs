namespace NFury.Web;

/// <summary>
/// Represents a request to execute a load test against an API endpoint
/// </summary>
public record LoadTestRequest
{
    /// <summary>
    /// The target URL to test
    /// </summary>
    public string Url { get; init; } = string.Empty;

    /// <summary>
    /// The HTTP method to use (GET, POST, PUT, DELETE, etc.)
    /// </summary>
    public string Method { get; init; } = "GET";

    /// <summary>
    /// Number of concurrent virtual users to simulate
    /// </summary>
    public int Users { get; init; } = 10;

    /// <summary>
    /// Total number of requests to make (mutually exclusive with Duration)
    /// </summary>
    public int? Requests { get; init; }

    /// <summary>
    /// Duration in seconds to run the test (mutually exclusive with Requests)
    /// </summary>
    public int? Duration { get; init; }

    /// <summary>
    /// Optional request body for POST/PUT requests
    /// </summary>
    public string? Body { get; init; }

    /// <summary>
    /// Content type of the request body
    /// </summary>
    public string ContentType { get; init; } = "application/json";

    /// <summary>
    /// Whether to skip SSL certificate validation
    /// </summary>
    public bool Insecure { get; init; }

    /// <summary>
    /// Optional custom headers to include in requests
    /// </summary>
    public Dictionary<string, string>? Headers { get; init; }

    /// <summary>
    /// Optional authentication configuration for obtaining tokens
    /// </summary>
    public AuthenticationConfig? Authentication { get; init; }
}

/// <summary>
/// Configuration for authenticating before running load tests
/// </summary>
public record AuthenticationConfig
{
    /// <summary>
    /// The authentication endpoint URL
    /// </summary>
    public string Url { get; init; } = string.Empty;

    /// <summary>
    /// The HTTP method for authentication (usually POST)
    /// </summary>
    public string Method { get; init; } = "POST";

    /// <summary>
    /// The request body for authentication (credentials, etc.)
    /// </summary>
    public string? Body { get; init; }

    /// <summary>
    /// Content type of the authentication request
    /// </summary>
    public string ContentType { get; init; } = "application/json";

    /// <summary>
    /// JSON path to extract the token from the response (e.g., "access_token" or "data.token")
    /// </summary>
    public string TokenPath { get; init; } = "access_token";

    /// <summary>
    /// Name of the header to inject the token into
    /// </summary>
    public string HeaderName { get; init; } = "Authorization";

    /// <summary>
    /// Prefix to add before the token (e.g., "Bearer ")
    /// </summary>
    public string HeaderPrefix { get; init; } = "Bearer ";

    /// <summary>
    /// Optional custom headers for the authentication request
    /// </summary>
    public Dictionary<string, string>? Headers { get; init; }
}

/// <summary>
/// Result of an authentication attempt
/// </summary>
public record AuthenticationResult
{
    /// <summary>
    /// Whether authentication was successful
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The extracted authentication token (if successful)
    /// </summary>
    public string? Token { get; init; }

    /// <summary>
    /// Error message if authentication failed
    /// </summary>
    public string? Error { get; init; }
}

/// <summary>
/// Request to test authentication configuration
/// </summary>
public record AuthTestRequest
{
    /// <summary>
    /// The authentication configuration to test
    /// </summary>
    public AuthenticationConfig Config { get; init; } = new();

    /// <summary>
    /// Whether to skip SSL certificate validation
    /// </summary>
    public bool Insecure { get; init; }
}

/// <summary>
/// Final results of a completed load test
/// </summary>
public record LoadTestResult
{
    /// <summary>
    /// Unique identifier for the test
    /// </summary>
    public string TestId { get; init; } = string.Empty;

    /// <summary>
    /// Total number of requests made during the test
    /// </summary>
    public long TotalRequests { get; init; }

    /// <summary>
    /// Number of successful requests (2xx status codes)
    /// </summary>
    public long SuccessfulRequests { get; init; }

    /// <summary>
    /// Number of failed requests (non-2xx status codes or errors)
    /// </summary>
    public long FailedRequests { get; init; }

    /// <summary>
    /// Peak (maximum) requests per second achieved during the test
    /// </summary>
    public double RequestsPerSecond { get; init; }

    /// <summary>
    /// Average response time in milliseconds
    /// </summary>
    public double AverageResponseTime { get; init; }

    /// <summary>
    /// Minimum response time in milliseconds
    /// </summary>
    public double MinResponseTime { get; init; }

    /// <summary>
    /// Maximum response time in milliseconds
    /// </summary>
    public double MaxResponseTime { get; init; }

    /// <summary>
    /// 50th percentile (median) response time in milliseconds
    /// </summary>
    public double Percentile50 { get; init; }

    /// <summary>
    /// 75th percentile response time in milliseconds
    /// </summary>
    public double Percentile75 { get; init; }

    /// <summary>
    /// 90th percentile response time in milliseconds
    /// </summary>
    public double Percentile90 { get; init; }

    /// <summary>
    /// 95th percentile response time in milliseconds
    /// </summary>
    public double Percentile95 { get; init; }

    /// <summary>
    /// 99th percentile response time in milliseconds
    /// </summary>
    public double Percentile99 { get; init; }

    /// <summary>
    /// Total elapsed time for the test in milliseconds
    /// </summary>
    public long TotalElapsedTime { get; init; }

    /// <summary>
    /// Results grouped by HTTP status code
    /// </summary>
    public Dictionary<int, StatusCodeResult> StatusCodes { get; init; } = new();
}

/// <summary>
/// Statistics for a specific HTTP status code
/// </summary>
public record StatusCodeResult
{
    /// <summary>
    /// The HTTP status code
    /// </summary>
    public int StatusCode { get; init; }

    /// <summary>
    /// Number of responses with this status code
    /// </summary>
    public int Count { get; init; }

    /// <summary>
    /// Minimum response time for this status code
    /// </summary>
    public double MinResponseTime { get; init; }

    /// <summary>
    /// Average response time for this status code
    /// </summary>
    public double AvgResponseTime { get; init; }

    /// <summary>
    /// Maximum response time for this status code
    /// </summary>
    public double MaxResponseTime { get; init; }

    /// <summary>
    /// 50th percentile response time for this status code
    /// </summary>
    public double Percentile50 { get; init; }

    /// <summary>
    /// 75th percentile response time for this status code
    /// </summary>
    public double Percentile75 { get; init; }

    /// <summary>
    /// 90th percentile response time for this status code
    /// </summary>
    public double Percentile90 { get; init; }

    /// <summary>
    /// 95th percentile response time for this status code
    /// </summary>
    public double Percentile95 { get; init; }

    /// <summary>
    /// 99th percentile response time for this status code
    /// </summary>
    public double Percentile99 { get; init; }
}

/// <summary>
/// Real-time metric update sent during test execution
/// </summary>
public record RealTimeMetric
{
    /// <summary>
    /// The test identifier
    /// </summary>
    public string TestId { get; init; } = string.Empty;

    /// <summary>
    /// Timestamp of the metric
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Response time of the latest request in milliseconds
    /// </summary>
    public long ResponseTime { get; init; }

    /// <summary>
    /// HTTP status code of the latest request
    /// </summary>
    public int StatusCode { get; init; }

    /// <summary>
    /// Whether the latest request was successful
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Total requests made so far
    /// </summary>
    public long TotalRequests { get; init; }

    /// <summary>
    /// Successful requests so far
    /// </summary>
    public long SuccessfulRequests { get; init; }

    /// <summary>
    /// Failed requests so far
    /// </summary>
    public long FailedRequests { get; init; }

    /// <summary>
    /// Current requests per second
    /// </summary>
    public double CurrentRps { get; init; }

    /// <summary>
    /// Current average response time
    /// </summary>
    public double AverageResponseTime { get; init; }
}

/// <summary>
/// Progress update sent during test execution
/// </summary>
public record TestProgressUpdate
{
    /// <summary>
    /// The test identifier
    /// </summary>
    public string TestId { get; init; } = string.Empty;

    /// <summary>
    /// Progress percentage (0-100)
    /// </summary>
    public double Progress { get; init; }

    /// <summary>
    /// Elapsed time in milliseconds
    /// </summary>
    public long ElapsedTime { get; init; }

    /// <summary>
    /// Total requests made so far
    /// </summary>
    public long TotalRequests { get; init; }

    /// <summary>
    /// Current requests per second
    /// </summary>
    public double RequestsPerSecond { get; init; }

    /// <summary>
    /// Current average response time
    /// </summary>
    public double AverageResponseTime { get; init; }

    /// <summary>
    /// Whether the test has completed
    /// </summary>
    public bool IsCompleted { get; init; }

    /// <summary>
    /// Error message if the test failed
    /// </summary>
    public string? Error { get; init; }
}

/// <summary>
/// Request wrapper for starting a test with optional project ID
/// </summary>
public record LoadTestStartRequest
{
    /// <summary>
    /// The load test configuration to execute
    /// </summary>
    public LoadTestRequest Request { get; init; } = new();

    /// <summary>
    /// Optional project ID to associate the test with
    /// </summary>
    public int? ProjectId { get; init; }
}

/// <summary>
/// SignalR message indicating a client has connected
/// </summary>
/// <remarks>
/// Used for Native AOT compatibility where runtime serialization is not available
/// </remarks>
public record SignalRConnectedMessage
{
    /// <summary>
    /// The SignalR connection identifier
    /// </summary>
    public string ConnectionId { get; init; } = string.Empty;
}

/// <summary>
/// SignalR message containing a test identifier
/// </summary>
/// <remarks>
/// Used for Native AOT compatibility where runtime serialization is not available
/// </remarks>
public record SignalRTestIdMessage
{
    /// <summary>
    /// The test identifier
    /// </summary>
    public string TestId { get; init; } = string.Empty;
}

/// <summary>
/// SignalR message indicating a test error occurred
/// </summary>
/// <remarks>
/// Used for Native AOT compatibility where runtime serialization is not available
/// </remarks>
public record SignalRTestErrorMessage
{
    /// <summary>
    /// The test identifier where the error occurred
    /// </summary>
    public string TestId { get; init; } = string.Empty;

    /// <summary>
    /// The error message describing what went wrong
    /// </summary>
    public string Error { get; init; } = string.Empty;
}
