namespace MessagePlatform.API.DTOs
{
    public class MessageDto
    {
        public string Id { get; set; }
        public string SenderId { get; set; }
        public string RecipientId { get; set; }
        public string Content { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsRead { get; set; }
        public string? GroupId { get; set; }
        public string? ReplyToMessageId { get; set; }
        public List<AttachmentDto>? Attachments { get; set; }
    }

    public class SendMessageDto
    {
        public string RecipientId { get; set; }
        public string Content { get; set; }
        public string? ReplyToMessageId { get; set; }
    }

    public class SendGroupMessageDto
    {
        public string GroupId { get; set; }
        public string Content { get; set; }
    }

    public class AttachmentDto
    {
        public string Id { get; set; }
        public string FileName { get; set; }
        public string FileUrl { get; set; }
        public long FileSize { get; set; }
        public string ContentType { get; set; }
    }

    public class UserDto
    {
        public string Id { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string DisplayName { get; set; }
        public string? ProfilePictureUrl { get; set; }
        public DateTime LastSeen { get; set; }
        public bool IsOnline { get; set; }
        public string Status { get; set; }
    }

    public class GroupDto
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<string> MemberIds { get; set; }
        public List<string> AdminIds { get; set; }
        public string? GroupImageUrl { get; set; }
    }

    public class CreateGroupDto
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public List<string> InitialMemberIds { get; set; }
    }
}