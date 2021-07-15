using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Microsoft.Extensions.Configuration;
using Bottrader.Shared;


namespace PersistanceService
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
                    RedisConfig redisConfig = hostContext.Configuration.GetSection("Redis").Get<RedisConfig>();
                    services.AddSingleton(redisConfig);
                    services.Replace(ServiceDescriptor.Singleton(typeof(ILogger<>), typeof(TimedLogger<>)));

                    services.AddDbContext<Model.TradebotContext>(options =>
                        options.UseNpgsql( hostContext.Configuration.GetConnectionString("tradebotContext")),ServiceLifetime.Scoped);

                    services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(string.Format("{0}:{1}, password={2}", redisConfig.IP, redisConfig.Port, redisConfig.Password)));
                    services.AddHostedService<PostgresWorker>();
                });
    }
}
