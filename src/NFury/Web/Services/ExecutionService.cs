using System.Text.Json;
using Microsoft.Data.Sqlite;
using NFury.Web.Data;

namespace NFury.Web.Services;

/// <summary>
/// Service for managing test executions in the database
/// </summary>
/// <remarks>
/// Handles CRUD operations for test executions, including creating, completing,
/// failing, and canceling tests. Uses raw ADO.NET with SQLite for Native AOT compatibility.
/// </remarks>
public class ExecutionService
{
    private readonly SqliteDatabase _database;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExecutionService"/> class
    /// </summary>
    /// <param name="database">The SQLite database instance</param>
    public ExecutionService(SqliteDatabase database)
    {
        _database = database;
    }

    /// <summary>
    /// Creates a new execution for an endpoint
    /// </summary>
    /// <param name="endpointId">The endpoint identifier</param>
    /// <param name="testId">The unique test identifier</param>
    /// <param name="usersOverride">Optional override for the number of concurrent users</param>
    /// <returns>The created test execution</returns>
    /// <exception cref="InvalidOperationException">Thrown when the endpoint is not found</exception>
    public async Task<TestExecution> CreateExecutionAsync(int endpointId, string testId, int? usersOverride = null)
    {
        using var conn = _database.CreateConnection();

        using var getCmd = conn.CreateCommand();
        getCmd.CommandText = "SELECT Url, Method, Users, Requests, Duration FROM Endpoints WHERE Id = @EndpointId";
        getCmd.Parameters.AddWithValue("@EndpointId", endpointId);

        using var reader = await getCmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException($"Endpoint {endpointId} not found");
        }

        var url = reader.GetString(0);
        var method = reader.GetString(1);
        var users = usersOverride ?? reader.GetInt32(2);
        var targetRequests = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3);
        var targetDuration = reader.IsDBNull(4) ? (int?)null : reader.GetInt32(4);
        reader.Close();

        var now = DateTime.UtcNow;

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Executions (TestId, EndpointId, Url, Method, Users, TargetRequests, TargetDuration, StartedAt, Status)
            VALUES (@TestId, @EndpointId, @Url, @Method, @Users, @TargetRequests, @TargetDuration, @StartedAt, @Status);
            SELECT last_insert_rowid();
            """;

        cmd.Parameters.AddWithValue("@TestId", testId);
        cmd.Parameters.AddWithValue("@EndpointId", endpointId);
        cmd.Parameters.AddWithValue("@Url", url);
        cmd.Parameters.AddWithValue("@Method", method);
        cmd.Parameters.AddWithValue("@Users", users);
        cmd.Parameters.AddWithValue("@TargetRequests", (object?)targetRequests ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@TargetDuration", (object?)targetDuration ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@StartedAt", now.ToString("O"));
        cmd.Parameters.AddWithValue("@Status", "Running");

        var id = Convert.ToInt32(await cmd.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);

        return new TestExecution
        {
            Id = id,
            TestId = testId,
            EndpointId = endpointId,
            Url = url,
            Method = method,
            Users = users,
            TargetRequests = targetRequests,
            TargetDuration = targetDuration,
            StartedAt = now,
            Status = "Running",
            Endpoint = new TestEndpoint
            {
                Id = endpointId,
                Url = url,
                Method = method,
                Users = users
            }
        };
    }

    /// <summary>
    /// Creates a new execution for an ad-hoc test (not linked to an endpoint)
    /// </summary>
    /// <param name="request">The load test request configuration</param>
    /// <param name="testId">The unique test identifier</param>
    /// <returns>The created test execution</returns>
    public async Task<TestExecution> CreateAdHocExecutionAsync(LoadTestRequest request, string testId)
    {
        using var conn = _database.CreateConnection();
        using var cmd = conn.CreateCommand();

        var now = DateTime.UtcNow;

        cmd.CommandText = """
            INSERT INTO Executions (TestId, EndpointId, Url, Method, Users, TargetRequests, TargetDuration, StartedAt, Status)
            VALUES (@TestId, NULL, @Url, @Method, @Users, @TargetRequests, @TargetDuration, @StartedAt, @Status);
            SELECT last_insert_rowid();
            """;

        cmd.Parameters.AddWithValue("@TestId", testId);
        cmd.Parameters.AddWithValue("@Url", request.Url);
        cmd.Parameters.AddWithValue("@Method", request.Method);
        cmd.Parameters.AddWithValue("@Users", request.Users);
        cmd.Parameters.AddWithValue("@TargetRequests", (object?)request.Requests ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@TargetDuration", (object?)request.Duration ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@StartedAt", now.ToString("O"));
        cmd.Parameters.AddWithValue("@Status", "Running");

        var id = Convert.ToInt32(await cmd.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);

        return new TestExecution
        {
            Id = id,
            TestId = testId,
            EndpointId = null,
            Url = request.Url,
            Method = request.Method,
            Users = request.Users,
            TargetRequests = request.Requests,
            TargetDuration = request.Duration,
            StartedAt = now,
            Status = "Running"
        };
    }

    /// <summary>
    /// Retrieves an execution by its test identifier
    /// </summary>
    /// <param name="testId">The unique test identifier</param>
    /// <returns>The execution if found, otherwise null</returns>
    public async Task<TestExecution?> GetExecutionByTestIdAsync(string testId)
    {
        using var conn = _database.CreateConnection();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = """
            SELECT e.Id, e.TestId, e.EndpointId, e.Url, e.Method, e.Users, e.TargetRequests, e.TargetDuration,
                   e.StartedAt, e.CompletedAt, e.Status, e.TotalRequests, e.SuccessfulRequests, e.FailedRequests,
                   e.TotalElapsedTime, e.RequestsPerSecond, e.AverageResponseTime, e.MinResponseTime,
                   e.MaxResponseTime, e.Percentile50, e.Percentile75, e.Percentile90, e.Percentile95,
                   e.Percentile99, e.StatusCodesJson, e.ErrorMessage
            FROM Executions e
            WHERE e.TestId = @TestId
            """;
        cmd.Parameters.AddWithValue("@TestId", testId);

        using var reader = await cmd.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            return MapExecution(reader);
        }

        return null;
    }

    /// <summary>
    /// Retrieves an execution by its database identifier including related endpoint and project
    /// </summary>
    /// <param name="id">The execution identifier</param>
    /// <returns>The execution with related data if found, otherwise null</returns>
    public async Task<TestExecution?> GetExecutionByIdAsync(int id)
    {
        using var conn = _database.CreateConnection();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = """
            SELECT e.Id, e.TestId, e.EndpointId, e.Url, e.Method, e.Users, e.TargetRequests, e.TargetDuration,
                   e.StartedAt, e.CompletedAt, e.Status, e.TotalRequests, e.SuccessfulRequests, e.FailedRequests,
                   e.TotalElapsedTime, e.RequestsPerSecond, e.AverageResponseTime, e.MinResponseTime,
                   e.MaxResponseTime, e.Percentile50, e.Percentile75, e.Percentile90, e.Percentile95,
                   e.Percentile99, e.StatusCodesJson, e.ErrorMessage,
                   ep.Id, ep.ProjectId, ep.Name, ep.Url, ep.Method, ep.Users,
                   p.Id, p.Name
            FROM Executions e
            LEFT JOIN Endpoints ep ON e.EndpointId = ep.Id
            LEFT JOIN Projects p ON ep.ProjectId = p.Id
            WHERE e.Id = @Id
            """;
        cmd.Parameters.AddWithValue("@Id", id);

        using var reader = await cmd.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            var execution = MapExecution(reader);

            if (!reader.IsDBNull(26))
            {
                execution.Endpoint = new TestEndpoint
                {
                    Id = reader.GetInt32(26),
                    ProjectId = reader.GetInt32(27),
                    Name = reader.GetString(28),
                    Url = reader.GetString(29),
                    Method = reader.GetString(30),
                    Users = reader.GetInt32(31)
                };

                if (!reader.IsDBNull(32))
                {
                    execution.Endpoint.Project = new Project
                    {
                        Id = reader.GetInt32(32),
                        Name = reader.GetString(33)
                    };
                }
            }

            return execution;
        }

        return null;
    }

    /// <summary>
    /// Retrieves an execution with all its metric snapshots
    /// </summary>
    /// <param name="id">The execution identifier</param>
    /// <returns>The execution with metrics if found, otherwise null</returns>
    public async Task<TestExecution?> GetExecutionWithMetricsAsync(int id)
    {
        var execution = await GetExecutionByIdAsync(id);
        if (execution == null)
            return null;

        using var conn = _database.CreateConnection();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = """
            SELECT Id, ExecutionId, Timestamp, TotalRequests, FailedRequests,
                   ResponseTime, AverageResponseTime, CurrentRps, StatusCode
            FROM MetricSnapshots
            WHERE ExecutionId = @ExecutionId
            ORDER BY Timestamp
            """;
        cmd.Parameters.AddWithValue("@ExecutionId", id);

        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            execution.Metrics.Add(new TestMetricSnapshot
            {
                Id = reader.GetInt32(0),
                ExecutionId = reader.GetInt32(1),
                Timestamp = DateTime.Parse(reader.GetString(2), System.Globalization.CultureInfo.InvariantCulture),
                TotalRequests = reader.GetInt64(3),
                FailedRequests = reader.GetInt64(4),
                ResponseTime = reader.GetDouble(5),
                AverageResponseTime = reader.GetDouble(6),
                CurrentRps = reader.GetDouble(7),
                StatusCode = reader.GetInt32(8)
            });
        }

        return execution;
    }

    /// <summary>
    /// Adds a metric snapshot for a running test
    /// </summary>
    /// <param name="testId">The unique test identifier</param>
    /// <param name="metric">The real-time metric to save</param>
    public async Task AddMetricSnapshotAsync(string testId, RealTimeMetric metric)
    {
        var execution = await GetExecutionByTestIdAsync(testId);
        if (execution == null)
            return;

        using var conn = _database.CreateConnection();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = """
            INSERT INTO MetricSnapshots (ExecutionId, Timestamp, TotalRequests, FailedRequests, ResponseTime, AverageResponseTime, CurrentRps, StatusCode)
            VALUES (@ExecutionId, @Timestamp, @TotalRequests, @FailedRequests, @ResponseTime, @AverageResponseTime, @CurrentRps, @StatusCode)
            """;

        cmd.Parameters.AddWithValue("@ExecutionId", execution.Id);
        cmd.Parameters.AddWithValue("@Timestamp", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("@TotalRequests", metric.TotalRequests);
        cmd.Parameters.AddWithValue("@FailedRequests", metric.FailedRequests);
        cmd.Parameters.AddWithValue("@ResponseTime", metric.ResponseTime);
        cmd.Parameters.AddWithValue("@AverageResponseTime", metric.AverageResponseTime);
        cmd.Parameters.AddWithValue("@CurrentRps", metric.CurrentRps);
        cmd.Parameters.AddWithValue("@StatusCode", metric.StatusCode);

        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Completes an execution with successful results
    /// </summary>
    /// <param name="testId">The unique test identifier</param>
    /// <param name="result">The final load test results</param>
    public async Task CompleteExecutionAsync(string testId, LoadTestResult result)
    {
        using var conn = _database.CreateConnection();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = """
            UPDATE Executions SET
                Status = @Status,
                CompletedAt = @CompletedAt,
                TotalRequests = @TotalRequests,
                SuccessfulRequests = @SuccessfulRequests,
                FailedRequests = @FailedRequests,
                TotalElapsedTime = @TotalElapsedTime,
                RequestsPerSecond = @RequestsPerSecond,
                AverageResponseTime = @AverageResponseTime,
                MinResponseTime = @MinResponseTime,
                MaxResponseTime = @MaxResponseTime,
                Percentile50 = @Percentile50,
                Percentile75 = @Percentile75,
                Percentile90 = @Percentile90,
                Percentile95 = @Percentile95,
                Percentile99 = @Percentile99,
                StatusCodesJson = @StatusCodesJson
            WHERE TestId = @TestId
            """;

        cmd.Parameters.AddWithValue("@TestId", testId);
        cmd.Parameters.AddWithValue("@Status", "Completed");
        cmd.Parameters.AddWithValue("@CompletedAt", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("@TotalRequests", result.TotalRequests);
        cmd.Parameters.AddWithValue("@SuccessfulRequests", result.SuccessfulRequests);
        cmd.Parameters.AddWithValue("@FailedRequests", result.FailedRequests);
        cmd.Parameters.AddWithValue("@TotalElapsedTime", result.TotalElapsedTime);
        cmd.Parameters.AddWithValue("@RequestsPerSecond", result.RequestsPerSecond);
        cmd.Parameters.AddWithValue("@AverageResponseTime", result.AverageResponseTime);
        cmd.Parameters.AddWithValue("@MinResponseTime", result.MinResponseTime);
        cmd.Parameters.AddWithValue("@MaxResponseTime", result.MaxResponseTime);
        cmd.Parameters.AddWithValue("@Percentile50", result.Percentile50);
        cmd.Parameters.AddWithValue("@Percentile75", result.Percentile75);
        cmd.Parameters.AddWithValue("@Percentile90", result.Percentile90);
        cmd.Parameters.AddWithValue("@Percentile95", result.Percentile95);
        cmd.Parameters.AddWithValue("@Percentile99", result.Percentile99);
        cmd.Parameters.AddWithValue("@StatusCodesJson", JsonSerializer.Serialize(result.StatusCodes, AppJsonContext.Default.DictionaryInt32StatusCodeResult));

        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Marks an execution as failed with an error message
    /// </summary>
    /// <param name="testId">The unique test identifier</param>
    /// <param name="errorMessage">The error message describing the failure</param>
    public async Task FailExecutionAsync(string testId, string errorMessage)
    {
        using var conn = _database.CreateConnection();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = """
            UPDATE Executions SET
                Status = @Status,
                CompletedAt = @CompletedAt,
                ErrorMessage = @ErrorMessage
            WHERE TestId = @TestId
            """;

        cmd.Parameters.AddWithValue("@TestId", testId);
        cmd.Parameters.AddWithValue("@Status", "Failed");
        cmd.Parameters.AddWithValue("@CompletedAt", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("@ErrorMessage", errorMessage);

        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Marks an execution as cancelled
    /// </summary>
    /// <param name="testId">The unique test identifier</param>
    public async Task CancelExecutionAsync(string testId)
    {
        using var conn = _database.CreateConnection();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = """
            UPDATE Executions SET
                Status = @Status,
                CompletedAt = @CompletedAt
            WHERE TestId = @TestId
            """;

        cmd.Parameters.AddWithValue("@TestId", testId);
        cmd.Parameters.AddWithValue("@Status", "Cancelled");
        cmd.Parameters.AddWithValue("@CompletedAt", DateTime.UtcNow.ToString("O"));

        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Retrieves the most recent test executions
    /// </summary>
    /// <param name="count">The maximum number of executions to return</param>
    /// <returns>A list of recent executions ordered by start time descending</returns>
    public async Task<List<TestExecution>> GetRecentExecutionsAsync(int count = 20)
    {
        using var conn = _database.CreateConnection();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = """
            SELECT e.Id, e.TestId, e.EndpointId, e.Url, e.Method, e.Users, e.TargetRequests, e.TargetDuration,
                   e.StartedAt, e.CompletedAt, e.Status, e.TotalRequests, e.SuccessfulRequests, e.FailedRequests,
                   e.TotalElapsedTime, e.RequestsPerSecond, e.AverageResponseTime, e.MinResponseTime,
                   e.MaxResponseTime, e.Percentile50, e.Percentile75, e.Percentile90, e.Percentile95,
                   e.Percentile99, e.StatusCodesJson, e.ErrorMessage,
                   ep.Id, ep.ProjectId, ep.Name, ep.Url, ep.Method, ep.Users,
                   p.Id, p.Name
            FROM Executions e
            LEFT JOIN Endpoints ep ON e.EndpointId = ep.Id
            LEFT JOIN Projects p ON ep.ProjectId = p.Id
            ORDER BY e.StartedAt DESC
            LIMIT @Count
            """;
        cmd.Parameters.AddWithValue("@Count", count);

        var executions = new List<TestExecution>();
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var execution = MapExecution(reader);

            if (!reader.IsDBNull(26))
            {
                execution.Endpoint = new TestEndpoint
                {
                    Id = reader.GetInt32(26),
                    ProjectId = reader.GetInt32(27),
                    Name = reader.GetString(28),
                    Url = reader.GetString(29),
                    Method = reader.GetString(30),
                    Users = reader.GetInt32(31)
                };

                if (!reader.IsDBNull(32))
                {
                    execution.Endpoint.Project = new Project
                    {
                        Id = reader.GetInt32(32),
                        Name = reader.GetString(33)
                    };
                }
            }

            executions.Add(execution);
        }

        return executions;
    }

    /// <summary>
    /// Searches executions with optional filters and pagination
    /// </summary>
    /// <param name="endpointId">Optional endpoint filter</param>
    /// <param name="projectId">Optional project filter</param>
    /// <param name="status">Optional status filter</param>
    /// <param name="from">Optional start date filter</param>
    /// <param name="to">Optional end date filter</param>
    /// <param name="page">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <returns>A list of matching executions</returns>
    public async Task<List<TestExecution>> SearchExecutionsAsync(
        int? endpointId = null,
        int? projectId = null,
        string? status = null,
        DateTime? from = null,
        DateTime? to = null,
        int page = 1,
        int pageSize = 20)
    {
        using var conn = _database.CreateConnection();
        using var cmd = conn.CreateCommand();

        var whereClause = new List<string>();

        if (endpointId.HasValue)
        {
            whereClause.Add("e.EndpointId = @EndpointId");
            cmd.Parameters.AddWithValue("@EndpointId", endpointId.Value);
        }

        if (projectId.HasValue)
        {
            whereClause.Add("ep.ProjectId = @ProjectId");
            cmd.Parameters.AddWithValue("@ProjectId", projectId.Value);
        }

        if (!string.IsNullOrEmpty(status))
        {
            whereClause.Add("e.Status = @Status");
            cmd.Parameters.AddWithValue("@Status", status);
        }

        if (from.HasValue)
        {
            whereClause.Add("e.StartedAt >= @From");
            cmd.Parameters.AddWithValue("@From", from.Value.ToString("O"));
        }

        if (to.HasValue)
        {
            whereClause.Add("e.StartedAt <= @To");
            cmd.Parameters.AddWithValue("@To", to.Value.ToString("O"));
        }

        var whereString = whereClause.Count > 0 ? "WHERE " + string.Join(" AND ", whereClause) : "";

        cmd.CommandText = $"""
            SELECT e.Id, e.TestId, e.EndpointId, e.Url, e.Method, e.Users, e.TargetRequests, e.TargetDuration,
                   e.StartedAt, e.CompletedAt, e.Status, e.TotalRequests, e.SuccessfulRequests, e.FailedRequests,
                   e.TotalElapsedTime, e.RequestsPerSecond, e.AverageResponseTime, e.MinResponseTime,
                   e.MaxResponseTime, e.Percentile50, e.Percentile75, e.Percentile90, e.Percentile95,
                   e.Percentile99, e.StatusCodesJson, e.ErrorMessage,
                   ep.Id, ep.ProjectId, ep.Name, ep.Url, ep.Method, ep.Users,
                   p.Id, p.Name
            FROM Executions e
            LEFT JOIN Endpoints ep ON e.EndpointId = ep.Id
            LEFT JOIN Projects p ON ep.ProjectId = p.Id
            {whereString}
            ORDER BY e.StartedAt DESC
            LIMIT @Limit OFFSET @Offset
            """;

        cmd.Parameters.AddWithValue("@Limit", pageSize);
        cmd.Parameters.AddWithValue("@Offset", (page - 1) * pageSize);

        var executions = new List<TestExecution>();
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var execution = MapExecution(reader);

            if (!reader.IsDBNull(26))
            {
                execution.Endpoint = new TestEndpoint
                {
                    Id = reader.GetInt32(26),
                    ProjectId = reader.GetInt32(27),
                    Name = reader.GetString(28),
                    Url = reader.GetString(29),
                    Method = reader.GetString(30),
                    Users = reader.GetInt32(31)
                };

                if (!reader.IsDBNull(32))
                {
                    execution.Endpoint.Project = new Project
                    {
                        Id = reader.GetInt32(32),
                        Name = reader.GetString(33)
                    };
                }
            }

            executions.Add(execution);
        }

        return executions;
    }

    /// <summary>
    /// Deletes an execution by its identifier
    /// </summary>
    /// <param name="id">The execution identifier</param>
    /// <returns>True if the execution was deleted, otherwise false</returns>
    public async Task<bool> DeleteExecutionAsync(int id)
    {
        using var conn = _database.CreateConnection();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "DELETE FROM Executions WHERE Id = @Id";
        cmd.Parameters.AddWithValue("@Id", id);

        var affected = await cmd.ExecuteNonQueryAsync();
        return affected > 0;
    }

    /// <summary>
    /// Gets aggregated statistics for executions
    /// </summary>
    /// <param name="projectId">Optional project filter</param>
    /// <param name="endpointId">Optional endpoint filter</param>
    /// <returns>Aggregated execution statistics</returns>
    public async Task<ExecutionStatistics> GetStatisticsAsync(int? projectId = null, int? endpointId = null)
    {
        using var conn = _database.CreateConnection();
        using var cmd = conn.CreateCommand();

        var whereClause = new List<string>();

        if (endpointId.HasValue)
        {
            whereClause.Add("e.EndpointId = @EndpointId");
            cmd.Parameters.AddWithValue("@EndpointId", endpointId.Value);
        }
        else if (projectId.HasValue)
        {
            whereClause.Add("ep.ProjectId = @ProjectId");
            cmd.Parameters.AddWithValue("@ProjectId", projectId.Value);
        }

        var whereString = whereClause.Count > 0 ? "WHERE " + string.Join(" AND ", whereClause) : "";

        cmd.CommandText = $"""
            SELECT 
                COUNT(*) as TotalExecutions,
                SUM(CASE WHEN e.Status = 'Completed' THEN 1 ELSE 0 END) as SuccessfulExecutions,
                SUM(CASE WHEN e.Status = 'Failed' THEN 1 ELSE 0 END) as FailedExecutions,
                SUM(CASE WHEN e.Status = 'Cancelled' THEN 1 ELSE 0 END) as CancelledExecutions,
                SUM(e.TotalRequests) as TotalRequests,
                AVG(CASE WHEN e.Status = 'Completed' THEN e.AverageResponseTime END) as AvgResponseTime,
                AVG(CASE WHEN e.Status = 'Completed' THEN e.RequestsPerSecond END) as AvgRps
            FROM Executions e
            LEFT JOIN Endpoints ep ON e.EndpointId = ep.Id
            {whereString}
            """;

        using var reader = await cmd.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            return new ExecutionStatistics
            {
                TotalExecutions = reader.GetInt32(0),
                SuccessfulExecutions = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                FailedExecutions = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                CancelledExecutions = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                TotalRequests = reader.IsDBNull(4) ? 0 : reader.GetInt64(4),
                AverageResponseTime = reader.IsDBNull(5) ? 0 : reader.GetDouble(5),
                AverageRps = reader.IsDBNull(6) ? 0 : reader.GetDouble(6)
            };
        }

        return new ExecutionStatistics();
    }

    /// <summary>
    /// Maps a database reader row to a TestExecution entity
    /// </summary>
    /// <remarks>
    /// Expected columns: Id, TestId, EndpointId, Url, Method, Users, TargetRequests, TargetDuration,
    /// StartedAt, CompletedAt, Status, TotalRequests, SuccessfulRequests, FailedRequests, TotalElapsedTime, 
    /// RequestsPerSecond, AverageResponseTime, MinResponseTime, MaxResponseTime, Percentile50-99, StatusCodesJson, ErrorMessage
    /// </remarks>
    /// <param name="reader">The data reader positioned at a row</param>
    /// <returns>The mapped TestExecution entity</returns>
    public static TestExecution MapExecution(SqliteDataReader reader)
    {
        return new TestExecution
        {
            Id = reader.GetInt32(0),
            TestId = reader.GetString(1),
            EndpointId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
            Url = reader.GetString(3),
            Method = reader.GetString(4),
            Users = reader.GetInt32(5),
            TargetRequests = reader.IsDBNull(6) ? null : reader.GetInt32(6),
            TargetDuration = reader.IsDBNull(7) ? null : reader.GetInt32(7),
            StartedAt = DateTime.Parse(reader.GetString(8), System.Globalization.CultureInfo.InvariantCulture),
            CompletedAt = reader.IsDBNull(9) ? null : DateTime.Parse(reader.GetString(9), System.Globalization.CultureInfo.InvariantCulture),
            Status = reader.GetString(10),
            TotalRequests = reader.GetInt64(11),
            SuccessfulRequests = reader.GetInt64(12),
            FailedRequests = reader.GetInt64(13),
            TotalElapsedTime = reader.GetDouble(14),
            RequestsPerSecond = reader.GetDouble(15),
            AverageResponseTime = reader.GetDouble(16),
            MinResponseTime = reader.GetDouble(17),
            MaxResponseTime = reader.GetDouble(18),
            Percentile50 = reader.GetDouble(19),
            Percentile75 = reader.GetDouble(20),
            Percentile90 = reader.GetDouble(21),
            Percentile95 = reader.GetDouble(22),
            Percentile99 = reader.GetDouble(23),
            StatusCodesJson = reader.IsDBNull(24) ? null : reader.GetString(24),
            ErrorMessage = reader.IsDBNull(25) ? null : reader.GetString(25)
        };
    }
}

/// <summary>
/// Aggregated statistics for test executions
/// </summary>
public class ExecutionStatistics
{
    /// <summary>
    /// Total number of executions
    /// </summary>
    public int TotalExecutions { get; set; }

    /// <summary>
    /// Number of successfully completed executions
    /// </summary>
    public int SuccessfulExecutions { get; set; }

    /// <summary>
    /// Number of failed executions
    /// </summary>
    public int FailedExecutions { get; set; }

    /// <summary>
    /// Number of cancelled executions
    /// </summary>
    public int CancelledExecutions { get; set; }

    /// <summary>
    /// Total number of HTTP requests across all executions
    /// </summary>
    public long TotalRequests { get; set; }

    /// <summary>
    /// Average response time across successful executions
    /// </summary>
    public double AverageResponseTime { get; set; }

    /// <summary>
    /// Average requests per second across successful executions
    /// </summary>
    public double AverageRps { get; set; }
}
