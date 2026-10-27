using MicroRabbit.Banking.Data.Context;
using MicroRabbit.Banking.Domain.Events;
using MicroServicesRabbitMQ.Domain.Core.Bus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
namespace MicroRabbit.Banking.Data.Outbox;
public sealed class OutboxPublisher(IServiceScopeFactory scopes, ILogger<OutboxPublisher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<BankingDbContext>();
                var bus = scope.ServiceProvider.GetRequiredService<IEventBus>();
                var messages = await context.OutboxMessages.Where(m => m.PublishedAt == null).OrderBy(m => m.CreatedAt).Take(50).ToListAsync(stoppingToken);
                foreach (var message in messages)
                {
                    var transfer = JsonConvert.DeserializeObject<TransferCreatedEvent>(message.Payload) ?? throw new InvalidOperationException("Invalid outbox payload");
                    bus.Publish(transfer); // Returns only after broker confirmation.
                    message.PublishedAt = DateTime.UtcNow;
                    await context.SaveChangesAsync(stoppingToken);
                    logger.LogInformation("Published transfer event {EventId}", message.Id);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Outbox publication failed; pending events will be retried"); }
            try { await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
