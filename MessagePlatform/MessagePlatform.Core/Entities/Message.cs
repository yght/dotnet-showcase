using System;
using System.Collections.Generic;

namespace MessagePlatform.Core.Entities
{
    public class Message
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string SenderId { get; set; }
        public string RecipientId { get; set; }
        public string Content { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsRead { get; set; }
        public bool IsDeleted { get; set; }
        public string GroupId { get; set; }
        public string ReplyToMessageId { get; set; }
        public List<Attachment> Attachments { get; set; }
        public MessageStatus Status { get; set; } = MessageStatus.Sent;
        public DateTime EditedAt { get; set; }
        public List<MessageReaction> Reactions { get; set; }
    }

    public class Attachment
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string FileName { get; set; }
        public string FileUrl { get; set; }
        public long FileSize { get; set; }
        public string ContentType { get; set; }
        public string ThumbnailUrl { get; set; }
        public DateTime UploadedAt { get; set; }
    }

    public class MessageReaction
    {
        public string UserId { get; set; }
        public string Emoji { get; set; }
        public DateTime ReactedAt { get; set; }
    }

    public enum MessageStatus
    {
        Sent,
        Delivered,
        Read,
        Failed
    }

    public class User
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Username { get; set; }
        public string Email { get; set; }
        public string DisplayName { get; set; }
        public string ProfilePictureUrl { get; set; }
        public DateTime LastSeen { get; set; }
        public bool IsOnline { get; set; }
        public UserStatus Status { get; set; }
        public string StatusMessage { get; set; }
        public UserPreferences Preferences { get; set; } = new UserPreferences();
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class UserPreferences
    {
        public bool EmailNotifications { get; set; } = true;
        public bool PushNotifications { get; set; } = true;
        public bool ShowOnlineStatus { get; set; } = true;
        public bool ShowReadReceipts { get; set; } = true;
        public string Theme { get; set; } = "light";
        public string Language { get; set; } = "en";
    }

    public enum UserStatus
    {
        Available,
        Busy,
        DoNotDisturb,
        Away,
        Invisible,
        Offline
    }

    public class Group
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Description { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<string> MemberIds { get; set; } = new List<string>();
        public List<string> AdminIds { get; set; } = new List<string>();
        public string GroupImageUrl { get; set; }
        public bool IsPublic { get; set; }
        public GroupSettings Settings { get; set; } = new GroupSettings();
        public DateTime LastActivityAt { get; set; }
    }

    public class GroupSettings
    {
        public bool AllowMembersToAddOthers { get; set; } = true;
        public bool AllowMembersToChangeGroupInfo { get; set; } = false;
        public bool MuteNotifications { get; set; } = false;
        public int MaxMembers { get; set; } = 1000;
    }
}