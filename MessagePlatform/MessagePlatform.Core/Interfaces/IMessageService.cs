using MessagePlatform.Core.Entities;

namespace MessagePlatform.Core.Interfaces
{
    public interface IMessageService
    {
        Task<Message> SaveMessageAsync(Message message);
        Task<Message?> GetMessageAsync(string messageId);
        Task<IEnumerable<Message>> GetMessagesAsync(string userId1, string userId2, int limit = 50, DateTime? before = null);
        Task<IEnumerable<Message>> GetGroupMessagesAsync(string groupId, int limit = 50, DateTime? before = null);
        Task<bool> MarkAsReadAsync(string messageId, string userId);
        Task<bool> DeleteMessageAsync(string messageId, string userId);
        Task<bool> EditMessageAsync(string messageId, string userId, string newContent);
        Task<IEnumerable<Message>> SearchMessagesAsync(string userId, string searchTerm);
        Task<bool> AddReactionAsync(string messageId, string userId, string emoji);
        Task<bool> RemoveReactionAsync(string messageId, string userId, string emoji);
    }

    public interface IUserService
    {
        Task<User?> GetUserAsync(string userId);
        Task<User> CreateUserAsync(User user);
        Task<bool> UpdateUserStatusAsync(string userId, UserStatus status);
        Task<bool> UpdateLastSeenAsync(string userId);
        Task<IEnumerable<User>> SearchUsersAsync(string searchTerm);
        Task<bool> UpdateUserProfileAsync(string userId, string displayName, string? profilePictureUrl);
        Task<bool> UpdateUserPreferencesAsync(string userId, UserPreferences preferences);
        Task<IEnumerable<User>> GetContactsAsync(string userId);
        Task<bool> BlockUserAsync(string userId, string blockedUserId);
        Task<bool> UnblockUserAsync(string userId, string blockedUserId);
    }

    public interface IGroupService
    {
        Task<Group> CreateGroupAsync(Group group);
        Task<Group?> GetGroupAsync(string groupId);
        Task<bool> AddMemberAsync(string groupId, string userId);
        Task<bool> RemoveMemberAsync(string groupId, string userId);
        Task<IEnumerable<Group>> GetUserGroupsAsync(string userId);
        Task<bool> UpdateGroupInfoAsync(string groupId, string name, string description);
        Task<bool> PromoteToAdminAsync(string groupId, string userId);
        Task<bool> DemoteFromAdminAsync(string groupId, string userId);
        Task<bool> UpdateGroupSettingsAsync(string groupId, GroupSettings settings);
        Task<IEnumerable<User>> GetGroupMembersAsync(string groupId);
    }

    public interface IUserConnectionService
    {
        Task AddConnectionAsync(string userId, string connectionId);
        Task RemoveConnectionAsync(string userId, string connectionId);
        Task<IEnumerable<string>> GetConnectionsAsync(string userId);
        Task<bool> IsUserOnlineAsync(string userId);
        Task<Dictionary<string, bool>> GetOnlineStatusAsync(IEnumerable<string> userIds);
    }

    public interface INotificationService
    {
        Task SendPushNotificationAsync(string userId, string title, string message, Dictionary<string, string>? data = null);
        Task SendEmailNotificationAsync(string email, string subject, string body);
        Task SendGroupNotificationAsync(string groupId, string title, string message, string? excludeUserId = null);
    }

    public interface IFileStorageService
    {
        Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType);
        Task<bool> DeleteFileAsync(string fileUrl);
        Task<string> GenerateThumbnailAsync(string fileUrl);
        Task<Stream> DownloadFileAsync(string fileUrl);
    }

    public interface ICacheService
    {
        Task<T?> GetAsync<T>(string key);
        Task SetAsync<T>(string key, T value, TimeSpan? expiry = null);
        Task RemoveAsync(string key);
        Task<bool> ExistsAsync(string key);
    }
}