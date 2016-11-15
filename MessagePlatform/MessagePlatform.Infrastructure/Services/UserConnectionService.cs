using MessagePlatform.Core.Interfaces;
using StackExchange.Redis;
using Microsoft.Extensions.Configuration;

namespace MessagePlatform.Infrastructure.Services
{
    public class UserConnectionService : IUserConnectionService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly IDatabase _database;

        public UserConnectionService(IConfiguration configuration)
        {
            _redis = ConnectionMultiplexer.Connect(configuration.GetConnectionString("Redis"));
            _database = _redis.GetDatabase();
        }

        public async Task AddConnectionAsync(string userId, string connectionId)
        {
            await _database.SetAddAsync($"user:connections:{userId}", connectionId);
            await _database.StringSetAsync($"connection:user:{connectionId}", userId);
        }

        public async Task RemoveConnectionAsync(string userId, string connectionId)
        {
            await _database.SetRemoveAsync($"user:connections:{userId}", connectionId);
            await _database.KeyDeleteAsync($"connection:user:{connectionId}");
        }

        public async Task<IEnumerable<string>> GetConnectionsAsync(string userId)
        {
            var values = await _database.SetMembersAsync($"user:connections:{userId}");
            return values.Select(v => v.ToString());
        }

        public async Task<bool> IsUserOnlineAsync(string userId)
        {
            var length = await _database.SetLengthAsync($"user:connections:{userId}");
            return length > 0;
        }
    }
}