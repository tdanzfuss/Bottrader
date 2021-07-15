using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;

namespace PersistanceService.Model
{
    public class TradebotContext : DbContext
    {
        public DbSet<Trade> Trades { get; set; }
        public DbSet<OrderBookEntry> OrderBookEntries { get; set; }

        public TradebotContext()
        {
        }

        public TradebotContext([NotNullAttribute] DbContextOptions<TradebotContext> options) : base(options)
        {

        }
    }
}
