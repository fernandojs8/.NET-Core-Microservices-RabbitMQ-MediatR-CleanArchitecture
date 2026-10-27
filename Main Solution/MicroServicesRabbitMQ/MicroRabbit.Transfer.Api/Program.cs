using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using MicroRabbit.Transfer.Data.Context;
using System.Linq;
using System.Threading.Tasks;
namespace MicroRabbit.Transfer.Api;
public class Program
{
    public static async Task Main(string[] args)
    {
        using var host = CreateHostBuilder(args.Where(arg => arg != "--migrate").ToArray()).Build();
        if (args.Contains("--migrate"))
        {
            using var scope = host.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<TransferDbContext>().Database.MigrateAsync();
            return;
        }
        await host.RunAsync();
    }
    public static IHostBuilder CreateHostBuilder(string[] args) => Host.CreateDefaultBuilder(args)
        .ConfigureWebHostDefaults(webBuilder => webBuilder.UseStartup<Startup>());
}
