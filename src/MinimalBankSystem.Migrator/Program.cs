using Microsoft.Extensions.Hosting;
using MinimalBankSystem.Migrator;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
return await MigratorApplication.RunAsync(builder.Configuration, Console.Error);
