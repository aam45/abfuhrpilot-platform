using AbfuhrPilot.Application.Common;

namespace AbfuhrPilot.Api.Auth;

/// <summary>Reads the middleware-generated correlation id for the current request.</summary>
public class CorrelationIdProvider : ICorrelationIdProvider
{
    private readonly IHttpContextAccessor _accessor;
    public CorrelationIdProvider(IHttpContextAccessor accessor) => _accessor = accessor;

    public string? CorrelationId =>
        _accessor.HttpContext?.Items.TryGetValue("CorrelationId", out var v) == true ? v as string : null;
}
