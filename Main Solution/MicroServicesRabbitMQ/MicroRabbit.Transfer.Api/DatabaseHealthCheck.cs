using MicroRabbit.Transfer.Data.Context;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Threading;
using System.Threading.Tasks;
namespace MicroRabbit.Transfer.Api;
public sealed class DatabaseHealthCheck(TransferDbContext context) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext check, CancellationToken cancellationToken = default)
        => await context.Database.CanConnectAsync(cancellationToken) ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("Database unavailable");
}
