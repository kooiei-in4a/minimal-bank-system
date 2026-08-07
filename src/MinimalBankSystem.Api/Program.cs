using MinimalBankSystem.Api.Errors;
using MinimalBankSystem.Api.Contracts;
using MinimalBankSystem.Api.Infrastructure;
using MinimalBankSystem.Api.Middleware;
using MinimalBankSystem.Application;
using Microsoft.AspNetCore.Mvc;

WebApplicationBuilder builder = WebApplication.CreateEmptyBuilder(
    new WebApplicationOptions { Args = args, ContentRootPath = AppContext.BaseDirectory });
builder.WebHost.UseKestrel();

builder.Logging.AddTechnicalLogging();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IApplicationClock, ApplicationClock>();
builder.Services.AddSingleton<IExceptionToHttpMapper, DefaultExceptionToHttpMapper>();
builder.Services
    .AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = _ =>
            new BadRequestObjectResult(new ErrorResponse("validation_failed", "The request is invalid."));
    });

WebApplication app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.MapControllers();

app.Run();

public partial class Program;
