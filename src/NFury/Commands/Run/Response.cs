using System.Net;

namespace NFury.Commands.Run;

internal sealed record Response(Guid Id, long ElapsedTime, HttpStatusCode StatusCode);
