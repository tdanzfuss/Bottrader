using System;
using System.ComponentModel.DataAnnotations;

namespace PersistanceService.Model
{
    public class Trade
    {
        public Trade()
        {
        }

        [Key]
        public long ID { get; set; }
        public DateTime Timestamp { get; set; }
        public String TradePair { get; set; }
        public decimal Price { get; set; }
        public decimal Volume { get; set; }
    }
}
