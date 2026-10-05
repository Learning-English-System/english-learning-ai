namespace Ai.Application.Interfaces;

public interface IAiCacheService
{
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);
    Task SetAsync(string key, string value, TimeSpan expiry, CancellationToken cancellationToken = default);
}
