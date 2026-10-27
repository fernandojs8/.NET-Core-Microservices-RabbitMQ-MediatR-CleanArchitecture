using MicroRabbit.Banking.Domain.Models;
using MicroRabbit.Banking.Domain.Events;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
namespace MicroRabbit.Banking.Domain.Interfaces;
public interface IAccountRepository
{
    IEnumerable<Account> GetAccounts();
    Task TransferAsync(TransferCreatedEvent transfer, CancellationToken cancellationToken);
}
