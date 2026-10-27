namespace MicroServicesRabbitMQ.Infrastructure.Bus;
public sealed class RabbitMqOptions
{
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";
    public int MaxRetries { get; set; } = 3;
    public int RetryDelayMilliseconds { get; set; } = 5000;
}
