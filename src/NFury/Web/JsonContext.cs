using System.Text.Json.Serialization;
using NFury.Commands.Server;
using NFury.Web.Data;
using NFury.Web.Services;

namespace NFury.Web;

// Response DTOs to replace anonymous types (required for Native AOT)
public record TestIdResponse(string TestId);
public record ErrorResponse(string Error);
public record IsRunningResponse(bool IsRunning);
public record ExecutionListResponse(List<TestExecution> Executions, int Total);

[JsonSerializable(typeof(LoadTestRequest))]
[JsonSerializable(typeof(AuthenticationConfig))]
[JsonSerializable(typeof(AuthenticationResult))]
[JsonSerializable(typeof(AuthTestRequest))]
[JsonSerializable(typeof(LoadTestResult))]
[JsonSerializable(typeof(StatusCodeResult))]
[JsonSerializable(typeof(RealTimeMetric))]
[JsonSerializable(typeof(TestProgressUpdate))]
// Response DTOs
[JsonSerializable(typeof(TestIdResponse))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(IsRunningResponse))]
[JsonSerializable(typeof(ExecutionListResponse))]
// Entity types
[JsonSerializable(typeof(Project))]
[JsonSerializable(typeof(TestEndpoint))]
[JsonSerializable(typeof(TestExecution))]
[JsonSerializable(typeof(TestMetricSnapshot))]
// DTOs
[JsonSerializable(typeof(ProjectDto))]
[JsonSerializable(typeof(ProjectAuthDto))]
[JsonSerializable(typeof(EndpointDto))]
[JsonSerializable(typeof(EndpointTestStartRequest))]
[JsonSerializable(typeof(ExecutionStatistics))]
// Export/Import DTOs
[JsonSerializable(typeof(ProjectExportDto))]
[JsonSerializable(typeof(ProjectExportData))]
[JsonSerializable(typeof(EndpointExportData))]
[JsonSerializable(typeof(ExecutionExportData))]
[JsonSerializable(typeof(ProjectImportResult))]
[JsonSerializable(typeof(List<EndpointExportData>))]
[JsonSerializable(typeof(List<ExecutionExportData>))]
// Lists
[JsonSerializable(typeof(List<Project>))]
[JsonSerializable(typeof(List<TestEndpoint>))]
[JsonSerializable(typeof(List<TestExecution>))]
[JsonSerializable(typeof(List<TestMetricSnapshot>))]
// Dictionaries
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(Dictionary<int, StatusCodeResult>))]
// SignalR message types
[JsonSerializable(typeof(SignalRConnectedMessage))]
[JsonSerializable(typeof(SignalRTestIdMessage))]
[JsonSerializable(typeof(SignalRTestErrorMessage))]
// Basic types
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(int))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
)]
public partial class AppJsonContext : JsonSerializerContext
{
}
