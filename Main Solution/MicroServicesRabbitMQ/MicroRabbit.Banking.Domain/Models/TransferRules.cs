using System;
namespace MicroRabbit.Banking.Domain.Models;
public static class TransferRules
{
    public static void Validate(int from, int to, decimal amount)
    {
        if (from <= 0 || to <= 0 || from == to)
            throw new ArgumentException("Informe duas contas distintas e válidas.");
        if (amount <= 0 || amount > 9999999999999999.99m || decimal.Round(amount, 2) != amount)
            throw new ArgumentException("O valor deve ser positivo e ter até duas casas decimais.");
    }
}
