using MicroRabbit.Banking.Domain.Models;
using MicroRabbit.Banking.Domain.Commands;
using MicroRabbit.Banking.Domain.CommandHandlers;
using MicroRabbit.Banking.Domain.Events;
using MicroRabbit.Banking.Domain.Interfaces;
using Xunit;
namespace MicroRabbit.Tests;
public class TransferRulesTests
{
    [Theory]
    [InlineData(0, 2, 10)] [InlineData(1, 0, 10)] [InlineData(1, 1, 10)]
    [InlineData(1, 2, 0)] [InlineData(1, 2, -1)] [InlineData(1, 2, 1.001)]
    public void Invalid_transfer_is_rejected(int from, int to, decimal amount)
        => Assert.Throws<ArgumentException>(() => new CreateTransferCommand(from, to, amount));
    [Fact] public void Valid_transfer_accepts_cents() => TransferRules.Validate(1, 2, 0.01m);
    [Fact] public async Task Handler_awaits_commit_and_propagates_failure()
    {
        var repository = new PendingRepository();
        var task = new TransferCommandHandler(repository).Handle(new CreateTransferCommand(1, 2, 10), default);
        Assert.False(task.IsCompleted);
        repository.Completion.SetException(new InvalidOperationException("commit failed"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => task);
    }
    private sealed class PendingRepository : IAccountRepository
    {
        public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IEnumerable<Account> GetAccounts() => Array.Empty<Account>();
        public Task TransferAsync(TransferCreatedEvent transfer, CancellationToken cancellationToken) => Completion.Task;
    }
}
