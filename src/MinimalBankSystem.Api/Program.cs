using MinimalBankSystem.Api.ExceptionMapping;
using MinimalBankSystem.Api.Logging;
using MinimalBankSystem.Api.Middleware;

namespace MinimalBankSystem.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var redactionOptions = new SensitiveDataRedactionOptions();

        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IExceptionMapper, DefaultExceptionMapper>();
        builder.Services.AddSingleton(redactionOptions);
        builder.Services.AddControllers();

        builder.Logging.ClearProviders();
        builder.Logging.AddJsonConsole(options =>
        {
            options.IncludeScopes = true;
            options.JsonWriterOptions = new System.Text.Json.JsonWriterOptions
            {
                Indented = false
            };
        });

        var app = builder.Build();

        if (!app.Environment.IsEnvironment("Testing"))
        {
            Console.SetOut(new RedactingTextWriter(Console.Out, redactionOptions.ProhibitedFieldNames));
        }

        app.UseMiddleware<ExceptionHandlingMiddleware>();
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.MapControllers();

        app.Run();
    }
}
