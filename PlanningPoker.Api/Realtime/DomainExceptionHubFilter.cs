using Microsoft.AspNetCore.SignalR;
using PlanningPoker.Domain.Errors;

namespace PlanningPoker.Api.Realtime;

/// <summary>
/// Translates any thrown <see cref="DomainException"/> (invalid card index, spectator trying to
/// play, reveal attempted before everyone has picked, ...) into a client-visible
/// <see cref="HubException"/>. <see cref="HubException"/> messages always reach the client, even
/// without <c>EnableDetailedErrors</c>, so this is the one exception type worth surfacing verbatim.
/// </summary>
public sealed class DomainExceptionHubFilter : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        try
        {
            return await next(invocationContext);
        }
        catch (DomainException ex)
        {
            throw new HubException(ex.Message);
        }
    }
}
