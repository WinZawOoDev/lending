using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace loans_service.Health;

public class ElasticsearchHealthCheck(ElasticsearchClient client) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await client.PingAsync(cancellationToken);
            return response.IsSuccess()
                ? HealthCheckResult.Healthy("Elasticsearch is reachable.")
                : HealthCheckResult.Unhealthy(
                    $"Elasticsearch ping failed: {response.DebugInformation}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Elasticsearch health check failed.", ex);
        }
    }
}
