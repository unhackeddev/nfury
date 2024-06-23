namespace NFury.Commands.Run;

internal sealed record Request(string Url, string Method, string? Body, string? ContentType, int NumberOfRequests)
{
}
