using MessagePlatform.Core.Entities;
using MessagePlatform.Core.Interfaces;
using MongoDB.Driver;
using MongoDB.Bson;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MessagePlatform.Infrastructure.Services
{
    public class MessageService : IMessageService
    {
        private readonly IMongoCollection<Message> _messages;
        private readonly ICacheService _cacheService;
        private readonly ILogger<MessageService> _logger;

        public MessageService(IConfiguration configuration, ICacheService cacheService, ILogger<MessageService> logger)
        {
            var client = new MongoClient(configuration.GetConnectionString("MongoDB"));
            var database = client.GetDatabase("MessagePlatform");
            _messages = database.GetCollection<Message>("messages");
            _cacheService = cacheService;
            _logger = logger;

            // Create indexes
            CreateIndexes();
        }

        private void CreateIndexes()
        {
            var indexOptions = new CreateIndexOptions { Background = true };
            
            // Composite index for conversation queries
            var conversationIndex = Builders<Message>.IndexKeys
                .Ascending(m => m.SenderId)
                .Ascending(m => m.RecipientId)
                .Descending(m => m.Timestamp);
            _messages.Indexes.CreateOne(new CreateIndexModel<Message>(conversationIndex, indexOptions));

            // Index for group messages
            var groupIndex = Builders<Message>.IndexKeys
                .Ascending(m => m.GroupId)
                .Descending(m => m.Timestamp);
            _messages.Indexes.CreateOne(new CreateIndexModel<Message>(groupIndex, indexOptions));

            // Text index for search
            var textIndex = Builders<Message>.IndexKeys.Text(m => m.Content);
            _messages.Indexes.CreateOne(new CreateIndexModel<Message>(textIndex, indexOptions));
        }

        public async Task<Message> SaveMessageAsync(Message message)
        {
            await _messages.InsertOneAsync(message);
            _logger.LogInformation($"Message {message.Id} saved successfully");
            
            // Invalidate cache for conversation
            var cacheKey = GetConversationCacheKey(message.SenderId, message.RecipientId ?? "");
            await _cacheService.RemoveAsync(cacheKey);
            
            return message;
        }

        public async Task<Message?> GetMessageAsync(string messageId)
        {
            var cacheKey = $"message:{messageId}";
            var cachedMessage = await _cacheService.GetAsync<Message>(cacheKey);
            if (cachedMessage != null)
                return cachedMessage;

            var message = await _messages.Find(m => m.Id == messageId).FirstOrDefaultAsync();
            if (message != null)
            {
                await _cacheService.SetAsync(cacheKey, message, TimeSpan.FromMinutes(5));
            }

            return message;
        }

        public async Task<IEnumerable<Message>> GetMessagesAsync(string userId1, string userId2, int limit = 50, DateTime? before = null)
        {
            var filter = Builders<Message>.Filter.And(
                Builders<Message>.Filter.Or(
                    Builders<Message>.Filter.And(
                        Builders<Message>.Filter.Eq(m => m.SenderId, userId1),
                        Builders<Message>.Filter.Eq(m => m.RecipientId, userId2)
                    ),
                    Builders<Message>.Filter.And(
                        Builders<Message>.Filter.Eq(m => m.SenderId, userId2),
                        Builders<Message>.Filter.Eq(m => m.RecipientId, userId1)
                    )
                ),
                Builders<Message>.Filter.Eq(m => m.IsDeleted, false)
            );

            if (before.HasValue)
            {
                filter = Builders<Message>.Filter.And(
                    filter,
                    Builders<Message>.Filter.Lt(m => m.Timestamp, before.Value)
                );
            }

            return await _messages.Find(filter)
                .SortByDescending(m => m.Timestamp)
                .Limit(limit)
                .ToListAsync();
        }

        public async Task<IEnumerable<Message>> GetGroupMessagesAsync(string groupId, int limit = 50, DateTime? before = null)
        {
            var filter = Builders<Message>.Filter.And(
                Builders<Message>.Filter.Eq(m => m.GroupId, groupId),
                Builders<Message>.Filter.Eq(m => m.IsDeleted, false)
            );

            if (before.HasValue)
            {
                filter = Builders<Message>.Filter.And(
                    filter,
                    Builders<Message>.Filter.Lt(m => m.Timestamp, before.Value)
                );
            }

            return await _messages.Find(filter)
                .SortByDescending(m => m.Timestamp)
                .Limit(limit)
                .ToListAsync();
        }

        public async Task<bool> MarkAsReadAsync(string messageId, string userId)
        {
            var filter = Builders<Message>.Filter.And(
                Builders<Message>.Filter.Eq(m => m.Id, messageId),
                Builders<Message>.Filter.Eq(m => m.RecipientId, userId)
            );

            var update = Builders<Message>.Update
                .Set(m => m.IsRead, true)
                .Set(m => m.Status, MessageStatus.Read);
                
            var result = await _messages.UpdateOneAsync(filter, update);
            
            // Invalidate cache
            await _cacheService.RemoveAsync($"message:{messageId}");
            
            return result.ModifiedCount > 0;
        }

        public async Task<bool> DeleteMessageAsync(string messageId, string userId)
        {
            var filter = Builders<Message>.Filter.And(
                Builders<Message>.Filter.Eq(m => m.Id, messageId),
                Builders<Message>.Filter.Or(
                    Builders<Message>.Filter.Eq(m => m.SenderId, userId),
                    Builders<Message>.Filter.Eq(m => m.RecipientId, userId)
                )
            );

            var update = Builders<Message>.Update.Set(m => m.IsDeleted, true);
            var result = await _messages.UpdateOneAsync(filter, update);
            
            // Invalidate cache
            await _cacheService.RemoveAsync($"message:{messageId}");
            
            return result.ModifiedCount > 0;
        }

        public async Task<bool> EditMessageAsync(string messageId, string userId, string newContent)
        {
            var filter = Builders<Message>.Filter.And(
                Builders<Message>.Filter.Eq(m => m.Id, messageId),
                Builders<Message>.Filter.Eq(m => m.SenderId, userId)
            );

            var update = Builders<Message>.Update
                .Set(m => m.Content, newContent)
                .Set(m => m.EditedAt, DateTime.UtcNow);
                
            var result = await _messages.UpdateOneAsync(filter, update);
            
            // Invalidate cache
            await _cacheService.RemoveAsync($"message:{messageId}");
            
            return result.ModifiedCount > 0;
        }

        public async Task<IEnumerable<Message>> SearchMessagesAsync(string userId, string searchTerm)
        {
            var filter = Builders<Message>.Filter.And(
                Builders<Message>.Filter.Text(searchTerm),
                Builders<Message>.Filter.Or(
                    Builders<Message>.Filter.Eq(m => m.SenderId, userId),
                    Builders<Message>.Filter.Eq(m => m.RecipientId, userId)
                ),
                Builders<Message>.Filter.Eq(m => m.IsDeleted, false)
            );

            return await _messages.Find(filter)
                .SortByDescending(m => m.Timestamp)
                .Limit(100)
                .ToListAsync();
        }

        public async Task<bool> AddReactionAsync(string messageId, string userId, string emoji)
        {
            var reaction = new MessageReaction
            {
                UserId = userId,
                Emoji = emoji,
                ReactedAt = DateTime.UtcNow
            };

            var update = Builders<Message>.Update.Push(m => m.Reactions, reaction);
            var result = await _messages.UpdateOneAsync(m => m.Id == messageId, update);
            
            // Invalidate cache
            await _cacheService.RemoveAsync($"message:{messageId}");
            
            return result.ModifiedCount > 0;
        }

        public async Task<bool> RemoveReactionAsync(string messageId, string userId, string emoji)
        {
            var update = Builders<Message>.Update.PullFilter(m => m.Reactions,
                r => r.UserId == userId && r.Emoji == emoji);
            var result = await _messages.UpdateOneAsync(m => m.Id == messageId, update);
            
            // Invalidate cache
            await _cacheService.RemoveAsync($"message:{messageId}");
            
            return result.ModifiedCount > 0;
        }

        private string GetConversationCacheKey(string userId1, string userId2)
        {
            var users = new[] { userId1, userId2 }.OrderBy(u => u).ToArray();
            return $"conversation:{users[0]}:{users[1]}";
        }
    }
}