using System;
namespace MicroRabbit.Banking.Data.Outbox;
public class OutboxMessage
{
    public Guid Id { get; set; }
    public string Payload { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
}
