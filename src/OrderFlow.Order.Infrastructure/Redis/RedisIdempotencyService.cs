using System.Text.Json;
using OrderFlow.Order.Application.Idempotency;
using StackExchange.Redis;

namespace OrderFlow.Order.Infrastructure.Redis;

public sealed class RedisIdempotencyService : IIdempotencyService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _database;
    
    private static readonly TimeSpan InProgressTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan CompletedTimeout = TimeSpan.FromHours(24);

    public RedisIdempotencyService(IConnectionMultiplexer redis)
    {
        _redis = redis;
        _database = _redis.GetDatabase();
    }

    public async Task<bool> TryAcquireAsync(string key, string requestHash)
    {
        var record = new IdempotencyRecord("InProgress", 0, string.Empty, Array.Empty<byte>(), string.Empty, requestHash);
        var json = JsonSerializer.Serialize(record);

        // SETNX in Redis guarantees atomicity
        return await _database.StringSetAsync(key, json, InProgressTimeout, When.NotExists);
    }

    public async Task<IdempotencyRecord?> GetRecordAsync(string key)
    {
        var value = await _database.StringGetAsync(key);
        if (!value.HasValue)
        {
            return null;
        }

        return JsonSerializer.Deserialize<IdempotencyRecord>(value.ToString());
    }

    public async Task CompleteAsync(string key, int statusCode, string contentType, byte[] responseBody, string location, string requestHash)
    {
        var record = new IdempotencyRecord("Completed", statusCode, contentType, responseBody, location, requestHash);
        var json = JsonSerializer.Serialize(record);

        await _database.StringSetAsync(key, json, CompletedTimeout);
    }

    public async Task RemoveAsync(string key)
    {
        await _database.KeyDeleteAsync(key);
    }
}
