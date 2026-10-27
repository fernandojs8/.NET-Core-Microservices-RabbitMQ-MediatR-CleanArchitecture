using MicroRabbit.Banking.Data.Context;
using MicroRabbit.Banking.Data.Repository;
using MicroRabbit.Banking.Domain.Events;
using MicroRabbit.Banking.Domain.Models;
using MicroRabbit.Transfer.Data.Context;
using MicroRabbit.Transfer.Data.Repository;
using MicroRabbit.Transfer.Domain.Models;
using MicroServicesRabbitMQ.Domain.Core.Bus;
using MicroServicesRabbitMQ.Infrastructure.Bus;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Xunit;
namespace MicroRabbit.Tests;
public sealed class IntegrationFactAttribute : FactAttribute
{
    public IntegrationFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("RUN_INTEGRATION_TESTS") != "true") Skip = "Set RUN_INTEGRATION_TESTS=true and provide SQL/RabbitMQ";
    }
}
public class IntegrationTests
{
    private static string Connection(string database)
    {
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("TEST_SQL") ?? throw new InvalidOperationException("TEST_SQL required"));
        builder.InitialCatalog = database;
        return builder.ConnectionString;
    }
    private static DbContextOptions<BankingDbContext> BankingOptions(string database) => new DbContextOptionsBuilder<BankingDbContext>()
        .UseSqlServer(Connection(database), x => x.MigrationsAssembly("MicroRabbit.Banking.Api")).Options;
    private static DbContextOptions<TransferDbContext> TransferOptions(string database) => new DbContextOptionsBuilder<TransferDbContext>()
        .UseSqlServer(Connection(database), x => x.MigrationsAssembly("MicroRabbit.Transfer.Api")).Options;
    [IntegrationFact, Trait("Category", "Integration")]
    public async Task Sql_transaction_preserves_money_and_outbox_under_competing_debits()
    {
        var database = "MicroRabbitTest_" + Guid.NewGuid().ToString("N");
        await using var setup = new BankingDbContext(BankingOptions(database));
        try
        {
            await setup.Database.MigrateAsync();
            setup.Accounts.AddRange(new Account { AccountType = "Test", AccountBalance = 100 }, new Account { AccountType = "Test", AccountBalance = 0 });
            await setup.SaveChangesAsync();
            var accounts = await setup.Accounts.OrderBy(x => x.Id).ToListAsync();
            var from = accounts[0].Id; var to = accounts[1].Id;
            async Task<bool> Send()
            {
                await using var db = new BankingDbContext(BankingOptions(database));
                try { await new AccountRepository(db).TransferAsync(new TransferCreatedEvent(from, to, 80), default); return true; }
                catch (InvalidOperationException) { return false; }
            }
            var results = await Task.WhenAll(Send(), Send());
            Assert.Single(results, x => x);
            setup.ChangeTracker.Clear();
            Assert.Equal(20, (await setup.Accounts.FindAsync(from)).AccountBalance);
            Assert.Equal(80, (await setup.Accounts.FindAsync(to)).AccountBalance);
            Assert.Single(await setup.OutboxMessages.ToListAsync());
            await Assert.ThrowsAsync<KeyNotFoundException>(() => new AccountRepository(setup).TransferAsync(new TransferCreatedEvent(from, 999999, 1), default));
            Assert.Single(await setup.OutboxMessages.ToListAsync());
        }
        finally { await setup.Database.EnsureDeletedAsync(); }
    }
    [IntegrationFact, Trait("Category", "Integration")]
    public async Task Sql_duplicate_event_creates_one_log()
    {
        var database = "MicroRabbitTest_" + Guid.NewGuid().ToString("N");
        await using var setup = new TransferDbContext(TransferOptions(database));
        try
        {
            var migrator = setup.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>();
            await migrator.MigrateAsync("20220121180218_Initial Migration");
            await setup.Database.ExecuteSqlRawAsync("INSERT INTO TransferLogs (FromAccount, ToAccount, Amount) VALUES (1, 2, 1), (1, 2, 2)");
            await setup.Database.MigrateAsync();
            var historic = await setup.TransferLogs.ToListAsync();
            Assert.Equal(2, historic.Select(x => x.EventId).Distinct().Count());
            Assert.DoesNotContain(historic, x => x.EventId == Guid.Empty);
            var id = Guid.NewGuid();
            async Task Add()
            {
                await using var db = new TransferDbContext(TransferOptions(database));
                await new TransferRepository(db).Add(new TransferLog { EventId = id, FromAccount = 1, ToAccount = 2, Amount = 5 });
            }
            await Task.WhenAll(Add(), Add());
            Assert.Single(await setup.TransferLogs.Where(x => x.EventId == id).ToListAsync());
        }
        finally { await setup.Database.EnsureDeletedAsync(); }
    }
    [IntegrationFact, Trait("Category", "Integration")]
    public async Task Broker_retries_then_deadletters_poison_message_and_keeps_consumer_alive()
    {
        var vhost = Environment.GetEnvironmentVariable("TEST_RABBIT_VHOST") ?? "/";
        var options = new RabbitMqOptions { VirtualHost = vhost, MaxRetries = 1, RetryDelayMilliseconds = 100 };
        var services = new ServiceCollection().AddLogging().AddSingleton(Options.Create(options))
            .AddSingleton<IHostApplicationLifetime, TestLifetime>().AddTransient<FailingHandler>();
        using var provider = services.BuildServiceProvider();
        using var bus = new RabbitMQBus(provider.GetRequiredService<IServiceScopeFactory>(), Options.Create(options), provider.GetRequiredService<ILogger<RabbitMQBus>>(), provider.GetRequiredService<IHostApplicationLifetime>());
        var factory = new ConnectionFactory { VirtualHost = vhost };
        using var connection = factory.CreateConnection(); using var channel = connection.CreateModel();
        var queue = RabbitMQBus.QueueName<PoisonEvent>();
        try
        {
            bus.Subscribe<PoisonEvent, FailingHandler>();
            await Task.Delay(100); // Subscribe must outlive its stack frame.
            bus.Publish(new PoisonEvent());
            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (DateTime.UtcNow < deadline)
            {
                var dead = channel.BasicGet(queue + ".dead", true);
                if (dead != null) { Assert.True(bus.IsReady); Assert.Equal(2, FailingHandler.Attempts); return; }
                await Task.Delay(100);
            }
            Assert.Fail("Message never reached dead-letter queue");
        }
        finally
        {
            bus.Dispose();
            channel.QueueDelete(queue); channel.QueueDelete(queue + ".retry"); channel.QueueDelete(queue + ".dead");
        }
    }
    public sealed class PoisonEvent : MicroServicesRabbitMQ.Domain.Core.Events.Event { }
    public sealed class FailingHandler : IEventHandler<PoisonEvent>
    {
        public static int Attempts;
        public Task Handle(PoisonEvent message) { Interlocked.Increment(ref Attempts); throw new InvalidOperationException("poison"); }
    }
    private sealed class TestLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => default;
        public CancellationToken ApplicationStopping => default;
        public CancellationToken ApplicationStopped => default;
        public void StopApplication() { }
    }
}
