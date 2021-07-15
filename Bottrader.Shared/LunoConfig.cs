using System;
namespace Bottrader.Shared
{
    public class LunoConfig
    {
        public string WebsocketUrl { get; set; }
        public string ApiKeyId { get; set; }
        public string ApiKeySecret { get; set; }
        public string[] TradingPairs { get; set; }
    }
}
