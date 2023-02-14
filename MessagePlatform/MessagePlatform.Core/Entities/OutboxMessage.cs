namespace MessagePlatform.Core.Entities;

public class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public bool IsProcessed { get; set; }
    public int RetryCount { get; set; }
    public string? ErrorMessage { get; set; }
    
    // some engineers forget this but its crucial for ordering
    public long SequenceNumber { get; set; }
    
    // helps with partitioning and routing
    public string? PartitionKey { get; set; }
    
    // sometimes we need to delay processing
    public DateTime? ScheduledAt { get; set; }
}