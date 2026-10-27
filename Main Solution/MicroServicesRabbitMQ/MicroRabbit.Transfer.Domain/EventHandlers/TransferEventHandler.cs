using MicroRabbit.Banking.Domain.Events;
using MicroRabbit.Transfer.Domain.Interfaces;
using MicroRabbit.Transfer.Domain.Models;
using MicroServicesRabbitMQ.Domain.Core.Bus;
using System.Threading.Tasks;
namespace MicroRabbit.Transfer.Domain.EventHandlers;
public class TransferEventHandler(ITransferRepository repository) : IEventHandler<TransferCreatedEvent>
{
    public Task Handle(TransferCreatedEvent transfer)
    {
        if (transfer.EventId == System.Guid.Empty) throw new System.ArgumentException("EventId is required");
        MicroRabbit.Banking.Domain.Models.TransferRules.Validate(transfer.From, transfer.To, transfer.Amount);
        return repository.Add(new TransferLog
        {
            EventId = transfer.EventId, FromAccount = transfer.From, ToAccount = transfer.To, Amount = transfer.Amount
        });
    }
}
