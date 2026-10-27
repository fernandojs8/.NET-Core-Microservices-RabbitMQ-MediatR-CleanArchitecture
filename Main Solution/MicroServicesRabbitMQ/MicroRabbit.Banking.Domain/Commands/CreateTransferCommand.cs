namespace MicroRabbit.Banking.Domain.Commands
{
    public class CreateTransferCommand : TransferCommand
    {
        public CreateTransferCommand(int from, int to, decimal amount)
        {
            MicroRabbit.Banking.Domain.Models.TransferRules.Validate(from, to, amount);
            From = from;
            To = to;
            Amount = amount;
        }
    }
}
