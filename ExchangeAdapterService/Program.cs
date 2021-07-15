using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Bottrader.Shared;

namespace ExchangeAdapterService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    LunoConfig lunoConfig = hostContext.Configuration.GetSection("Luno").Get<LunoConfig>();
                    RedisConfig redisConfig = hostContext.Configuration.GetSection("Redis").Get<RedisConfig>();
                    services.AddSingleton(redisConfig);
                    services.AddSingleton(lunoConfig);
                    services.Replace(ServiceDescriptor.Singleton(typeof(ILogger<>), typeof(TimedLogger<>)));

                    services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(string.Format( "{0}:{1}, password={2}",redisConfig.IP,redisConfig.Port,redisConfig.Password)));
                    services.AddHostedService<LunoMainWorker>();                    
                });
    }
}
