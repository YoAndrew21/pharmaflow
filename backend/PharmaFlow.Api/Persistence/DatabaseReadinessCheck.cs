using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace PharmaFlow.Api.Persistence;

public sealed class DatabaseReadinessCheck(PharmaFlowDbContext context) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext healthContext, CancellationToken cancellationToken = default)
    {
        try
        {
            return await context.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy();
        }
        catch
        {
            // Neither health responses nor health-result logging receive exception details.
            return HealthCheckResult.Unhealthy();
        }
    }
}
