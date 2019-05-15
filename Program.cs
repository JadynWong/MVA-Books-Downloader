using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace bookdown
{
    class Program
    {
        public static IConfiguration Configuration;
        public static IServiceProvider ServiceProvider;
        static async Task Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            Configuration = new ConfigurationBuilder()
               .SetBasePath(Directory.GetCurrentDirectory())
               .AddJsonFile("appsettings.json", true, true)
               .Build();

            var services = new ServiceCollection();
            services.AddOptions();
            services.AddHttpClient<BookSearchService>();

            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddConfiguration(Configuration);
                builder.SetMinimumLevel(LogLevel.Information);
                builder.AddFilter("Microsoft", LogLevel.Warning);
                builder.AddFilter("System", LogLevel.Warning);
                builder.AddConsole(c => c.IncludeScopes = true);
            });
            services.TryAddScoped<BookSearchService>();
            services.AddSingleton(Configuration);
            ServiceProvider = services.BuildServiceProvider();
            using (var scope = ServiceProvider.CreateScope())
            {
                var booksearch = scope.ServiceProvider.GetRequiredService<BookSearchService>();
                var dic = await booksearch.SearchAsync();
                await booksearch.DownloadBooks(dic);
            }

            Console.ReadKey();
        }
    }
}
