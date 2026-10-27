using MicroServicesRabbitMQ.Domain.Core.Bus;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Threading;
using System.Threading.Tasks;
namespace MicroServicesRabbitMQ.Infrastructure.Bus;
public sealed class BrokerHealthCheck(IEventBus bus) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        => Task.FromResult(bus is RabbitMQBus rabbit && rabbit.IsReady
            ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("Consumer connection unavailable"));
}
