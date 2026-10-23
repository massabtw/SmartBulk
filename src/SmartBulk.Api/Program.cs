using Microsoft.AspNetCore.Authorization;
using SmartBulk.Api.Authentication;
using SmartBulk.Application;
using SmartBulk.Infrastructure;
using SmartBulk.Infrastructure.Database;

var builder = WebApplication.CreateBuilder(args);

// Add layers to DI
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Add Authentication and Authorization
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = ApiKeyAuthenticationOptions.DefaultScheme;
    options.DefaultChallengeScheme = ApiKeyAuthenticationOptions.DefaultScheme;
})
.AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
    ApiKeyAuthenticationOptions.DefaultScheme, null);

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddControllers();

var app = builder.Build();

// Run DbUp database migrations
var connectionString = builder.Configuration.GetConnectionString("AppDb")
    ?? builder.Configuration.GetConnectionString("DefaultConnection");

if (!string.IsNullOrWhiteSpace(connectionString))
{
    DatabaseMigrator.MigrateDatabase(connectionString, logger: app.Logger);
}

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }
