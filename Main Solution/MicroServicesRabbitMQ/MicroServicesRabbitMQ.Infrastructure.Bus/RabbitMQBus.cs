using MediatR;
using MicroServicesRabbitMQ.Domain.Core.Bus;
using MicroServicesRabbitMQ.Domain.Core.Commands;
using MicroServicesRabbitMQ.Domain.Core.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
namespace MicroServicesRabbitMQ.Infrastructure.Bus;
public sealed class RabbitMQBus : IEventBus, IDisposable
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<RabbitMQBus> _logger;
    private readonly RabbitMqOptions _options;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly object _gate = new();
    private volatile bool _returned;
    private bool _disposed;
    private IConnection _connection;
    private IModel _publisher;
    private readonly Dictionary<string, IModel> _consumers = new();
    public RabbitMQBus(IServiceScopeFactory scopes, IOptions<RabbitMqOptions> options, ILogger<RabbitMQBus> logger, IHostApplicationLifetime lifetime)
    {
        _lifetime = lifetime; _scopes = scopes; _options = options.Value; _logger = logger;
        if (_options.MaxRetries < 0 || _options.RetryDelayMilliseconds < 1) throw new ArgumentException("Invalid RabbitMQ retry configuration");
    }
    public async Task SendCommand<T>(T command) where T : Command
    {
        using var scope = _scopes.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IMediator>().Send(command);
    }
    private void Connect()
    {
        if (_connection?.IsOpen == true && _publisher?.IsOpen == true) return;
        _publisher?.Dispose();
        // Keep a recovering connection and consumer channels alive until application shutdown.
        if (_connection?.IsOpen != true)
        {
            if (_connection != null) throw new InvalidOperationException("Broker reconnecting; retry publication later");
            _connection = new ConnectionFactory
            {
                HostName = _options.HostName, Port = _options.Port, UserName = _options.UserName,
                Password = _options.Password, VirtualHost = _options.VirtualHost,
                DispatchConsumersAsync = true, AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
            }.CreateConnection();
        }
        _publisher = _connection.CreateModel();
        _publisher.ConfirmSelect();
        _publisher.BasicReturn += (_, _) => _returned = true;
    }
    public bool IsReady => _connection?.IsOpen == true && _consumers.Count > 0 && System.Linq.Enumerable.All(_consumers.Values, c => c.IsOpen);
    public static string QueueName<T>() => typeof(T).Name + ".v2";
    private void Declare(IModel channel, string queue)
    {
        channel.QueueDeclare(queue + ".dead", true, false, false, new Dictionary<string, object> { ["x-queue-type"] = "quorum" });
        channel.QueueDeclare(queue, true, false, false, new Dictionary<string, object>
        {
            ["x-queue-type"] = "quorum", ["x-overflow"] = "reject-publish", ["x-dead-letter-strategy"] = "at-least-once",
            ["x-dead-letter-exchange"] = "", ["x-dead-letter-routing-key"] = queue + ".dead"
        });
        channel.QueueDeclare(queue + ".retry", true, false, false, new Dictionary<string, object>
        {
            ["x-message-ttl"] = _options.RetryDelayMilliseconds,
            ["x-queue-type"] = "quorum", ["x-overflow"] = "reject-publish", ["x-dead-letter-strategy"] = "at-least-once",
            ["x-dead-letter-exchange"] = "", ["x-dead-letter-routing-key"] = queue
        });
    }
    public void Publish<T>(T @event) where T : Event
    {
        lock (_gate)
        {
            Connect();
            var queue = QueueName<T>(); Declare(_publisher, queue);
            var properties = _publisher.CreateBasicProperties();
            properties.Persistent = true; properties.ContentType = "application/json";
            properties.MessageId = @event.EventId.ToString();
            PublishConfirmed(queue, properties, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(@event)));
        }
    }
    // Queues are declared on the same channel before publishing to the default exchange.
    private void PublishConfirmed(string queue, IBasicProperties properties, ReadOnlyMemory<byte> body)
    {
        _returned = false;
        _publisher.BasicPublish("", queue, true, properties, body);
        _publisher.WaitForConfirmsOrDie(TimeSpan.FromSeconds(10));
        if (_returned) throw new InvalidOperationException("Broker returned an unroutable message");
    }
    public void Subscribe<T, TH>() where T : Event where TH : IEventHandler<T>
    {
        lock (_gate)
        {
            Connect(); var queue = QueueName<T>();
            if (_consumers.ContainsKey(queue)) throw new InvalidOperationException("Duplicate subscription: " + queue);
            var channel = _connection.CreateModel(); Declare(channel, queue);
            channel.BasicQos(0, 1, false);
            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.Received += async (_, delivery) =>
            {
                try
                {
                    var message = JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(delivery.Body.Span))
                        ?? throw new InvalidOperationException("Empty event");
                    using var scope = _scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<TH>().Handle(message);
                    channel.BasicAck(delivery.DeliveryTag, false);
                    _logger.LogInformation("Processed {Queue} event {EventId}", queue, message.EventId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed processing {Queue}; delivery {Tag}", queue, delivery.DeliveryTag);
                    try
                    {
                        var retries = delivery.BasicProperties.Headers?.TryGetValue("x-retries", out var value) == true ? Convert.ToInt32(value) : 0;
                        if (retries >= _options.MaxRetries)
                        {
                            channel.BasicNack(delivery.DeliveryTag, false, false);
                        }
                        else
                        {
                            lock (_gate)
                            {
                                Connect(); Declare(_publisher, queue);
                                var properties = _publisher.CreateBasicProperties(); properties.Persistent = true;
                                properties.ContentType = "application/json";
                                properties.MessageId = delivery.BasicProperties.MessageId;
                                properties.Headers = new Dictionary<string, object> { ["x-retries"] = retries + 1 };
                                PublishConfirmed(queue + ".retry", properties, delivery.Body);
                            }
                            channel.BasicAck(delivery.DeliveryTag, false);
                        }
                    }
                    catch (Exception retryError)
                    {
                        _logger.LogError(retryError, "Retry publication failed; leaving delivery unacknowledged until reconnect");
                        // Close so the broker requeues the unacknowledged delivery. The host restart policy recovers.
                        channel.Close();
                        _lifetime.StopApplication();
                    }
                }
            };
            channel.BasicConsume(queue, false, consumer);
            _consumers.Add(queue, channel);
        }
    }
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var channel in _consumers.Values) channel.Dispose();
            _publisher?.Dispose(); _connection?.Dispose();
        }
    }
}
