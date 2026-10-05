using Ai.Application.Interfaces;
using Ai.Domain.Interfaces;
using Ai.Infrastructure.Options;
using Ai.Infrastructure.Providers;
using Ai.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Ai.Infrastructure.Extensions;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddAiInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AiOptions>(options =>
        {
            var section = configuration.GetSection("Ai");
            options.Provider = section["Provider"]
                ?? configuration["AI_PROVIDER"]
                ?? "fake";
            options.GeminiApiKey = section["GeminiApiKey"]
                ?? configuration["GEMINI_API_KEY"];
            options.GeminiModel = section["GeminiModel"]
                ?? configuration["GEMINI_MODEL"]
                ?? "gemini-2.5-flash";
            options.PromptVersion = section["PromptVersion"] ?? "v1";
        });

        // Register HTTP client for Gemini provider
        services.AddHttpClient<GeminiTextAiProvider>();

        // Register ITextAiProvider based on configuration
        services.AddScoped<ITextAiProvider>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AiOptions>>().Value;
            if (options.Provider.Equals("gemini", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(options.GeminiApiKey))
            {
                return sp.GetRequiredService<GeminiTextAiProvider>();
            }

            return new FakeTextAiProvider();
        });

        // Pronunciation evaluator
        services.AddSingleton<IPronunciationEvaluator, AcousticPronunciationEvaluator>();

        // Redis
        string redisConn = configuration.GetConnectionString("Redis")
            ?? configuration["REDIS_CONNECTION"]
            ?? "localhost:6379";

        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var config = ConfigurationOptions.Parse(redisConn);
            config.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(config);
        });

        services.AddSingleton<IAiCacheService, RedisAiCacheService>();

        return services;
    }
}
