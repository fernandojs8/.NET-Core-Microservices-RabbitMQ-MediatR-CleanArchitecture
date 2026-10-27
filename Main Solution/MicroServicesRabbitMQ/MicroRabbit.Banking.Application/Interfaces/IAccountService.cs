using MicroRabbit.Banking.Application.Models;
using MicroRabbit.Banking.Domain.Models;
using System;
using System.Collections.Generic;

namespace MicroRabbit.Banking.Application.Interfaces
{
    public interface IAccountService
    {
        IEnumerable<Account> GetAccounts();
        System.Threading.Tasks.Task<string> Transfer(AccountTransfer accountTransfer);
    }
}
