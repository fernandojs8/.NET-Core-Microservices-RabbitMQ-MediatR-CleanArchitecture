using MicroRabbit.Transfer.Data.Context;
using MicroRabbit.Transfer.Domain.Interfaces;
using MicroRabbit.Transfer.Domain.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
namespace MicroRabbit.Transfer.Data.Repository;
public class TransferRepository(TransferDbContext context) : ITransferRepository
{
    public IEnumerable<TransferLog> GetTransferLogs() => context.TransferLogs.AsNoTracking().ToList();
    public async Task Add(TransferLog transferLog)
    {
        // The unique index is the final guard for simultaneous duplicate deliveries.
        if (await context.TransferLogs.AnyAsync(x => x.EventId == transferLog.EventId)) return;
        context.TransferLogs.Add(transferLog);
        try { await context.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            context.ChangeTracker.Clear();
            if (!await context.TransferLogs.AnyAsync(x => x.EventId == transferLog.EventId)) throw;
        }
    }
}
