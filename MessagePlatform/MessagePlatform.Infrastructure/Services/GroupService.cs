using MessagePlatform.Core.Entities;
using MessagePlatform.Core.Interfaces;
using MongoDB.Driver;
using Microsoft.Extensions.Configuration;

namespace MessagePlatform.Infrastructure.Services
{
    public class GroupService : IGroupService
    {
        private readonly IMongoCollection<Group> _groups;

        public GroupService(IConfiguration configuration)
        {
            var client = new MongoClient(configuration.GetConnectionString("MongoDB"));
            var database = client.GetDatabase("MessagePlatform");
            _groups = database.GetCollection<Group>("groups");
        }

        public async Task<Group> CreateGroupAsync(Group group)
        {
            group.CreatedAt = DateTime.UtcNow;
            await _groups.InsertOneAsync(group);
            return group;
        }

        public async Task<Group> GetGroupAsync(string groupId)
        {
            return await _groups.Find(g => g.Id == groupId).FirstOrDefaultAsync();
        }

        public async Task<bool> AddMemberAsync(string groupId, string userId)
        {
            var filter = Builders<Group>.Filter.Eq(g => g.Id, groupId);
            var update = Builders<Group>.Update.AddToSet(g => g.MemberIds, userId);
            var result = await _groups.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> RemoveMemberAsync(string groupId, string userId)
        {
            var filter = Builders<Group>.Filter.Eq(g => g.Id, groupId);
            var update = Builders<Group>.Update.Pull(g => g.MemberIds, userId);
            var result = await _groups.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }

        public async Task<IEnumerable<Group>> GetUserGroupsAsync(string userId)
        {
            var filter = Builders<Group>.Filter.AnyEq(g => g.MemberIds, userId);
            return await _groups.Find(filter).ToListAsync();
        }
    }
}