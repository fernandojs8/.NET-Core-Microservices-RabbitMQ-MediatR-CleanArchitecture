using MicroRabbit.Banking.Domain.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace MicroRabbit.Banking.Data.Context
{
    public class BankingDbContext : DbContext
    {
        public BankingDbContext(DbContextOptions options) : base(options)
        {
        }

        public DbSet<MicroRabbit.Banking.Data.Outbox.OutboxMessage> OutboxMessages { get; set; }

        public DbSet<Account> Accounts { get; set; }
    }
}
