using Microsoft.Data.Sqlite;

namespace NFury.Web.Data;

/// <summary>
/// SQLite database access layer using raw ADO.NET for Native AOT compatibility
/// </summary>
/// <remarks>
/// This class manages the database connection and schema initialization.
/// Uses raw ADO.NET instead of EF Core to ensure full Native AOT compatibility.
/// </remarks>
public sealed class SqliteDatabase : IDisposable
{
    private readonly string _connectionString;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteDatabase"/> class
    /// </summary>
    /// <param name="databasePath">The path to the SQLite database file</param>
    public SqliteDatabase(string databasePath)
    {
        _connectionString = $"Data Source={databasePath}";
    }

    /// <summary>
    /// Creates and opens a new database connection
    /// </summary>
    /// <returns>An open SQLite connection</returns>
    /// <remarks>
    /// The caller is responsible for disposing the returned connection
    /// </remarks>
    public SqliteConnection CreateConnection()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    /// <summary>
    /// Initializes the database schema, creating tables and indexes if they don't exist
    /// </summary>
    /// <remarks>
    /// Creates the following tables: Projects, Endpoints, Executions, and MetricSnapshots.
    /// Also sets up foreign key relationships and performance indexes.
    /// </remarks>
    public void InitializeDatabase()
    {
        using var conn = CreateConnection();

        var createProjectsTable = """
            CREATE TABLE IF NOT EXISTS Projects (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Description TEXT,
                AuthUrl TEXT,
                AuthMethod TEXT,
                AuthContentType TEXT,
                AuthBody TEXT,
                AuthHeadersJson TEXT,
                AuthTokenPath TEXT,
                AuthHeaderName TEXT DEFAULT 'Authorization',
                AuthHeaderPrefix TEXT DEFAULT 'Bearer',
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_Projects_Name ON Projects(Name);
            CREATE INDEX IF NOT EXISTS IX_Projects_CreatedAt ON Projects(CreatedAt);
            """;

        var createEndpointsTable = """
            CREATE TABLE IF NOT EXISTS Endpoints (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ProjectId INTEGER NOT NULL,
                Name TEXT NOT NULL,
                Description TEXT,
                Url TEXT NOT NULL,
                Method TEXT NOT NULL DEFAULT 'GET',
                Users INTEGER NOT NULL DEFAULT 10,
                Requests INTEGER,
                Duration INTEGER,
                ContentType TEXT,
                Body TEXT,
                Insecure INTEGER NOT NULL DEFAULT 0,
                RequiresAuth INTEGER NOT NULL DEFAULT 0,
                HeadersJson TEXT,
                AuthenticationJson TEXT,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                FOREIGN KEY (ProjectId) REFERENCES Projects(Id) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS IX_Endpoints_ProjectId ON Endpoints(ProjectId);
            CREATE INDEX IF NOT EXISTS IX_Endpoints_Name ON Endpoints(Name);
            """;

        var createExecutionsTable = """
            CREATE TABLE IF NOT EXISTS Executions (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                TestId TEXT NOT NULL UNIQUE,
                EndpointId INTEGER,
                Url TEXT NOT NULL DEFAULT '',
                Method TEXT NOT NULL DEFAULT 'GET',
                Users INTEGER NOT NULL,
                TargetRequests INTEGER,
                TargetDuration INTEGER,
                StartedAt TEXT NOT NULL,
                CompletedAt TEXT,
                Status TEXT NOT NULL DEFAULT 'Running',
                TotalRequests INTEGER NOT NULL DEFAULT 0,
                SuccessfulRequests INTEGER NOT NULL DEFAULT 0,
                FailedRequests INTEGER NOT NULL DEFAULT 0,
                TotalElapsedTime REAL NOT NULL DEFAULT 0,
                RequestsPerSecond REAL NOT NULL DEFAULT 0,
                AverageResponseTime REAL NOT NULL DEFAULT 0,
                MinResponseTime REAL NOT NULL DEFAULT 0,
                MaxResponseTime REAL NOT NULL DEFAULT 0,
                Percentile50 REAL NOT NULL DEFAULT 0,
                Percentile75 REAL NOT NULL DEFAULT 0,
                Percentile90 REAL NOT NULL DEFAULT 0,
                Percentile95 REAL NOT NULL DEFAULT 0,
                Percentile99 REAL NOT NULL DEFAULT 0,
                StatusCodesJson TEXT,
                ErrorMessage TEXT,
                FOREIGN KEY (EndpointId) REFERENCES Endpoints(Id) ON DELETE SET NULL
            );
            CREATE INDEX IF NOT EXISTS IX_Executions_TestId ON Executions(TestId);
            CREATE INDEX IF NOT EXISTS IX_Executions_EndpointId ON Executions(EndpointId);
            CREATE INDEX IF NOT EXISTS IX_Executions_StartedAt ON Executions(StartedAt);
            CREATE INDEX IF NOT EXISTS IX_Executions_Status ON Executions(Status);
            """;

        var createMetricsTable = """
            CREATE TABLE IF NOT EXISTS MetricSnapshots (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ExecutionId INTEGER NOT NULL,
                Timestamp TEXT NOT NULL,
                TotalRequests INTEGER NOT NULL DEFAULT 0,
                FailedRequests INTEGER NOT NULL DEFAULT 0,
                ResponseTime REAL NOT NULL DEFAULT 0,
                AverageResponseTime REAL NOT NULL DEFAULT 0,
                CurrentRps REAL NOT NULL DEFAULT 0,
                StatusCode INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY (ExecutionId) REFERENCES Executions(Id) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS IX_MetricSnapshots_Timestamp ON MetricSnapshots(Timestamp);
            CREATE INDEX IF NOT EXISTS IX_MetricSnapshots_ExecutionId_Timestamp ON MetricSnapshots(ExecutionId, Timestamp);
            """;

        using var cmd = conn.CreateCommand();
        cmd.CommandText = createProjectsTable;
        cmd.ExecuteNonQuery();

        cmd.CommandText = createEndpointsTable;
        cmd.ExecuteNonQuery();

        cmd.CommandText = createExecutionsTable;
        cmd.ExecuteNonQuery();

        cmd.CommandText = createMetricsTable;
        cmd.ExecuteNonQuery();

        MigrateDatabase(conn);
    }

    /// <summary>
    /// Runs database migrations to update schema for existing databases
    /// </summary>
    /// <param name="conn">The database connection</param>
    private static void MigrateDatabase(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "PRAGMA table_info(Executions)";
        using var reader = cmd.ExecuteReader();

        var hasProjectId = false;
        var hasEndpointId = false;

        while (reader.Read())
        {
            var columnName = reader.GetString(1);
            if (columnName == "ProjectId")
                hasProjectId = true;
            if (columnName == "EndpointId")
                hasEndpointId = true;
        }
        reader.Close();

        if (hasProjectId && !hasEndpointId)
        {
            cmd.CommandText = "ALTER TABLE Executions ADD COLUMN EndpointId INTEGER REFERENCES Endpoints(Id) ON DELETE SET NULL";
            cmd.ExecuteNonQuery();
        }

        MigrateProjectsTable(conn);
        MigrateEndpointsTable(conn);
    }

    /// <summary>
    /// Migrates the Projects table to add authentication columns
    /// </summary>
    /// <param name="conn">The database connection</param>
    private static void MigrateProjectsTable(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA table_info(Projects)";
        using var reader = cmd.ExecuteReader();

        var hasAuthUrl = false;
        while (reader.Read())
        {
            var columnName = reader.GetString(1);
            if (columnName == "AuthUrl")
                hasAuthUrl = true;
        }
        reader.Close();

        if (!hasAuthUrl)
        {
            var authColumns = new[]
            {
                "ALTER TABLE Projects ADD COLUMN AuthUrl TEXT",
                "ALTER TABLE Projects ADD COLUMN AuthMethod TEXT",
                "ALTER TABLE Projects ADD COLUMN AuthContentType TEXT",
                "ALTER TABLE Projects ADD COLUMN AuthBody TEXT",
                "ALTER TABLE Projects ADD COLUMN AuthHeadersJson TEXT",
                "ALTER TABLE Projects ADD COLUMN AuthTokenPath TEXT",
                "ALTER TABLE Projects ADD COLUMN AuthHeaderName TEXT DEFAULT 'Authorization'",
                "ALTER TABLE Projects ADD COLUMN AuthHeaderPrefix TEXT DEFAULT 'Bearer'"
            };

            foreach (var sql in authColumns)
            {
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
        }
    }

    /// <summary>
    /// Migrates the Endpoints table to add the RequiresAuth column
    /// </summary>
    /// <param name="conn">The database connection</param>
    private static void MigrateEndpointsTable(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA table_info(Endpoints)";
        using var reader = cmd.ExecuteReader();

        var hasRequiresAuth = false;
        while (reader.Read())
        {
            var columnName = reader.GetString(1);
            if (columnName == "RequiresAuth")
                hasRequiresAuth = true;
        }
        reader.Close();

        if (!hasRequiresAuth)
        {
            cmd.CommandText = "ALTER TABLE Endpoints ADD COLUMN RequiresAuth INTEGER NOT NULL DEFAULT 0";
            cmd.ExecuteNonQuery();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
    }
}

#region Entities

/// <summary>
/// Represents a load testing project that groups multiple endpoints
/// </summary>
public class Project
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public string? AuthUrl { get; set; }
    public string? AuthMethod { get; set; }
    public string? AuthContentType { get; set; }
    public string? AuthBody { get; set; }
    public string? AuthHeadersJson { get; set; }
    public string? AuthTokenPath { get; set; }
    public string? AuthHeaderName { get; set; } = "Authorization";
    public string? AuthHeaderPrefix { get; set; } = "Bearer";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<TestEndpoint> Endpoints { get; set; } = [];
}

/// <summary>
/// Represents an endpoint within a project with its test configuration
/// </summary>
public class TestEndpoint
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Method { get; set; } = "GET";
    public int Users { get; set; } = 10;
    public int? Requests { get; set; }
    public int? Duration { get; set; }
    public string? ContentType { get; set; }
    public string? Body { get; set; }
    public bool Insecure { get; set; }
    public bool RequiresAuth { get; set; } // If true, uses project's auth endpoint before testing
    public string? HeadersJson { get; set; }
    public string? AuthenticationJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Project? Project { get; set; }
    public List<TestExecution> Executions { get; set; } = [];
}

/// <summary>
/// Represents a single execution of a load test
/// </summary>
public class TestExecution
{
    public int Id { get; set; }
    public string TestId { get; set; } = string.Empty;
    public int? EndpointId { get; set; }
    public TestEndpoint? Endpoint { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Method { get; set; } = "GET";
    public int Users { get; set; }
    public int? TargetRequests { get; set; }
    public int? TargetDuration { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = "Running";
    public long TotalRequests { get; set; }
    public long SuccessfulRequests { get; set; }
    public long FailedRequests { get; set; }
    public double TotalElapsedTime { get; set; }
    public double RequestsPerSecond { get; set; }
    public double AverageResponseTime { get; set; }
    public double MinResponseTime { get; set; }
    public double MaxResponseTime { get; set; }
    public double Percentile50 { get; set; }
    public double Percentile75 { get; set; }
    public double Percentile90 { get; set; }
    public double Percentile95 { get; set; }
    public double Percentile99 { get; set; }
    public string? StatusCodesJson { get; set; }
    public string? ErrorMessage { get; set; }

    public List<TestMetricSnapshot> Metrics { get; set; } = [];
}

/// <summary>
/// Stores a snapshot of metrics at a point in time during test execution
/// </summary>
public class TestMetricSnapshot
{
    public int Id { get; set; }
    public int ExecutionId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public long TotalRequests { get; set; }
    public long FailedRequests { get; set; }
    public double ResponseTime { get; set; }
    public double AverageResponseTime { get; set; }
    public double CurrentRps { get; set; }
    public int StatusCode { get; set; }
}

#endregion
