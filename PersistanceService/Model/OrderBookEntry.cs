using System;
using System.ComponentModel.DataAnnotations;

namespace PersistanceService.Model
{
    public class OrderBookEntry
    {
        public OrderBookEntry()
        {
        }

        [Key]
        public long ID { get; set; }
        public DateTime Timestamp { get; set; }
        public String Type { get; set; }
        public String TradePair { get; set; }
        public decimal MinPrice { get; set; }
        public decimal MaxPrice { get; set; }
        public decimal Volume { get; set; }
    }
}
