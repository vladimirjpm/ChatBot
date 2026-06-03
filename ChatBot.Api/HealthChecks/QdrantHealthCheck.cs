using Microsoft.Extensions.Diagnostics.HealthChecks;
using Qdrant.Client;

namespace ChatBot.Api.HealthChecks;

/// <summary>
/// Qdrant health check: calls gRPC <c>HealthAsync</c> (~few ms),
/// returns Unhealthy if the connection cannot be established or the timeout is exceeded.
///
/// .NET: implements <see cref="IHealthCheck"/> — the standard ASP.NET Core
/// HealthChecks contract (Microsoft.Extensions.Diagnostics.HealthChecks).
/// Registered via <c>AddHealthChecks().AddCheck&lt;T&gt;(name)</c>.
/// </summary>
public sealed class QdrantHealthCheck(QdrantClient client) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Hard timeout — health check must not hang. Railway/K8s expect a response within ~5 s.
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(3));

            var info = await client.HealthAsync(cts.Token);
            return HealthCheckResult.Healthy($"qdrant {info.Version}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("qdrant unreachable", ex);
        }
    }
}
