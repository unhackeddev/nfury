using System.Text.Json.Serialization;
using NFury.Commands.Server;
using NFury.Web.Data;
using NFury.Web.Services;

namespace NFury.Web;

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
[JsonSerializable(typeof(TestIdResponse))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(IsRunningResponse))]
[JsonSerializable(typeof(ExecutionListResponse))]
[JsonSerializable(typeof(Project))]
[JsonSerializable(typeof(TestEndpoint))]
[JsonSerializable(typeof(TestExecution))]
[JsonSerializable(typeof(TestMetricSnapshot))]
[JsonSerializable(typeof(ProjectDto))]
[JsonSerializable(typeof(ProjectAuthDto))]
[JsonSerializable(typeof(EndpointDto))]
[JsonSerializable(typeof(EndpointTestStartRequest))]
[JsonSerializable(typeof(ExecutionStatistics))]
[JsonSerializable(typeof(ProjectExportDto))]
[JsonSerializable(typeof(ProjectExportData))]
[JsonSerializable(typeof(EndpointExportData))]
[JsonSerializable(typeof(ExecutionExportData))]
[JsonSerializable(typeof(ProjectImportResult))]
[JsonSerializable(typeof(List<EndpointExportData>))]
[JsonSerializable(typeof(List<ExecutionExportData>))]
[JsonSerializable(typeof(List<Project>))]
[JsonSerializable(typeof(List<TestEndpoint>))]
[JsonSerializable(typeof(List<TestExecution>))]
[JsonSerializable(typeof(List<TestMetricSnapshot>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(Dictionary<int, StatusCodeResult>))]
[JsonSerializable(typeof(SignalRConnectedMessage))]
[JsonSerializable(typeof(SignalRTestIdMessage))]
[JsonSerializable(typeof(SignalRTestErrorMessage))]
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
