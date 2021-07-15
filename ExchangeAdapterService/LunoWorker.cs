using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.IO;
using System.Text.Json.Serialization;
using StackExchange.Redis;
using Bottrader.Shared;

namespace ExchangeAdapterService
{
    public class LunoWorker : BackgroundService
    {
        private readonly ILogger<LunoWorker> _logger;
        private readonly IConnectionMultiplexer _redis;
        private readonly LunoConfig _config;

        private IDatabase _redis_db;
        private ISubscriber _redis_subscriber;
        private string _tradepair;

        private LunoOrderBook orderBook;

        public LunoWorker(ILogger<LunoWorker> logger, IConnectionMultiplexer connex, LunoConfig config, string tradepair)
        {
            _logger = logger;
            _redis = connex;
            _redis_db = _redis.GetDatabase();
            _redis_subscriber = _redis.GetSubscriber();
            _config = config;
            _tradepair = tradepair;
            // _tradepair = "XBTZAR";

            orderBook = null;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Boolean isConnected = false;
            int currentRetryCount = 0;
            int maxRetryCount = 5;

            while (!stoppingToken.IsCancellationRequested && (currentRetryCount < maxRetryCount))
            {
                _logger.LogInformation(String.Format("Starting Luno worker for {0} - RetryCount {1}. Is Connected {2} ",_tradepair,currentRetryCount, isConnected));
                // We don't have a connection yet, create the WS connection and kick off threads for reeiving messages and sending keepalives
                if (!isConnected)
                {
                    _logger.LogInformation(String.Format("Connecting to WS for {0}",_tradepair));

                    try
                    {
                        using (var socket = new ClientWebSocket())
                        {
                            //await socket.ConnectAsync(new Uri(String.Format( "wss://ws.luno.com/api/1/stream/{0}",_tradepair)), CancellationToken.None);
                            string wsConnectionString = String.Format(_config.WebsocketUrl, _tradepair);
                            await socket.ConnectAsync(new Uri(wsConnectionString), CancellationToken.None);

                            if(socket.State == WebSocketState.Open)
                            {
                                isConnected = true;                                
                                await Authenticate(socket);
                                // Also reset retry count
                                currentRetryCount = 0;

                                Task t = ReceiveMessage(socket, isConnected, stoppingToken);
                                Task k = KeepAlive(socket, isConnected, stoppingToken);

                                // Block main thread here to avoid socket being disposed.
                                // It continues if any of the two threads return
                                Task.WaitAny(t, k);
                                if (t.IsFaulted)
                                    _logger.LogError(t.Exception, string.Format("ReceiveMessage Faulted for {0}", _tradepair));
                                if (k.IsFaulted)
                                    _logger.LogError(k.Exception, string.Format("KeepAlive Faulted for {0}", _tradepair));

                                _logger.LogInformation(String.Format("WS connection disconnected for {0}. Wait and retry.", _tradepair));

                                if (socket.State != WebSocketState.Closed)
                                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);

                                isConnected = false;
                            }                            
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, String.Format("Error in Luno Websocket connection for {0}",_tradepair));
                        isConnected = false;
                    }

                }

                // Delay before restarting and reconnecting..
                int waitTime = 1000 * ++currentRetryCount;
                _logger.LogInformation(String.Format("Connection restarting in {0} ms for {1} ",waitTime,_tradepair));
                isConnected = false;
                // reset the orderbook
                orderBook = null;
                await Task.Delay(waitTime, stoppingToken);                
                    
            }
        }

        /// <summary>
        /// Send keep alive messages to keep WS open
        /// </summary>
        /// <param name="socket"></param>
        /// <returns></returns>
        protected async Task KeepAlive(ClientWebSocket socket, Boolean keepAlive, CancellationToken stoppingToken)
        {
            while (keepAlive && socket.State == WebSocketState.Open && !stoppingToken.IsCancellationRequested )
            {
                Thread.Sleep(60000);
                await socket.SendAsync(Encoding.UTF8.GetBytes(""), WebSocketMessageType.Text, true, CancellationToken.None);
                _logger.LogInformation(string.Format ("Keepalive sent for {0}", _tradepair));
            }

            _logger.LogInformation(String.Format("KeepAlive exits for {0}",_tradepair));
        }

        /// <summary>
        /// Sends Authentication details to the exchange
        /// </summary>
        /// <param name="socket"></param>
        /// <returns></returns>
        protected async Task Authenticate(ClientWebSocket socket)
        {
            // LunoAuthDetails auth = new LunoAuthDetails() { api_key_id = "ev7yd8cjxunbg", api_key_secret = "SngH7dkVN9icTvfqU1owoktfdh2VKIJyltq1VNXeOEs" };
            LunoAuthDetails auth = new LunoAuthDetails() { api_key_id = _config.ApiKeyId, api_key_secret = _config.ApiKeySecret };
            string authString = JsonSerializer.Serialize<LunoAuthDetails>(auth);
            await socket.SendAsync(Encoding.UTF8.GetBytes(authString), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        /// <summary>
        /// Processes messages received from the exchange
        /// </summary>
        /// <param name="socket"></param>
        /// <returns></returns>
        protected async Task ReceiveMessage(ClientWebSocket socket, Boolean isConnected, CancellationToken stoppingToken)
        {
            var buffer = new ArraySegment<byte>(new byte[2048]);
            do
            {
                WebSocketReceiveResult result;
                using (var ms = new MemoryStream())
                {
                    do
                    {
                        result = await socket.ReceiveAsync(buffer, CancellationToken.None);
                        ms.Write(buffer.Array, buffer.Offset, result.Count);
                    } while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Close)
                        break;

                    ms.Seek(0, SeekOrigin.Begin);
                    using (var reader = new StreamReader(ms, Encoding.UTF8))
                    {
                        string responseString = reader.ReadToEnd();
                        if (responseString == null || responseString.Length <= 0)
                            processKeepAlive();

                        // The first message after connection is the OrderBook. Just deserialise and Go
                        else if (orderBook == null)
                        {
                            orderBook = JsonSerializer.Deserialize<LunoOrderBook>(responseString);
                        }
                        // Now we only receive Order book updates and KeepAlives. 
                        else
                        {
                            var updates = JsonSerializer.Deserialize<LunoOrderBookUpdate>(responseString);
                            if (!processOrderbookUpdate(updates))
                                isConnected = false; // The processing of update was unsuccesfull, break connection and rebuild order book
                        }

                        // Console.WriteLine(responseString);
                    }
                }
            } while (isConnected && !stoppingToken.IsCancellationRequested);

            _logger.LogInformation(string.Format("ReceiveMessage exits for {0}",_tradepair));
        }

        protected bool processOrderbookUpdate(LunoOrderBookUpdate updates)
        {
            if (Convert.ToUInt64(updates.sequence) != (orderBook.CurrentSequence + 1))
            {
                //Out of sequence message. Disconnect and refresh order book
                disconnect(string.Format("Out of sequence message for {0}, got seq: {1} expected seq: {2}",_tradepair, updates.sequence, orderBook.CurrentSequence + 1));
                return false;
            }

            if (updates.trade_updates != null && updates.trade_updates.Length > 0)
            {
                foreach (var tu in updates.trade_updates)
                {
                    var makerOrder = orderBook.AllOrders.Where(ob => ob.id == tu.maker_order_id).FirstOrDefault();
                    // var takerOrder = orderBook.AllOrders.Where(ob => ob.id == tu.taker_order_id).FirstOrDefault();

                    //decimal tradeVolume = Convert.ToDecimal(tu.Base);
                    decimal makerVolume = makerOrder.Volume - Convert.ToDecimal(tu.Base);

                    // If the whole makerOrder has been fulfilled, remove from OrderBook, else reduce volume available
                    if (makerVolume <= 0)
                        deleteOrderFromOrderBook(makerOrder.id);
                    else
                        makerOrder.volume = Convert.ToString(makerVolume);

                    addTradeToOrderBook(makerOrder, tu);
                }
            }
            if (updates.delete_update != null)
            {
                deleteOrderFromOrderBook(updates.delete_update.order_id);                
            }
            if (updates.create_update != null)
            {
                if (updates.create_update.type == "BID")
                    orderBook.bids = orderBook.bids.Concat(new[]
                    { new LunoOBOrder()
                        {
                            id = updates.create_update.order_id,
                            price = updates.create_update.price,
                            volume = updates.create_update.volume
                        }
                    }).ToArray();

                else if (updates.create_update.type == "ASK")
                    orderBook.asks = orderBook.asks.Concat(new[]
                    { new LunoOBOrder()
                        {
                            id = updates.create_update.order_id,
                            price = updates.create_update.price,
                            volume = updates.create_update.volume
                        }
                    }).ToArray();
            }
            if (updates.status_update != null)
            {
                orderBook.status = updates.status_update.status;
            }

            // OK the order update has been processed succesfully, now update the sequence
            orderBook.CurrentSequence = Convert.ToUInt64(updates.sequence);
            return true;
        }


        protected void deleteOrderFromOrderBook(string order_id)
        {
            orderBook.asks = orderBook.asks.Where(ob => ob.id != order_id).ToArray();
            orderBook.bids = orderBook.bids.Where(ob => ob.id != order_id).ToArray();
        }

        protected void addTradeToOrderBook(LunoOBOrder makerOrder, LunoTradeUpdate tu)
        {
            if (orderBook.Trades == null)
                orderBook.Trades = new List<LunoTrade>();

            LunoTrade new_trade = new LunoTrade() { date = DateTime.Now, Price = makerOrder.Price, Volume = Convert.ToDecimal(tu.Base), trade = tu };
            orderBook.Trades.Add(new_trade);
            string new_trade_string = JsonSerializer.Serialize<LunoTrade>(new_trade);

            _redis_db.StringSetAsync(String.Format("{0}.PRICE", _tradepair), makerOrder.price);
            _redis_db.ListLeftPushAsync(String.Format("{0}.TRADES", _tradepair), new_trade_string);
            _redis_db.StringSetAsync(String.Format("{0}.BIDS", _tradepair), JsonSerializer.Serialize<LunoOBOrder[]>(orderBook.bids));
            _redis_db.StringSetAsync(String.Format("{0}.ASKS", _tradepair), JsonSerializer.Serialize<LunoOBOrder[]>(orderBook.asks));

            _redis_subscriber.PublishAsync(String.Format("{0}.TRADE", _tradepair), new_trade_string);

            _logger.LogInformation(_tradepair+" Trade at " + makerOrder.price+" Volume: " + tu.Base);
        }

        protected void disconnect(string disconnectionReason)
        {
            _logger.LogInformation(string.Format("Disconnect started on {0} : {1} ",_tradepair, disconnectionReason));
            orderBook = null;
        }
        
        protected void processKeepAlive()
        {
            _logger.LogInformation( String.Format("Keepalive received for {0}",_tradepair));            
        }

    }
    public class LunoAuthDetails
    {
        public string api_key_id { get; set; }
        public string api_key_secret { get; set; }
    }

    public class LunoOrderBook
    {
        private ulong? seq;
       
        public string sequence { get; set; }
        public LunoOBOrder[] asks { get; set; }
        public LunoOBOrder[] bids { get; set; }
        public string status { get; set; }
        public long timestamp { get; set; }

        public List<LunoTrade> Trades { get; set; }

        public ulong CurrentSequence
        {
            get
            {
                if (!seq.HasValue)
                    seq = Convert.ToUInt64(sequence);
                return seq.Value;
            }
            set
            {
                seq = value;
            }
        }
        public LunoOBOrder[] AllOrders
        {
            get { return asks.Concat(bids).ToArray(); }
        }
    }

    public class LunoOrderBookUpdate
    {
        public string sequence { get; set; }
        public LunoTradeUpdate[] trade_updates { get; set; }
        public LunoCreateUpdate create_update { get; set; }
        public LunoDeleteUpdate delete_update { get; set; }
        public LunoStatusUpdate status_update { get; set; }
        public long timestamp { get; set; }
    }
    public class LunoCreateUpdate
    {
        public string order_id { get; set; }
        public string type { get; set; }
        public string price { get; set; }
        public string volume { get; set; }
    }

    public class LunoDeleteUpdate
    {
        public string order_id { get; set; }
    }

    public class LunoStatusUpdate
    {
        public string status { get; set; }
    }

    public class LunoTradeUpdate
    {
        [JsonPropertyName("base")]
        public string Base { get; set; }
        public string counter { get; set; }
        public string maker_order_id { get; set; }
        public string taker_order_id { get; set; }
    }

    public class LunoOBOrder
    {
        public string id { get; set; }
        public string price { get; set; }
        public string volume { get; set; }

        public decimal Price { get { return Convert.ToDecimal(price); } }
        public decimal Volume { get { return Convert.ToDecimal(volume); } }
    }

    public class LunoTrade
    {
        public LunoTradeUpdate trade { get; set; }
        public decimal Price { get; set; }
        public decimal Volume { get; set; }
        public DateTime date { get; set; }
    }

    
}
