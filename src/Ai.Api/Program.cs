using Ai.Application.Extensions;
using Ai.Infrastructure.Extensions;
using Ai.Infrastructure.Grpc;
using BuildingBlocks.Grpc.Extensions;
using BuildingBlocks.Observability;
using BuildingBlocks.Web.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Logging
builder.Logging.AddCustomStructuredLogging(builder.Environment);

// Core services
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Shared building blocks
builder.Services.AddBuildingBlocksWeb();

// AI domain layers
builder.Services.AddAiInfrastructure(builder.Configuration);
builder.Services.AddAiApplication();

// gRPC server & internal authorization policies
builder.Services.AddGrpcServerBuildingBlocks();
builder.Services.AddInternalServiceAuthorizationPolicies();

// Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["AUTH_AUTHORITY"] ?? "http://localhost:5001";
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true
        };
    });

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:8080")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseBuildingBlocksWeb();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapGrpcService<AiInternalGrpcService>();
app.MapControllers();

app.Run();

// Required for WebApplicationFactory in integration tests
public partial class Program { }
