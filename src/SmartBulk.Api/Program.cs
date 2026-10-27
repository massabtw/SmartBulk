using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using SmartBulk.Api.Authentication;
using SmartBulk.Api.BackgroundServices;
using SmartBulk.Application;
using SmartBulk.Infrastructure;
using SmartBulk.Infrastructure.Database;

var builder = WebApplication.CreateBuilder(args);

// Add layers to DI
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Add Hosted Services
builder.Services.AddHostedService<TelegramPollingHostedService>();
builder.Services.AddHostedService<CafeteriaReminderHostedService>();

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

// Add CORS
const string FrontendCorsPolicy = "FrontendPolicy";
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:3000", "http://127.0.0.1:3000", "https://localhost:3000", "https://127.0.0.1:3000" };

builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        policy.WithOrigins(corsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SmartBulk API",
        Version = "v1",
        Description = "API de automação nutricional e tracking de refeições do SmartBulk"
    });

    c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Description = "Chave de API. Informe no cabeçalho X-Api-Key.",
        In = ParameterLocation.Header,
        Name = "X-Api-Key",
        Type = SecuritySchemeType.ApiKey
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "ApiKey"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Run DbUp database migrations
var connectionString = builder.Configuration.GetConnectionString("AppDb")
    ?? builder.Configuration.GetConnectionString("DefaultConnection");

if (!string.IsNullOrWhiteSpace(connectionString))
{
    DatabaseMigrator.MigrateDatabase(connectionString, logger: app.Logger);
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "SmartBulk API v1");
    c.RoutePrefix = "swagger";
});

app.UseRouting();

app.UseCors(FrontendCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new
{
    name = "SmartBulk API",
    status = "running",
    swagger = "/swagger",
    frontend = "http://localhost:3000"
})).AllowAnonymous();

app.MapControllers();

app.Run();

public partial class Program { }
