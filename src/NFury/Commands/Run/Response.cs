using System.Net;

namespace NFury.Commands.Run;

public sealed record Response(Guid Id, long ElapsedTime, HttpStatusCode StatusCode);