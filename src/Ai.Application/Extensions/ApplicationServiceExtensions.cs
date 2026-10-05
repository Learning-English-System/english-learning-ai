using Ai.Application.Interfaces;
using Ai.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Ai.Application.Extensions;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddAiApplication(this IServiceCollection services)
    {
        services.AddScoped<IAiService, AiService>();
        return services;
    }
}
