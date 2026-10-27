using System;

namespace MicroServicesRabbitMQ.Domain.Core.Events
{
    public abstract class Event
    {
        public Guid EventId { get; set; } = Guid.NewGuid();
        public DateTime Timestamp { get; protected set; }

        public Event()
        {
            Timestamp = DateTime.UtcNow;
        }
    }
}
