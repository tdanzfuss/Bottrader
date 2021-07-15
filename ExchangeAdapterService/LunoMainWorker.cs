using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Bottrader.Shared;

namespace ExchangeAdapterService
{
    public class LunoMainWorker : BackgroundService
    {
        private readonly ILogger<LunoWorker> _logger;
        private readonly IConnectionMultiplexer _redis;
        private readonly LunoConfig _config;

        public LunoMainWorker(ILogger<LunoWorker> logger, IConnectionMultiplexer connex, LunoConfig config)
        {
            _logger = logger;
            _redis = connex;
            _config = config;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!stoppingToken.IsCancellationRequested)
            {
                var allTradingPairs = new List<Task>();
                foreach (string tp in _config.TradingPairs)
                {
                    LunoWorker worker = new LunoWorker(_logger, _redis, _config, tp);
                    allTradingPairs.Add(worker.StartAsync(stoppingToken));
                }
                // Task.WaitAll(allTradingPairs.ToArray());
                await Task.WhenAll(allTradingPairs.ToArray());
            }
        }
    }
}
