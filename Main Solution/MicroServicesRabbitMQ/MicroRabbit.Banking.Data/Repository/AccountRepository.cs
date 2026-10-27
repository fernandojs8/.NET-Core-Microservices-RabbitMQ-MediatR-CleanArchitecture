using MicroRabbit.Banking.Data.Context;
using MicroRabbit.Banking.Data.Outbox;
using MicroRabbit.Banking.Domain.Events;
using MicroRabbit.Banking.Domain.Interfaces;
using MicroRabbit.Banking.Domain.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
namespace MicroRabbit.Banking.Data.Repository;
public class AccountRepository : IAccountRepository
{
    private readonly BankingDbContext _context;
    public AccountRepository(BankingDbContext context) => _context = context;
    public IEnumerable<Account> GetAccounts() => _context.Accounts.AsNoTracking().ToList();
    public async Task TransferAsync(TransferCreatedEvent transfer, CancellationToken cancellationToken)
    {
        TransferRules.Validate(transfer.From, transfer.To, transfer.Amount);
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        // Lock both accounts in a deterministic order; competing debits cannot overwrite balances.
        var accounts = await _context.Accounts.FromSqlInterpolated($"SELECT * FROM Accounts WITH (UPDLOCK, HOLDLOCK) WHERE Id IN ({transfer.From}, {transfer.To}) ORDER BY Id").ToListAsync(cancellationToken);
        var from = accounts.SingleOrDefault(a => a.Id == transfer.From);
        var to = accounts.SingleOrDefault(a => a.Id == transfer.To);
        if (from == null || to == null) throw new KeyNotFoundException("Conta não encontrada.");
        if (from.AccountBalance < transfer.Amount) throw new InvalidOperationException("Saldo insuficiente.");
        if (to.AccountBalance > 9999999999999999.99m - transfer.Amount) throw new InvalidOperationException("Limite de saldo excedido.");
        from.AccountBalance -= transfer.Amount;
        to.AccountBalance += transfer.Amount;
        _context.OutboxMessages.Add(new OutboxMessage { Id = transfer.EventId, Payload = JsonSerializer.Serialize(transfer), CreatedAt = DateTime.UtcNow });
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
