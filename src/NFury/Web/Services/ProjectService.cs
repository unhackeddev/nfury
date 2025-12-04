using System.Text.Json;
using Microsoft.Data.Sqlite;
using NFury.Web.Data;

namespace NFury.Web.Services;

/// <summary>
/// Service for managing projects and their endpoints in the database
/// </summary>
/// <remarks>
/// Handles CRUD operations for projects, endpoints, and their authentication configurations.
/// Uses raw ADO.NET with SQLite for Native AOT compatibility.
/// </remarks>
public class ProjectService
{
    private readonly SqliteDatabase _database;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectService"/> class
    /// </summary>
    /// <param name="database">The SQLite database instance</param>
    public ProjectService(SqliteDatabase database)
    {
        _database = database;
    }

    #region Projects

    /// <summary>
    /// Retrieves all projects with their endpoints
    /// </summary>
    /// <returns>A list of all projects ordered by last update date</returns>
    public async Task<List<Project>> GetAllProjectsAsync()
    {
        using var conn = _database.CreateConnection();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = """
            SELECT Id, Name, Description, AuthUrl, AuthMethod, AuthContentType, AuthBody, 
                   AuthHeadersJson, AuthTokenPath, AuthHeaderName, AuthHeaderPrefix,
                   CreatedAt, UpdatedAt
            FROM Projects
            ORDER BY UpdatedAt DESC
            """;

        var projects = new List<Project>();
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            projects.Add(MapProject(reader));
        }
        reader.Close();

        foreach (var project in projects)
        {
            project.Endpoints = await GetProjectEndpointsInternalAsync(conn, project.Id);
        }

        return projects;
    }

    /// <summary>
    /// Retrieves a project by its identifier including all endpoints and recent executions
    /// </summary>
    /// <param name="id">The project identifier</param>
    /// <returns>The project if found, otherwise null</returns>
    public async Task<Project?> GetProjectByIdAsync(int id)
    {
        using var conn = _database.CreateConnection();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = """
            SELECT Id, Name, Description, AuthUrl, AuthMethod, AuthContentType, AuthBody, 
                   AuthHeadersJson, AuthTokenPath, AuthHeaderName, AuthHeaderPrefix,
                   CreatedAt, UpdatedAt
            FROM Projects
            WHERE Id = @Id
            """;
        cmd.Parameters.AddWithValue("@Id", id);

        using var reader = await cmd.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            var project = MapProject(reader);
            reader.Close();

            project.Endpoints = await GetProjectEndpointsInternalAsync(conn, id, includeExecutions: true);
            return project;
        }

        return null;
    }

    /// <summary>
    /// Creates a new project
    /// </summary>
    /// <param name="dto">The project data transfer object</param>
    /// <returns>The created project with its assigned identifier</returns>
    public async Task<Project> CreateProjectAsync(ProjectDto dto)
    {
        using var conn = _database.CreateConnection();
        using var cmd = conn.CreateCommand();

        var now = DateTime.UtcNow;

        cmd.CommandText = """
            INSERT INTO Projects (Name, Description, CreatedAt, UpdatedAt)
            VALUES (@Name, @Description, @CreatedAt, @UpdatedAt);
            SELECT last_insert_rowid();
            """;

        cmd.Parameters.AddWithValue("@Name", dto.Name);
        cmd.Parameters.AddWithValue("@Description", (object?)dto.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CreatedAt", now.ToString("O"));
        cmd.Parameters.AddWithValue("@UpdatedAt", now.ToString("O"));

        var id = Convert.ToInt32(await cmd.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);

        return new Project
        {
            Id = id,
            Name = dto.Name,
            Description = dto.Description,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// Updates an existing project's basic information
    /// </summary>
    /// <param name="id">The project identifier</param>
    /// <param name="dto">The updated project data</param>
    /// <returns>The updated project if found, otherwise null</returns>
    public async Task<Project?> UpdateProjectAsync(int id, ProjectDto dto)
    {
        using var conn = _database.CreateConnection();
        using var cmd = conn.CreateCommand();

        var now = DateTime.UtcNow;

        cmd.CommandText = """
            UPDATE Projects SET
                Name = @Name,
                Description = @Description,
                UpdatedAt = @UpdatedAt
            WHERE Id = @Id
            """;

        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@Name", dto.Name);
        cmd.Parameters.AddWithValue("@Description", (object?)dto.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@UpdatedAt", now.ToString("O"));

        var affected = await cmd.ExecuteNonQueryAsync();

        if (affected == 0)
            return null;

        return await GetProjectByIdAsync(id);
    }

    /// <summary>
    /// Updates a project's authentication configuration
    /// </summary>
    /// <param name="id">The project identifier</param>
    /// <param name="dto">The authentication configuration</param>
    /// <returns>The updated project if found, otherwise null</returns>
    public async Task<Project?> UpdateProjectAuthAsync(int id, ProjectAuthDto dto)
    {
        using var conn = _database.CreateConnection();
        using var cmd = conn.CreateCommand();

        var now = DateTime.UtcNow;
        var headersJson = dto.AuthHeaders != null
            ? JsonSerializer.Serialize(dto.AuthHeaders, AppJsonContext.Default.DictionaryStringString)
            : null;

        cmd.CommandText = """
            UPDATE Projects SET
                AuthUrl = @AuthUrl,
                AuthMethod = @AuthMethod,
                AuthContentType = @AuthContentType,
                AuthBody = @AuthBody,
                AuthHeadersJson = @AuthHeadersJson,
                AuthTokenPath = @AuthTokenPath,
                AuthHeaderName = @AuthHeaderName,
                AuthHeaderPrefix = @AuthHeaderPrefix,
                UpdatedAt = @UpdatedAt
            WHERE Id = @Id
            """;

        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@AuthUrl", (object?)dto.AuthUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@AuthMethod", (object?)dto.AuthMethod ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@AuthContentType", (object?)dto.AuthContentType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@AuthBody", (object?)dto.AuthBody ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@AuthHeadersJson", (object?)headersJson ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@AuthTokenPath", (object?)dto.AuthTokenPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@AuthHeaderName", (object?)dto.AuthHeaderName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@AuthHeaderPrefix", (object?)dto.AuthHeaderPrefix ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@UpdatedAt", now.ToString("O"));

        var affected = await cmd.ExecuteNonQueryAsync();

        if (affected == 0)
            return null;

        return await GetProjectByIdAsync(id);
    }

    /// <summary>
    /// Deletes a project's authentication configuration
    /// </summary>
    /// <param name="id">The project identifier</param>
    /// <returns>True if the project was found and updated, otherwise false</returns>
    public async Task<bool> DeleteProjectAuthAsync(int id)
    {
        using var conn = _database.CreateConnection();
        using var cmd = conn.CreateCommand();

        var now = DateTime.UtcNow;

        cmd.CommandText = """
            UPDATE Projects SET
                AuthUrl = NULL,
                AuthMethod = NULL,
                AuthContentType = NULL,
                AuthBody = NULL,
                AuthHeadersJson = NULL,
                AuthTokenPath = NULL,
                AuthHeaderName = NULL,
                AuthHeaderPrefix = NULL,
                UpdatedAt = @UpdatedAt
            WHERE Id = @Id
            """;

        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@UpdatedAt", now.ToString("O"));

        var affected = await cmd.ExecuteNonQueryAsync();
        return affected > 0;
    }

    /// <summary>
    /// Deletes a project and all its associated endpoints
    /// </summary>
    /// <param name="id">The project identifier</param>
    /// <returns>True if the project was deleted, otherwise false</returns>
    public async Task<bool> DeleteProjectAsync(int id)
    {
        using var conn = _database.CreateConnection();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "DELETE FROM Projects WHERE Id = @Id";
        cmd.Parameters.AddWithValue("@Id", id);

        var affected = await cmd.ExecuteNonQueryAsync();
        return affected > 0;
    }

    #endregion

    #region Endpoints

    /// <summary>
    /// Retrieves all endpoints for a specific project
    /// </summary>
    /// <param name="projectId">The project identifier</param>
    /// <returns>A list of endpoints belonging to the project</returns>
    public async Task<List<TestEndpoint>> GetProjectEndpointsAsync(int projectId)
    {
        using var conn = _database.CreateConnection();
        return await GetProjectEndpointsInternalAsync(conn, projectId, includeExecutions: true);
    }

    /// <summary>
    /// Retrieves an endpoint by its identifier
    /// </summary>
    /// <param name="id">The endpoint identifier</param>
    /// <returns>The endpoint if found, otherwise null</returns>
    public async Task<TestEndpoint?> GetEndpointByIdAsync(int id)
    {
        using var conn = _database.CreateConnection();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = """
            SELECT Id, ProjectId, Name, Description, Url, Method, Users, Requests, Duration,
                   ContentType, Body, Insecure, RequiresAuth, HeadersJson, AuthenticationJson,
                   CreatedAt, UpdatedAt
            FROM Endpoints
            WHERE Id = @Id
            """;
        cmd.Parameters.AddWithValue("@Id", id);

        using var reader = await cmd.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            var endpoint = MapEndpoint(reader);
            reader.Close();

            endpoint.Executions = await GetEndpointExecutionsInternalAsync(conn, id, 10);
            return endpoint;
        }

        return null;
    }

    /// <summary>
    /// Creates a new endpoint for a project
    /// </summary>
    /// <param name="projectId">The project identifier</param>
    /// <param name="dto">The endpoint data transfer object</param>
    /// <returns>The created endpoint with its assigned identifier</returns>
    public async Task<TestEndpoint> CreateEndpointAsync(int projectId, EndpointDto dto)
    {
        using var conn = _database.CreateConnection();
        using var cmd = conn.CreateCommand();

        var now = DateTime.UtcNow;
        var headersJson = dto.Headers != null
            ? JsonSerializer.Serialize(dto.Headers, AppJsonContext.Default.DictionaryStringString)
            : null;
        var authJson = dto.Authentication != null
            ? JsonSerializer.Serialize(dto.Authentication, AppJsonContext.Default.AuthenticationConfig)
            : null;

        cmd.CommandText = """
            INSERT INTO Endpoints (ProjectId, Name, Description, Url, Method, Users, Requests, Duration,
                                   ContentType, Body, Insecure, RequiresAuth, HeadersJson, AuthenticationJson,
                                   CreatedAt, UpdatedAt)
            VALUES (@ProjectId, @Name, @Description, @Url, @Method, @Users, @Requests, @Duration,
                    @ContentType, @Body, @Insecure, @RequiresAuth, @HeadersJson, @AuthenticationJson,
                    @CreatedAt, @UpdatedAt);
            SELECT last_insert_rowid();
            """;

        cmd.Parameters.AddWithValue("@ProjectId", projectId);
        cmd.Parameters.AddWithValue("@Name", dto.Name);
        cmd.Parameters.AddWithValue("@Description", (object?)dto.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Url", dto.Url);
        cmd.Parameters.AddWithValue("@Method", dto.Method);
        cmd.Parameters.AddWithValue("@Users", dto.Users);
        cmd.Parameters.AddWithValue("@Requests", (object?)dto.Requests ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Duration", (object?)dto.Duration ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ContentType", (object?)dto.ContentType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Body", (object?)dto.Body ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Insecure", dto.Insecure ? 1 : 0);
        cmd.Parameters.AddWithValue("@RequiresAuth", dto.RequiresAuth ? 1 : 0);
        cmd.Parameters.AddWithValue("@HeadersJson", (object?)headersJson ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@AuthenticationJson", (object?)authJson ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CreatedAt", now.ToString("O"));
        cmd.Parameters.AddWithValue("@UpdatedAt", now.ToString("O"));

        var id = Convert.ToInt32(await cmd.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);

        await UpdateProjectTimestampAsync(conn, projectId);

        return new TestEndpoint
        {
            Id = id,
            ProjectId = projectId,
            Name = dto.Name,
            Description = dto.Description,
            Url = dto.Url,
            Method = dto.Method,
            Users = dto.Users,
            Requests = dto.Requests,
            Duration = dto.Duration,
            ContentType = dto.ContentType,
            Body = dto.Body,
            Insecure = dto.Insecure,
            RequiresAuth = dto.RequiresAuth,
            HeadersJson = headersJson,
            AuthenticationJson = authJson,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// Updates an existing endpoint
    /// </summary>
    /// <param name="id">The endpoint identifier</param>
    /// <param name="dto">The updated endpoint data</param>
    /// <returns>The updated endpoint if found, otherwise null</returns>
    public async Task<TestEndpoint?> UpdateEndpointAsync(int id, EndpointDto dto)
    {
        using var conn = _database.CreateConnection();
        using var cmd = conn.CreateCommand();

        var now = DateTime.UtcNow;
        var headersJson = dto.Headers != null
            ? JsonSerializer.Serialize(dto.Headers, AppJsonContext.Default.DictionaryStringString)
            : null;
        var authJson = dto.Authentication != null
            ? JsonSerializer.Serialize(dto.Authentication, AppJsonContext.Default.AuthenticationConfig)
            : null;

        cmd.CommandText = """
            UPDATE Endpoints SET
                Name = @Name,
                Description = @Description,
                Url = @Url,
                Method = @Method,
                Users = @Users,
                Requests = @Requests,
                Duration = @Duration,
                ContentType = @ContentType,
                Body = @Body,
                Insecure = @Insecure,
                RequiresAuth = @RequiresAuth,
                HeadersJson = @HeadersJson,
                AuthenticationJson = @AuthenticationJson,
                UpdatedAt = @UpdatedAt
            WHERE Id = @Id
            """;

        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@Name", dto.Name);
        cmd.Parameters.AddWithValue("@Description", (object?)dto.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Url", dto.Url);
        cmd.Parameters.AddWithValue("@Method", dto.Method);
        cmd.Parameters.AddWithValue("@Users", dto.Users);
        cmd.Parameters.AddWithValue("@Requests", (object?)dto.Requests ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Duration", (object?)dto.Duration ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ContentType", (object?)dto.ContentType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Body", (object?)dto.Body ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Insecure", dto.Insecure ? 1 : 0);
        cmd.Parameters.AddWithValue("@RequiresAuth", dto.RequiresAuth ? 1 : 0);
        cmd.Parameters.AddWithValue("@HeadersJson", (object?)headersJson ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@AuthenticationJson", (object?)authJson ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@UpdatedAt", now.ToString("O"));

        var affected = await cmd.ExecuteNonQueryAsync();

        if (affected == 0)
            return null;

        var endpoint = await GetEndpointByIdAsync(id);
        if (endpoint != null)
        {
            await UpdateProjectTimestampAsync(conn, endpoint.ProjectId);
        }

        return endpoint;
    }

    /// <summary>
    /// Deletes an endpoint
    /// </summary>
    /// <param name="id">The endpoint identifier</param>
    /// <returns>True if the endpoint was deleted, otherwise false</returns>
    public async Task<bool> DeleteEndpointAsync(int id)
    {
        using var conn = _database.CreateConnection();

        using var getCmd = conn.CreateCommand();
        getCmd.CommandText = "SELECT ProjectId FROM Endpoints WHERE Id = @Id";
        getCmd.Parameters.AddWithValue("@Id", id);
        var projectId = await getCmd.ExecuteScalarAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Endpoints WHERE Id = @Id";
        cmd.Parameters.AddWithValue("@Id", id);

        var affected = await cmd.ExecuteNonQueryAsync();

        if (affected > 0 && projectId != null)
        {
            await UpdateProjectTimestampAsync(conn, Convert.ToInt32(projectId, System.Globalization.CultureInfo.InvariantCulture));
        }

        return affected > 0;
    }

    /// <summary>
    /// Retrieves paginated executions for an endpoint
    /// </summary>
    /// <param name="endpointId">The endpoint identifier</param>
    /// <param name="page">The page number (1-based)</param>
    /// <param name="pageSize">The number of items per page</param>
    /// <returns>A list of test executions</returns>
    public async Task<List<TestExecution>> GetEndpointExecutionsAsync(int endpointId, int page = 1, int pageSize = 20)
    {
        using var conn = _database.CreateConnection();
        return await GetEndpointExecutionsInternalAsync(conn, endpointId, pageSize, (page - 1) * pageSize);
    }

    /// <summary>
    /// Gets the total number of executions for an endpoint
    /// </summary>
    /// <param name="endpointId">The endpoint identifier</param>
    /// <returns>The total execution count</returns>
    public async Task<int> GetEndpointExecutionCountAsync(int endpointId)
    {
        using var conn = _database.CreateConnection();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT COUNT(*) FROM Executions WHERE EndpointId = @EndpointId";
        cmd.Parameters.AddWithValue("@EndpointId", endpointId);

        return Convert.ToInt32(await cmd.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    #endregion

    #region Export/Import

    /// <summary>
    /// Exports a project with all its endpoints and execution history
    /// </summary>
    /// <param name="projectId">The project identifier</param>
    /// <returns>The export data if the project exists, otherwise null</returns>
    public async Task<ProjectExportDto?> ExportProjectAsync(int projectId)
    {
        using var conn = _database.CreateConnection();

        using var projCmd = conn.CreateCommand();
        projCmd.CommandText = """
            SELECT Id, Name, Description, AuthUrl, AuthMethod, AuthContentType, AuthBody,
                   AuthHeadersJson, AuthTokenPath, AuthHeaderName, AuthHeaderPrefix, CreatedAt, UpdatedAt
            FROM Projects WHERE Id = @Id
            """;
        projCmd.Parameters.AddWithValue("@Id", projectId);

        using var projReader = await projCmd.ExecuteReaderAsync();
        if (!await projReader.ReadAsync())
            return null;

        var project = MapProject(projReader);
        projReader.Close();

        var exportData = new ProjectExportDto
        {
            Version = "1.0",
            ExportedAt = DateTime.UtcNow,
            Project = new ProjectExportData
            {
                Name = project.Name,
                Description = project.Description,
                AuthUrl = project.AuthUrl,
                AuthMethod = project.AuthMethod,
                AuthContentType = project.AuthContentType,
                AuthBody = project.AuthBody,
                AuthHeaders = !string.IsNullOrEmpty(project.AuthHeadersJson)
                    ? JsonSerializer.Deserialize(project.AuthHeadersJson, AppJsonContext.Default.DictionaryStringString)
                    : null,
                AuthTokenPath = project.AuthTokenPath,
                AuthHeaderName = project.AuthHeaderName,
                AuthHeaderPrefix = project.AuthHeaderPrefix,
                Endpoints = []
            }
        };

        var endpoints = await GetProjectEndpointsInternalAsync(conn, projectId, includeExecutions: false);
        foreach (var endpoint in endpoints)
        {
            var endpointExport = new EndpointExportData
            {
                Name = endpoint.Name,
                Description = endpoint.Description,
                Url = endpoint.Url,
                Method = endpoint.Method,
                Users = endpoint.Users,
                Requests = endpoint.Requests,
                Duration = endpoint.Duration,
                ContentType = endpoint.ContentType,
                Body = endpoint.Body,
                Insecure = endpoint.Insecure,
                RequiresAuth = endpoint.RequiresAuth,
                Headers = !string.IsNullOrEmpty(endpoint.HeadersJson)
                    ? JsonSerializer.Deserialize(endpoint.HeadersJson, AppJsonContext.Default.DictionaryStringString)
                    : null,
                Authentication = !string.IsNullOrEmpty(endpoint.AuthenticationJson)
                    ? JsonSerializer.Deserialize(endpoint.AuthenticationJson, AppJsonContext.Default.AuthenticationConfig)
                    : null,
                Executions = []
            };

            var executions = await GetAllEndpointExecutionsAsync(conn, endpoint.Id);
            foreach (var exec in executions)
            {
                endpointExport.Executions.Add(new ExecutionExportData
                {
                    TestId = exec.TestId,
                    Url = exec.Url,
                    Method = exec.Method,
                    Users = exec.Users,
                    TargetRequests = exec.TargetRequests,
                    TargetDuration = exec.TargetDuration,
                    StartedAt = exec.StartedAt,
                    CompletedAt = exec.CompletedAt,
                    Status = exec.Status,
                    TotalRequests = exec.TotalRequests,
                    SuccessfulRequests = exec.SuccessfulRequests,
                    FailedRequests = exec.FailedRequests,
                    TotalElapsedTime = exec.TotalElapsedTime,
                    RequestsPerSecond = exec.RequestsPerSecond,
                    AverageResponseTime = exec.AverageResponseTime,
                    MinResponseTime = exec.MinResponseTime,
                    MaxResponseTime = exec.MaxResponseTime,
                    Percentile50 = exec.Percentile50,
                    Percentile75 = exec.Percentile75,
                    Percentile90 = exec.Percentile90,
                    Percentile95 = exec.Percentile95,
                    Percentile99 = exec.Percentile99,
                    StatusCodes = !string.IsNullOrEmpty(exec.StatusCodesJson)
                        ? JsonSerializer.Deserialize(exec.StatusCodesJson, AppJsonContext.Default.DictionaryInt32StatusCodeResult)
                        : null,
                    ErrorMessage = exec.ErrorMessage
                });
            }

            exportData.Project.Endpoints.Add(endpointExport);
        }

        return exportData;
    }

    /// <summary>
    /// Retrieves all executions for an endpoint without pagination
    /// </summary>
    /// <param name="conn">The database connection</param>
    /// <param name="endpointId">The endpoint identifier</param>
    /// <returns>A list of all test executions for the endpoint</returns>
    private static async Task<List<TestExecution>> GetAllEndpointExecutionsAsync(SqliteConnection conn, int endpointId)
    {
        using var cmd = conn.CreateCommand();

        cmd.CommandText = """
            SELECT Id, TestId, EndpointId, Url, Method, Users, TargetRequests, TargetDuration,
                   StartedAt, CompletedAt, Status, TotalRequests, SuccessfulRequests, FailedRequests,
                   TotalElapsedTime, RequestsPerSecond, AverageResponseTime, MinResponseTime,
                   MaxResponseTime, Percentile50, Percentile75, Percentile90, Percentile95,
                   Percentile99, StatusCodesJson, ErrorMessage
            FROM Executions
            WHERE EndpointId = @EndpointId
            ORDER BY StartedAt DESC
            """;

        cmd.Parameters.AddWithValue("@EndpointId", endpointId);

        var executions = new List<TestExecution>();
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            executions.Add(ExecutionService.MapExecution(reader));
        }

        return executions;
    }

    /// <summary>
    /// Imports a project from export data
    /// </summary>
    /// <param name="importData">The project export data to import</param>
    /// <returns>The import result containing the created project ID and counts</returns>
    public async Task<ProjectImportResult> ImportProjectAsync(ProjectExportDto importData)
    {
        try
        {
            using var conn = _database.CreateConnection();
            using var transaction = conn.BeginTransaction();

            var now = DateTime.UtcNow.ToString("O");
            var authHeadersJson = importData.Project.AuthHeaders != null
                ? JsonSerializer.Serialize(importData.Project.AuthHeaders, AppJsonContext.Default.DictionaryStringString)
                : null;

            using var projCmd = conn.CreateCommand();
            projCmd.CommandText = """
                INSERT INTO Projects (Name, Description, AuthUrl, AuthMethod, AuthContentType, AuthBody,
                                     AuthHeadersJson, AuthTokenPath, AuthHeaderName, AuthHeaderPrefix, CreatedAt, UpdatedAt)
                VALUES (@Name, @Description, @AuthUrl, @AuthMethod, @AuthContentType, @AuthBody,
                        @AuthHeadersJson, @AuthTokenPath, @AuthHeaderName, @AuthHeaderPrefix, @CreatedAt, @UpdatedAt);
                SELECT last_insert_rowid();
                """;

            projCmd.Parameters.AddWithValue("@Name", importData.Project.Name + " (Imported)");
            projCmd.Parameters.AddWithValue("@Description", (object?)importData.Project.Description ?? DBNull.Value);
            projCmd.Parameters.AddWithValue("@AuthUrl", (object?)importData.Project.AuthUrl ?? DBNull.Value);
            projCmd.Parameters.AddWithValue("@AuthMethod", (object?)importData.Project.AuthMethod ?? DBNull.Value);
            projCmd.Parameters.AddWithValue("@AuthContentType", (object?)importData.Project.AuthContentType ?? DBNull.Value);
            projCmd.Parameters.AddWithValue("@AuthBody", (object?)importData.Project.AuthBody ?? DBNull.Value);
            projCmd.Parameters.AddWithValue("@AuthHeadersJson", (object?)authHeadersJson ?? DBNull.Value);
            projCmd.Parameters.AddWithValue("@AuthTokenPath", (object?)importData.Project.AuthTokenPath ?? DBNull.Value);
            projCmd.Parameters.AddWithValue("@AuthHeaderName", (object?)importData.Project.AuthHeaderName ?? DBNull.Value);
            projCmd.Parameters.AddWithValue("@AuthHeaderPrefix", (object?)importData.Project.AuthHeaderPrefix ?? DBNull.Value);
            projCmd.Parameters.AddWithValue("@CreatedAt", now);
            projCmd.Parameters.AddWithValue("@UpdatedAt", now);

            var projectId = Convert.ToInt32(await projCmd.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
            var endpointsImported = 0;
            var executionsImported = 0;

            foreach (var endpointData in importData.Project.Endpoints)
            {
                var headersJson = endpointData.Headers != null
                    ? JsonSerializer.Serialize(endpointData.Headers, AppJsonContext.Default.DictionaryStringString)
                    : null;
                var authJson = endpointData.Authentication != null
                    ? JsonSerializer.Serialize(endpointData.Authentication, AppJsonContext.Default.AuthenticationConfig)
                    : null;

                using var endpCmd = conn.CreateCommand();
                endpCmd.CommandText = """
                    INSERT INTO Endpoints (ProjectId, Name, Description, Url, Method, Users, Requests, Duration,
                                          ContentType, Body, Insecure, RequiresAuth, HeadersJson, AuthenticationJson, CreatedAt, UpdatedAt)
                    VALUES (@ProjectId, @Name, @Description, @Url, @Method, @Users, @Requests, @Duration,
                            @ContentType, @Body, @Insecure, @RequiresAuth, @HeadersJson, @AuthenticationJson, @CreatedAt, @UpdatedAt);
                    SELECT last_insert_rowid();
                    """;

                endpCmd.Parameters.AddWithValue("@ProjectId", projectId);
                endpCmd.Parameters.AddWithValue("@Name", endpointData.Name);
                endpCmd.Parameters.AddWithValue("@Description", (object?)endpointData.Description ?? DBNull.Value);
                endpCmd.Parameters.AddWithValue("@Url", endpointData.Url);
                endpCmd.Parameters.AddWithValue("@Method", endpointData.Method);
                endpCmd.Parameters.AddWithValue("@Users", endpointData.Users);
                endpCmd.Parameters.AddWithValue("@Requests", (object?)endpointData.Requests ?? DBNull.Value);
                endpCmd.Parameters.AddWithValue("@Duration", (object?)endpointData.Duration ?? DBNull.Value);
                endpCmd.Parameters.AddWithValue("@ContentType", (object?)endpointData.ContentType ?? DBNull.Value);
                endpCmd.Parameters.AddWithValue("@Body", (object?)endpointData.Body ?? DBNull.Value);
                endpCmd.Parameters.AddWithValue("@Insecure", endpointData.Insecure ? 1 : 0);
                endpCmd.Parameters.AddWithValue("@RequiresAuth", endpointData.RequiresAuth ? 1 : 0);
                endpCmd.Parameters.AddWithValue("@HeadersJson", (object?)headersJson ?? DBNull.Value);
                endpCmd.Parameters.AddWithValue("@AuthenticationJson", (object?)authJson ?? DBNull.Value);
                endpCmd.Parameters.AddWithValue("@CreatedAt", now);
                endpCmd.Parameters.AddWithValue("@UpdatedAt", now);

                var endpointId = Convert.ToInt32(await endpCmd.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
                endpointsImported++;

                foreach (var execData in endpointData.Executions)
                {
                    var statusCodesJson = execData.StatusCodes != null
                        ? JsonSerializer.Serialize(execData.StatusCodes, AppJsonContext.Default.DictionaryInt32StatusCodeResult)
                        : null;

                    using var execCmd = conn.CreateCommand();
                    execCmd.CommandText = """
                        INSERT INTO Executions (TestId, EndpointId, Url, Method, Users, TargetRequests, TargetDuration,
                                               StartedAt, CompletedAt, Status, TotalRequests, SuccessfulRequests, FailedRequests,
                                               TotalElapsedTime, RequestsPerSecond, AverageResponseTime, MinResponseTime, MaxResponseTime,
                                               Percentile50, Percentile75, Percentile90, Percentile95, Percentile99, StatusCodesJson, ErrorMessage)
                        VALUES (@TestId, @EndpointId, @Url, @Method, @Users, @TargetRequests, @TargetDuration,
                                @StartedAt, @CompletedAt, @Status, @TotalRequests, @SuccessfulRequests, @FailedRequests,
                                @TotalElapsedTime, @RequestsPerSecond, @AverageResponseTime, @MinResponseTime, @MaxResponseTime,
                                @Percentile50, @Percentile75, @Percentile90, @Percentile95, @Percentile99, @StatusCodesJson, @ErrorMessage)
                        """;

                    execCmd.Parameters.AddWithValue("@TestId", $"imported-{Guid.NewGuid():N}");
                    execCmd.Parameters.AddWithValue("@EndpointId", endpointId);
                    execCmd.Parameters.AddWithValue("@Url", execData.Url);
                    execCmd.Parameters.AddWithValue("@Method", execData.Method);
                    execCmd.Parameters.AddWithValue("@Users", execData.Users);
                    execCmd.Parameters.AddWithValue("@TargetRequests", (object?)execData.TargetRequests ?? DBNull.Value);
                    execCmd.Parameters.AddWithValue("@TargetDuration", (object?)execData.TargetDuration ?? DBNull.Value);
                    execCmd.Parameters.AddWithValue("@StartedAt", execData.StartedAt.ToString("O"));
                    execCmd.Parameters.AddWithValue("@CompletedAt", execData.CompletedAt?.ToString("O") ?? (object)DBNull.Value);
                    execCmd.Parameters.AddWithValue("@Status", execData.Status);
                    execCmd.Parameters.AddWithValue("@TotalRequests", execData.TotalRequests);
                    execCmd.Parameters.AddWithValue("@SuccessfulRequests", execData.SuccessfulRequests);
                    execCmd.Parameters.AddWithValue("@FailedRequests", execData.FailedRequests);
                    execCmd.Parameters.AddWithValue("@TotalElapsedTime", execData.TotalElapsedTime);
                    execCmd.Parameters.AddWithValue("@RequestsPerSecond", execData.RequestsPerSecond);
                    execCmd.Parameters.AddWithValue("@AverageResponseTime", execData.AverageResponseTime);
                    execCmd.Parameters.AddWithValue("@MinResponseTime", execData.MinResponseTime);
                    execCmd.Parameters.AddWithValue("@MaxResponseTime", execData.MaxResponseTime);
                    execCmd.Parameters.AddWithValue("@Percentile50", execData.Percentile50);
                    execCmd.Parameters.AddWithValue("@Percentile75", execData.Percentile75);
                    execCmd.Parameters.AddWithValue("@Percentile90", execData.Percentile90);
                    execCmd.Parameters.AddWithValue("@Percentile95", execData.Percentile95);
                    execCmd.Parameters.AddWithValue("@Percentile99", execData.Percentile99);
                    execCmd.Parameters.AddWithValue("@StatusCodesJson", (object?)statusCodesJson ?? DBNull.Value);
                    execCmd.Parameters.AddWithValue("@ErrorMessage", (object?)execData.ErrorMessage ?? DBNull.Value);

                    await execCmd.ExecuteNonQueryAsync();
                    executionsImported++;
                }
            }

            transaction.Commit();

            return new ProjectImportResult
            {
                Success = true,
                ProjectId = projectId,
                ProjectName = importData.Project.Name + " (Imported)",
                EndpointsImported = endpointsImported,
                ExecutionsImported = executionsImported
            };
        }
        catch (Exception ex)
        {
            return new ProjectImportResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    #endregion

    #region Internal Helpers

    /// <summary>
    /// Retrieves all endpoints for a project using an existing connection
    /// </summary>
    /// <param name="conn">The database connection</param>
    /// <param name="projectId">The project identifier</param>
    /// <param name="includeExecutions">Whether to include recent executions</param>
    /// <returns>A list of endpoints</returns>
    private static async Task<List<TestEndpoint>> GetProjectEndpointsInternalAsync(SqliteConnection conn, int projectId, bool includeExecutions = false)
    {
        using var cmd = conn.CreateCommand();

        cmd.CommandText = """
            SELECT Id, ProjectId, Name, Description, Url, Method, Users, Requests, Duration,
                   ContentType, Body, Insecure, RequiresAuth, HeadersJson, AuthenticationJson,
                   CreatedAt, UpdatedAt
            FROM Endpoints
            WHERE ProjectId = @ProjectId
            ORDER BY Name ASC
            """;

        cmd.Parameters.AddWithValue("@ProjectId", projectId);

        var endpoints = new List<TestEndpoint>();
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            endpoints.Add(MapEndpoint(reader));
        }
        reader.Close();

        if (includeExecutions)
        {
            foreach (var endpoint in endpoints)
            {
                endpoint.Executions = await GetEndpointExecutionsInternalAsync(conn, endpoint.Id, 5);
            }
        }

        return endpoints;
    }

    /// <summary>
    /// Retrieves paginated executions for an endpoint using an existing connection
    /// </summary>
    /// <param name="conn">The database connection</param>
    /// <param name="endpointId">The endpoint identifier</param>
    /// <param name="limit">The maximum number of executions to return</param>
    /// <param name="offset">The number of executions to skip</param>
    /// <returns>A list of test executions</returns>
    private static async Task<List<TestExecution>> GetEndpointExecutionsInternalAsync(SqliteConnection conn, int endpointId, int limit, int offset = 0)
    {
        using var cmd = conn.CreateCommand();

        cmd.CommandText = """
            SELECT Id, TestId, EndpointId, Url, Method, Users, TargetRequests, TargetDuration,
                   StartedAt, CompletedAt, Status, TotalRequests, SuccessfulRequests, FailedRequests,
                   TotalElapsedTime, RequestsPerSecond, AverageResponseTime, MinResponseTime,
                   MaxResponseTime, Percentile50, Percentile75, Percentile90, Percentile95,
                   Percentile99, StatusCodesJson, ErrorMessage
            FROM Executions
            WHERE EndpointId = @EndpointId
            ORDER BY StartedAt DESC
            LIMIT @Limit OFFSET @Offset
            """;

        cmd.Parameters.AddWithValue("@EndpointId", endpointId);
        cmd.Parameters.AddWithValue("@Limit", limit);
        cmd.Parameters.AddWithValue("@Offset", offset);

        var executions = new List<TestExecution>();
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            executions.Add(ExecutionService.MapExecution(reader));
        }

        return executions;
    }

    /// <summary>
    /// Updates the project's UpdatedAt timestamp
    /// </summary>
    /// <param name="conn">The database connection</param>
    /// <param name="projectId">The project identifier</param>
    private static async Task UpdateProjectTimestampAsync(SqliteConnection conn, int projectId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Projects SET UpdatedAt = @UpdatedAt WHERE Id = @Id";
        cmd.Parameters.AddWithValue("@Id", projectId);
        cmd.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow.ToString("O"));
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Maps a database reader row to a Project entity
    /// </summary>
    /// <param name="reader">The data reader positioned at a row</param>
    /// <returns>The mapped Project entity</returns>
    private static Project MapProject(SqliteDataReader reader)
    {
        return new Project
        {
            Id = reader.GetInt32(0),
            Name = reader.GetString(1),
            Description = reader.IsDBNull(2) ? null : reader.GetString(2),
            AuthUrl = reader.IsDBNull(3) ? null : reader.GetString(3),
            AuthMethod = reader.IsDBNull(4) ? null : reader.GetString(4),
            AuthContentType = reader.IsDBNull(5) ? null : reader.GetString(5),
            AuthBody = reader.IsDBNull(6) ? null : reader.GetString(6),
            AuthHeadersJson = reader.IsDBNull(7) ? null : reader.GetString(7),
            AuthTokenPath = reader.IsDBNull(8) ? null : reader.GetString(8),
            AuthHeaderName = reader.IsDBNull(9) ? null : reader.GetString(9),
            AuthHeaderPrefix = reader.IsDBNull(10) ? null : reader.GetString(10),
            CreatedAt = DateTime.Parse(reader.GetString(11), System.Globalization.CultureInfo.InvariantCulture),
            UpdatedAt = DateTime.Parse(reader.GetString(12), System.Globalization.CultureInfo.InvariantCulture)
        };
    }

    /// <summary>
    /// Maps a database reader row to a TestEndpoint entity
    /// </summary>
    /// <param name="reader">The data reader positioned at a row</param>
    /// <returns>The mapped TestEndpoint entity</returns>
    private static TestEndpoint MapEndpoint(SqliteDataReader reader)
    {
        return new TestEndpoint
        {
            Id = reader.GetInt32(0),
            ProjectId = reader.GetInt32(1),
            Name = reader.GetString(2),
            Description = reader.IsDBNull(3) ? null : reader.GetString(3),
            Url = reader.GetString(4),
            Method = reader.GetString(5),
            Users = reader.GetInt32(6),
            Requests = reader.IsDBNull(7) ? null : reader.GetInt32(7),
            Duration = reader.IsDBNull(8) ? null : reader.GetInt32(8),
            ContentType = reader.IsDBNull(9) ? null : reader.GetString(9),
            Body = reader.IsDBNull(10) ? null : reader.GetString(10),
            Insecure = reader.GetInt32(11) == 1,
            RequiresAuth = reader.GetInt32(12) == 1,
            HeadersJson = reader.IsDBNull(13) ? null : reader.GetString(13),
            AuthenticationJson = reader.IsDBNull(14) ? null : reader.GetString(14),
            CreatedAt = DateTime.Parse(reader.GetString(15), System.Globalization.CultureInfo.InvariantCulture),
            UpdatedAt = DateTime.Parse(reader.GetString(16), System.Globalization.CultureInfo.InvariantCulture)
        };
    }

    #endregion
}

#region DTOs

/// <summary>
/// Data transfer object for creating or updating a project
/// </summary>
public class ProjectDto
{
    /// <summary>
    /// The project name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional project description
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// Data transfer object for updating project authentication configuration
/// </summary>
public class ProjectAuthDto
{
    /// <summary>
    /// The authentication endpoint URL
    /// </summary>
    public string? AuthUrl { get; set; }

    /// <summary>
    /// The HTTP method for authentication
    /// </summary>
    public string? AuthMethod { get; set; }

    /// <summary>
    /// Content type of the authentication request
    /// </summary>
    public string? AuthContentType { get; set; }

    /// <summary>
    /// The authentication request body
    /// </summary>
    public string? AuthBody { get; set; }

    /// <summary>
    /// Custom headers for the authentication request
    /// </summary>
    public Dictionary<string, string>? AuthHeaders { get; set; }

    /// <summary>
    /// JSON path to extract the token from the response
    /// </summary>
    public string? AuthTokenPath { get; set; }

    /// <summary>
    /// Name of the header to inject the token into
    /// </summary>
    public string? AuthHeaderName { get; set; }

    /// <summary>
    /// Prefix to add before the token
    /// </summary>
    public string? AuthHeaderPrefix { get; set; }
}

/// <summary>
/// Data transfer object for creating or updating an endpoint
/// </summary>
public class EndpointDto
{
    /// <summary>
    /// The endpoint name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional endpoint description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The target URL to test
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// The HTTP method to use
    /// </summary>
    public string Method { get; set; } = "GET";

    /// <summary>
    /// Number of concurrent virtual users
    /// </summary>
    public int Users { get; set; } = 10;

    /// <summary>
    /// Total number of requests to make
    /// </summary>
    public int? Requests { get; set; } = 100;

    /// <summary>
    /// Duration in seconds to run the test
    /// </summary>
    public int? Duration { get; set; }

    /// <summary>
    /// Content type of the request body
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// The request body
    /// </summary>
    public string? Body { get; set; }

    /// <summary>
    /// Whether to skip SSL certificate validation
    /// </summary>
    public bool Insecure { get; set; }

    /// <summary>
    /// Whether this endpoint requires authentication
    /// </summary>
    public bool RequiresAuth { get; set; }

    /// <summary>
    /// Custom headers for the request
    /// </summary>
    public Dictionary<string, string>? Headers { get; set; }

    /// <summary>
    /// Endpoint-specific authentication configuration (overrides project auth)
    /// </summary>
    public AuthenticationConfig? Authentication { get; set; }
}

/// <summary>
/// Data transfer object for project export
/// </summary>
public class ProjectExportDto
{
    /// <summary>
    /// Export format version
    /// </summary>
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// Timestamp when the export was created
    /// </summary>
    public DateTime ExportedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The exported project data
    /// </summary>
    public ProjectExportData Project { get; set; } = new();
}

/// <summary>
/// Exported project data including all configuration
/// </summary>
public class ProjectExportData
{
    /// <summary>
    /// The project name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional project description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The authentication endpoint URL
    /// </summary>
    public string? AuthUrl { get; set; }

    /// <summary>
    /// The HTTP method for authentication
    /// </summary>
    public string? AuthMethod { get; set; }

    /// <summary>
    /// Content type of the authentication request
    /// </summary>
    public string? AuthContentType { get; set; }

    /// <summary>
    /// The authentication request body
    /// </summary>
    public string? AuthBody { get; set; }

    /// <summary>
    /// Custom headers for the authentication request
    /// </summary>
    public Dictionary<string, string>? AuthHeaders { get; set; }

    /// <summary>
    /// JSON path to extract the token from the response
    /// </summary>
    public string? AuthTokenPath { get; set; }

    /// <summary>
    /// Name of the header to inject the token into
    /// </summary>
    public string? AuthHeaderName { get; set; }

    /// <summary>
    /// Prefix to add before the token
    /// </summary>
    public string? AuthHeaderPrefix { get; set; }

    /// <summary>
    /// List of exported endpoints with their execution history
    /// </summary>
    public List<EndpointExportData> Endpoints { get; set; } = [];
}

/// <summary>
/// Exported endpoint data including configuration and execution history
/// </summary>
public class EndpointExportData
{
    /// <summary>
    /// The endpoint name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional endpoint description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The target URL
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// The HTTP method
    /// </summary>
    public string Method { get; set; } = "GET";

    /// <summary>
    /// Number of concurrent virtual users
    /// </summary>
    public int Users { get; set; } = 10;

    /// <summary>
    /// Total number of requests
    /// </summary>
    public int? Requests { get; set; }

    /// <summary>
    /// Duration in seconds
    /// </summary>
    public int? Duration { get; set; }

    /// <summary>
    /// Content type of the request body
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// The request body
    /// </summary>
    public string? Body { get; set; }

    /// <summary>
    /// Whether to skip SSL certificate validation
    /// </summary>
    public bool Insecure { get; set; }

    /// <summary>
    /// Whether this endpoint requires authentication
    /// </summary>
    public bool RequiresAuth { get; set; }

    /// <summary>
    /// Custom headers for the request
    /// </summary>
    public Dictionary<string, string>? Headers { get; set; }

    /// <summary>
    /// Endpoint-specific authentication configuration
    /// </summary>
    public AuthenticationConfig? Authentication { get; set; }

    /// <summary>
    /// Historical execution data
    /// </summary>
    public List<ExecutionExportData> Executions { get; set; } = [];
}

/// <summary>
/// Exported execution data with full metrics
/// </summary>
public class ExecutionExportData
{
    /// <summary>
    /// Unique test identifier
    /// </summary>
    public string TestId { get; set; } = string.Empty;

    /// <summary>
    /// The target URL that was tested
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// The HTTP method used
    /// </summary>
    public string Method { get; set; } = "GET";

    /// <summary>
    /// Number of concurrent virtual users
    /// </summary>
    public int Users { get; set; }

    /// <summary>
    /// Target number of requests
    /// </summary>
    public int? TargetRequests { get; set; }

    /// <summary>
    /// Target duration in seconds
    /// </summary>
    public int? TargetDuration { get; set; }

    /// <summary>
    /// When the test started
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// When the test completed
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Final status of the test
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Total number of requests made
    /// </summary>
    public long TotalRequests { get; set; }

    /// <summary>
    /// Number of successful requests
    /// </summary>
    public long SuccessfulRequests { get; set; }

    /// <summary>
    /// Number of failed requests
    /// </summary>
    public long FailedRequests { get; set; }

    /// <summary>
    /// Total elapsed time in milliseconds
    /// </summary>
    public double TotalElapsedTime { get; set; }

    /// <summary>
    /// Average requests per second
    /// </summary>
    public double RequestsPerSecond { get; set; }

    /// <summary>
    /// Average response time in milliseconds
    /// </summary>
    public double AverageResponseTime { get; set; }

    /// <summary>
    /// Minimum response time in milliseconds
    /// </summary>
    public double MinResponseTime { get; set; }

    /// <summary>
    /// Maximum response time in milliseconds
    /// </summary>
    public double MaxResponseTime { get; set; }

    /// <summary>
    /// 50th percentile response time
    /// </summary>
    public double Percentile50 { get; set; }

    /// <summary>
    /// 75th percentile response time
    /// </summary>
    public double Percentile75 { get; set; }

    /// <summary>
    /// 90th percentile response time
    /// </summary>
    public double Percentile90 { get; set; }

    /// <summary>
    /// 95th percentile response time
    /// </summary>
    public double Percentile95 { get; set; }

    /// <summary>
    /// 99th percentile response time
    /// </summary>
    public double Percentile99 { get; set; }

    /// <summary>
    /// Results grouped by HTTP status code
    /// </summary>
    public Dictionary<int, StatusCodeResult>? StatusCodes { get; set; }

    /// <summary>
    /// Error message if the test failed
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Result of a project import operation
/// </summary>
public class ProjectImportResult
{
    /// <summary>
    /// Whether the import was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if the import failed
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// The ID of the imported project
    /// </summary>
    public int? ProjectId { get; set; }

    /// <summary>
    /// The name of the imported project
    /// </summary>
    public string? ProjectName { get; set; }

    /// <summary>
    /// Number of endpoints imported
    /// </summary>
    public int EndpointsImported { get; set; }

    /// <summary>
    /// Number of executions imported
    /// </summary>
    public int ExecutionsImported { get; set; }
}

#endregion
