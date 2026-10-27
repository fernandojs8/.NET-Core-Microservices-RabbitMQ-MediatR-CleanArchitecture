using MicroRabbit.Banking.Application.Interfaces;
using MicroRabbit.Banking.Application.Models;
using MicroRabbit.Banking.Domain.Commands;
using MicroRabbit.Banking.Domain.Interfaces;
using MicroRabbit.Banking.Domain.Models;
using MicroServicesRabbitMQ.Domain.Core.Bus;
using System.Collections.Generic;
using System.Threading.Tasks;
namespace MicroRabbit.Banking.Application.Services;
public class AccountService : IAccountService
{
    private readonly IAccountRepository _repository;
    private readonly IEventBus _bus;
    public AccountService(IAccountRepository repository, IEventBus bus) { _repository = repository; _bus = bus; }
    public IEnumerable<Account> GetAccounts() => _repository.GetAccounts();
    public async Task<string> Transfer(AccountTransfer transfer)
    {
        await _bus.SendCommand(new CreateTransferCommand(transfer.FromAccount, transfer.ToAccount, transfer.TransferAmount));
        return "Transferência gravada; publicação do evento pendente ou em processamento.";
    }
}
