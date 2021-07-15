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
        public static async Task Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();
            using (var scope = host.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<Model.TradebotContext>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<PostgresWorker>>();
                // At startup wait for DB connection to become ready before launching migrations.
                // We only wait max 5 retries with 2s delay
                int retryCount = 0;
                while (!db.Database.CanConnect())
                {
                    logger.LogWarning("Database not yet ready, retrying in 2s. RetryCount:"+ ++retryCount);
                    await Task.Delay(2000);
                    if (retryCount > 10)
                        throw new Exception("Could not connect to database after 10 retries. ConnectionString: " + db.Database.GetConnectionString());
                }
                db.Database.Migrate();                
            }
            host.Run();
            // CreateHostBuilder(args).Build().Run();
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
