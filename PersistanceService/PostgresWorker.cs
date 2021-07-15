using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using System.Text.Json.Serialization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace PersistanceService
{
    public class PostgresWorker : BackgroundService
    {
        private readonly ILogger<PostgresWorker> _logger;
        private readonly IConnectionMultiplexer _redis;
        private readonly IServiceProvider _provider;

        private IDatabase _redis_db;
        private ISubscriber _redis_subscriber;
        // private Model.TradebotContext _dbContext;

        public PostgresWorker(ILogger<PostgresWorker> logger, IConnectionMultiplexer connex, IServiceProvider provider)
        {
            _logger = logger;
            _redis = connex;
            // _dbContext = dbContext;
            _provider = provider;
            _redis_db = _redis.GetDatabase();
            _redis_subscriber = _redis.GetSubscriber();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await _redis_subscriber.SubscribeAsync("*.TRADE", (channel, value) =>
            {
                _logger.LogInformation(String.Format("Trade received on channel {0}",channel));

                using (var scope = _provider.CreateScope())
                {
                    // get an instance per thread.
                    Model.TradebotContext _dbContext = scope.ServiceProvider.GetRequiredService<Model.TradebotContext>();
                    try
                    {
                        var tradingPair = channel.ToString().Split(".")[0];
                        var values = JsonSerializer.Deserialize<Dictionary<string, object>>(value);

                        SaveTrade(_dbContext, tradingPair, values);
                        SaveOrderBookEntries(_dbContext, tradingPair, values);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, string.Format("Error saving trade on channel {0}. ", channel));
                    }                   
                }
            });

            _logger.LogInformation("Persistanceworker exit");

            while (!stoppingToken.IsCancellationRequested)
            {
                // Just keep alive until cancellation request
                await Task.Delay(1000, stoppingToken);
            }

            // Cancellationtoken is cancelled, unsubscibre from everything
            _redis_subscriber.UnsubscribeAll();
        }

        protected void SaveTrade(Model.TradebotContext _dbContext,string tradingPair, Dictionary<string,object> values)
        {                        
            Model.Trade newTrade = new Model.Trade()
            {
                Price = ((JsonElement)values["Price"]).GetDecimal(),
                Volume = ((JsonElement)values["Volume"]).GetDecimal(),
                TradePair = tradingPair,
                Timestamp = ((JsonElement)values["date"]).GetDateTime()
            };

            // add to data context and save
            _dbContext.Trades.Add(newTrade);
            _dbContext.SaveChanges();
        }

        protected void SaveOrderBookEntries(Model.TradebotContext _dbContext, string tradingPair, Dictionary<string, object> values)
        {
            var bidsCache = _redis_db.StringGet(string.Format("{0}.BIDS", tradingPair));
            var asksCache = _redis_db.StringGet(string.Format("{0}.ASKS", tradingPair));

            var bidsValuesArray = JsonSerializer.Deserialize<Dictionary<string, object>[]>(bidsCache);
            var asksValuesArray = JsonSerializer.Deserialize<Dictionary<string, object>[]>(asksCache);

            decimal minBid = bidsValuesArray.AsEnumerable().Min(bid => ((JsonElement)bid["Price"]).GetDecimal());
            decimal maxBid = bidsValuesArray.AsEnumerable().Max(bid => ((JsonElement)bid["Price"]).GetDecimal());
            decimal volBid = bidsValuesArray.AsEnumerable().Sum(bid => ((JsonElement)bid["Volume"]).GetDecimal());

            var bidOrderBook = new Model.OrderBookEntry
            {
                MinPrice = minBid,
                MaxPrice = maxBid,
                Timestamp = ((JsonElement)values["date"]).GetDateTime(),
                Type = "BID",
                TradePair = tradingPair,
                Volume = volBid
            };

            decimal minAsk = asksValuesArray.AsEnumerable().Min(ask => ((JsonElement)ask["Price"]).GetDecimal());
            decimal maxAsk = asksValuesArray.AsEnumerable().Max(ask => ((JsonElement)ask["Price"]).GetDecimal());
            decimal volAsk = asksValuesArray.AsEnumerable().Sum(ask => ((JsonElement)ask["Volume"]).GetDecimal());

            var askOrderBook = new Model.OrderBookEntry
            {
                MinPrice = minAsk,
                MaxPrice = maxAsk,
                Timestamp = ((JsonElement)values["date"]).GetDateTime(),
                Type = "ASK",
                TradePair = tradingPair,
                Volume = volAsk
            };

            _dbContext.OrderBookEntries.Add(bidOrderBook);
            _dbContext.OrderBookEntries.Add(askOrderBook);
            _dbContext.SaveChanges();
        }
    }
}
