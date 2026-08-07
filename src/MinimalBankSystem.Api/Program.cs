using MinimalBankSystem.Api.Runtime;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.AddApiRuntime();

WebApplication app = builder.Build();
app.UseApiRuntime();

app.Run();

public partial class Program;
