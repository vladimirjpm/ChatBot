using Microsoft.Extensions.Diagnostics.HealthChecks;
using Qdrant.Client;

namespace ChatBot.Api.HealthChecks;

/// <summary>
/// Health-чек Qdrant: вызывает gRPC <c>HealthAsync</c> (~few ms),
/// возвращает Unhealthy если соединение не установлено или превышен таймаут.
///
/// .NET: реализует <see cref="IHealthCheck"/> — стандартный контракт ASP.NET Core
/// HealthChecks (Microsoft.Extensions.Diagnostics.HealthChecks).
/// Регистрируется через <c>AddHealthChecks().AddCheck&lt;T&gt;(name)</c>.
/// </summary>
public sealed class QdrantHealthCheck(QdrantClient client) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Жёсткий таймаут — health-чек не должен висеть. Railway/K8s ждут ответ ~5 сек.
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(3));

            var info = await client.HealthAsync(cts.Token);
            return HealthCheckResult.Healthy($"qdrant {info.Version}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("qdrant недоступен", ex);
        }
    }
}
