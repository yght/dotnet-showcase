using System;
using System.Threading.Tasks;
using StackExchange.Redis;
using MessagePlatform.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace MessagePlatform.Infrastructure.Services
{
    public class CacheService : ICacheService
    {
        private readonly IDatabase _database;
        private readonly ConnectionMultiplexer _redis;

        public CacheService(IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("Redis");
            _redis = ConnectionMultiplexer.Connect(connectionString);
            _database = _redis.GetDatabase();
        }

        public async Task<T> GetAsync<T>(string key)
        {
            var value = await _database.StringGetAsync(key);
            if (!value.HasValue)
                return default(T);

            return JsonConvert.DeserializeObject<T>(value);
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
        {
            var serializedValue = JsonConvert.SerializeObject(value);
            await _database.StringSetAsync(key, serializedValue, expiry);
        }

        public async Task RemoveAsync(string key)
        {
            await _database.KeyDeleteAsync(key);
        }

        public async Task<bool> ExistsAsync(string key)
        {
            return await _database.KeyExistsAsync(key);
        }

        public void Dispose()
        {
            _redis?.Dispose();
        }
    }
}