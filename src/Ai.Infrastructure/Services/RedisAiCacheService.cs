using Ai.Application.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Ai.Infrastructure.Services;

public class RedisAiCacheService : IAiCacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisAiCacheService> _logger;

    public RedisAiCacheService(
        IConnectionMultiplexer redis,
        ILogger<RedisAiCacheService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!_redis.IsConnected) return null;
        try
        {
            var db = _redis.GetDatabase();
            var value = await db.StringGetAsync(key);
            return value.HasValue ? (string?)value : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis GET failed for key {Key}", key);
            return null;
        }
    }

    public async Task SetAsync(string key, string value, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        if (!_redis.IsConnected) return;
        try
        {
            var db = _redis.GetDatabase();
            await db.StringSetAsync(key, value, expiry);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis SET failed for key {Key}", key);
        }
    }
}
