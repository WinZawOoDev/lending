using Microsoft.Extensions.Diagnostics.HealthChecks;
using loans_service.Data;

namespace loans_service.Health;

public class PostgresHealthCheck(LoansDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await dbContext.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("Postgres is reachable.")
                : HealthCheckResult.Unhealthy("Postgres is unreachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Postgres health check failed.", ex);
        }
    }
}
