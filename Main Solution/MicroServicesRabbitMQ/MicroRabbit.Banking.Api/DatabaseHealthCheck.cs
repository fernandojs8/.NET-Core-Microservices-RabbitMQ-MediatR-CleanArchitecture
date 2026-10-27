using MicroRabbit.Banking.Data.Context;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Threading;
using System.Threading.Tasks;
namespace MicroRabbit.Banking.Api;
public sealed class DatabaseHealthCheck(BankingDbContext context) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext check, CancellationToken cancellationToken = default)
        => await context.Database.CanConnectAsync(cancellationToken) ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("Database unavailable");
}
