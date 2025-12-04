using NFury.Web;
using NFury.Web.Data;
using NFury.Web.Hubs;
using NFury.Web.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace NFury.Commands.Server;

public class ServerCommand : AsyncCommand<ServerSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, ServerSettings settings)
    {
        AnsiConsole.MarkupLine("[bold blue]Starting NFury Web Server...[/]");

        var builder = WebApplication.CreateBuilder();

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ListenAnyIP(settings.Port);
        });

        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default);
        });

        var dbPath = Path.Combine(AppContext.BaseDirectory, "nfury.db");
        var database = new SqliteDatabase(dbPath);
        database.InitializeDatabase();
        builder.Services.AddSingleton(database);

        builder.Services.AddSignalR()
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default);
            });
        builder.Services.AddSingleton<ExecutionService>();
        builder.Services.AddSingleton<ProjectService>();
        builder.Services.AddSingleton<LoadTestService>();
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyHeader()
                      .AllowAnyMethod();
            });
        });

        var app = builder.Build();

        app.UseCors();
        app.UseDefaultFiles();
        app.UseStaticFiles();

        app.MapHub<LoadTestHub>("/hubs/loadtest");

        app.MapPost("/api/endpoints/{endpointId:int}/test/start", async (int endpointId, EndpointTestStartRequest? request, LoadTestService service) =>
        {
            try
            {
                var testId = await service.StartEndpointTestAsync(endpointId, request?.UsersOverride);
                return Results.Ok(new TestIdResponse(testId));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new ErrorResponse(ex.Message));
            }
        });

        app.MapPost("/api/test/start", async (LoadTestRequest request, LoadTestService service) =>
        {
            try
            {
                var testId = await service.StartAdHocTestAsync(request);
                return Results.Ok(new TestIdResponse(testId));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new ErrorResponse(ex.Message));
            }
        });

        app.MapPost("/api/test/stop", async (LoadTestService service) =>
        {
            await service.StopTestAsync();
            return Results.Ok();
        });

        app.MapGet("/api/test/status", (LoadTestService service) =>
        {
            return Results.Ok(new IsRunningResponse(service.IsRunning));
        });

        app.MapPost("/api/auth/test", async (AuthTestRequest request, LoadTestService service) =>
        {
            var result = await service.AuthenticateAsync(request.Config, request.Insecure);
            return Results.Ok(result);
        });

        app.MapGet("/api/projects", async (ProjectService service) =>
        {
            var projects = await service.GetAllProjectsAsync();
            return Results.Ok(projects);
        });

        app.MapGet("/api/projects/{id:int}", async (int id, ProjectService service) =>
        {
            var project = await service.GetProjectByIdAsync(id);
            return project != null ? Results.Ok(project) : Results.NotFound();
        });

        app.MapPost("/api/projects", async (ProjectDto dto, ProjectService service) =>
        {
            var project = await service.CreateProjectAsync(dto);
            return Results.Created($"/api/projects/{project.Id}", project);
        });

        app.MapPut("/api/projects/{id:int}", async (int id, ProjectDto dto, ProjectService service) =>
        {
            var project = await service.UpdateProjectAsync(id, dto);
            return project != null ? Results.Ok(project) : Results.NotFound();
        });

        app.MapPut("/api/projects/{id:int}/auth", async (int id, ProjectAuthDto dto, ProjectService service) =>
        {
            var project = await service.UpdateProjectAuthAsync(id, dto);
            return project != null ? Results.Ok(project) : Results.NotFound();
        });

        app.MapDelete("/api/projects/{id:int}/auth", async (int id, ProjectService service) =>
        {
            var result = await service.DeleteProjectAuthAsync(id);
            return result ? Results.Ok() : Results.NotFound();
        });

        app.MapDelete("/api/projects/{id:int}", async (int id, ProjectService service) =>
        {
            var result = await service.DeleteProjectAsync(id);
            return result ? Results.Ok() : Results.NotFound();
        });

        app.MapGet("/api/projects/{id:int}/export", async (int id, ProjectService service) =>
        {
            var exportData = await service.ExportProjectAsync(id);
            return exportData != null ? Results.Ok(exportData) : Results.NotFound();
        });

        app.MapPost("/api/projects/import", async (ProjectExportDto importData, ProjectService service) =>
        {
            var result = await service.ImportProjectAsync(importData);
            return result.Success
                ? Results.Created($"/api/projects/{result.ProjectId}", result)
                : Results.BadRequest(new ErrorResponse(result.Error ?? "Import failed"));
        });

        app.MapGet("/api/projects/{projectId:int}/endpoints", async (int projectId, ProjectService service) =>
        {
            var endpoints = await service.GetProjectEndpointsAsync(projectId);
            return Results.Ok(endpoints);
        });

        app.MapGet("/api/endpoints/{id:int}", async (int id, ProjectService service) =>
        {
            var endpoint = await service.GetEndpointByIdAsync(id);
            return endpoint != null ? Results.Ok(endpoint) : Results.NotFound();
        });

        app.MapPost("/api/projects/{projectId:int}/endpoints", async (int projectId, EndpointDto dto, ProjectService service) =>
        {
            var endpoint = await service.CreateEndpointAsync(projectId, dto);
            return Results.Created($"/api/endpoints/{endpoint.Id}", endpoint);
        });

        app.MapPut("/api/endpoints/{id:int}", async (int id, EndpointDto dto, ProjectService service) =>
        {
            var endpoint = await service.UpdateEndpointAsync(id, dto);
            return endpoint != null ? Results.Ok(endpoint) : Results.NotFound();
        });

        app.MapDelete("/api/endpoints/{id:int}", async (int id, ProjectService service) =>
        {
            var result = await service.DeleteEndpointAsync(id);
            return result ? Results.Ok() : Results.NotFound();
        });

        app.MapGet("/api/endpoints/{endpointId:int}/executions", async (int endpointId, int page, int pageSize, ProjectService service) =>
        {
            var executions = await service.GetEndpointExecutionsAsync(endpointId, page > 0 ? page : 1, pageSize > 0 ? pageSize : 20);
            var count = await service.GetEndpointExecutionCountAsync(endpointId);
            return Results.Ok(new ExecutionListResponse(executions, count));
        });

        app.MapGet("/api/executions", async (ExecutionService service) =>
        {
            var executions = await service.GetRecentExecutionsAsync();
            return Results.Ok(executions);
        });

        app.MapGet("/api/executions/{id:int}", async (int id, ExecutionService service) =>
        {
            var execution = await service.GetExecutionByIdAsync(id);
            return execution != null ? Results.Ok(execution) : Results.NotFound();
        });

        app.MapGet("/api/executions/{id:int}/metrics", async (int id, ExecutionService service) =>
        {
            var execution = await service.GetExecutionWithMetricsAsync(id);
            return execution != null ? Results.Ok(execution) : Results.NotFound();
        });

        app.MapDelete("/api/executions/{id:int}", async (int id, ExecutionService service) =>
        {
            var result = await service.DeleteExecutionAsync(id);
            return result ? Results.Ok() : Results.NotFound();
        });

        app.MapGet("/api/executions/statistics", async (int? projectId, int? endpointId, ExecutionService service) =>
        {
            var stats = await service.GetStatisticsAsync(projectId, endpointId);
            return Results.Ok(stats);
        });

        app.MapGet("/api/executions/search", async (
            int? endpointId,
            int? projectId,
            string? status,
            DateTime? from,
            DateTime? to,
            int page,
            int pageSize,
            ExecutionService service) =>
        {
            var executions = await service.SearchExecutionsAsync(
                endpointId, projectId, status, from, to,
                page > 0 ? page : 1, pageSize > 0 ? pageSize : 20);
            return Results.Ok(executions);
        });

        AnsiConsole.MarkupLine($"[bold green]NFury Web Server started![/]");
        AnsiConsole.MarkupLine($"[blue]Open your browser at:[/] [link]http://{settings.Host}:{settings.Port}[/]");
        AnsiConsole.MarkupLine($"[dim]Database:[/] {dbPath}");
        AnsiConsole.MarkupLine("[dim]Press Ctrl+C to stop the server[/]");

        await app.RunAsync();

        return 0;
    }
}

public class EndpointTestStartRequest
{
    public int? UsersOverride { get; set; }
}

