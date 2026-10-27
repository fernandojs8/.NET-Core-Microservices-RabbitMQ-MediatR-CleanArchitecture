using MediatR;
using MicroRabbit.Banking.Domain.Commands;
using MicroRabbit.Banking.Domain.Events;
using MicroRabbit.Banking.Domain.Interfaces;
using System.Threading;
using System.Threading.Tasks;
namespace MicroRabbit.Banking.Domain.CommandHandlers;
public class TransferCommandHandler : IRequestHandler<CreateTransferCommand, bool>
{
    private readonly IAccountRepository _repository;
    public TransferCommandHandler(IAccountRepository repository) => _repository = repository;
    public async Task<bool> Handle(CreateTransferCommand request, CancellationToken cancellationToken)
    {
        await _repository.TransferAsync(new TransferCreatedEvent(request.From, request.To, request.Amount), cancellationToken);
        return true;
    }
}
