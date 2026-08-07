// FND-01 establishes the API project boundary only.
//
// The host is intentionally empty: no endpoint, no controller, no middleware and
// no service registration is defined here. The common API execution contract
// (error envelope, correlation ID, TimeProvider, logging) is owned by FND-02 and
// the health contract by a later WP-1 issue.

WebApplication app = WebApplication.CreateBuilder(args).Build();

app.Run();
