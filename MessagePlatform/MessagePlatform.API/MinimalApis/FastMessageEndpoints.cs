using FastEndpoints;
using MessagePlatform.Core.Entities;
using MessagePlatform.Core.Interfaces;

namespace MessagePlatform.API.MinimalApis;

// FastEndpoints gives even better performance and features
public class GetMessagesEndpoint : Endpoint<GetMessagesRequest, GetMessagesResponse>
{
    public override void Configure()
    {
        Get("/api/ultra-fast/messages");
        AllowAnonymous(); // for demo purposes
        Description(d => d
            .WithName("GetMessagesFast")
            .WithSummary("Ultra-fast message retrieval using FastEndpoints"));
    }

    public override async Task HandleAsync(GetMessagesRequest req, CancellationToken ct)
    {
        var repo = Resolve<IRepository<Message>>();
        
        var messages = await repo.GetAllAsync();
        var filtered = messages.Skip(req.Skip).Take(req.Take);
        
        await SendOkAsync(new GetMessagesResponse
        {
            Messages = filtered.ToList(),
            Total = messages.Count(),
            Page = req.Skip / req.Take + 1,
            PerformanceNote = "Blazing fast with FastEndpoints!"
        }, ct);
    }
}

public class SendMessageEndpoint : Endpoint<SendMessageRequest, SendMessageResponse>
{
    public override void Configure()
    {
        Post("/api/ultra-fast/messages");
        AllowAnonymous();
        Description(d => d
            .WithName("SendMessageFast")
            .WithSummary("Send message with ultra-fast processing"));
    }

    public override async Task HandleAsync(SendMessageRequest req, CancellationToken ct)
    {
        var repo = Resolve<IRepository<Message>>();
        
        var message = new Message
        {
            Id = Guid.NewGuid().ToString(),
            SenderId = req.SenderId,
            RecipientId = req.RecipientId,
            Content = req.Content,
            Timestamp = DateTime.UtcNow,
            Status = MessageStatus.Sent
        };

        var created = await repo.AddAsync(message);
        
        await SendCreatedAtAsync<GetMessageEndpoint>(
            new { id = created.Id },
            new SendMessageResponse
            {
                MessageId = created.Id,
                Status = "sent",
                Timestamp = created.Timestamp
            }, 
            cancellation: ct);
    }
}

public class GetMessageEndpoint : Endpoint<GetMessageRequest, Message>
{
    public override void Configure()
    {
        Get("/api/ultra-fast/messages/{id}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetMessageRequest req, CancellationToken ct)
    {
        var repo = Resolve<IRepository<Message>>();
        var message = await repo.GetByIdAsync(req.Id);
        
        if (message == null)
            await SendNotFoundAsync(ct);
        else
            await SendOkAsync(message, ct);
    }
}

// batch endpoint - shows off FastEndpoints power
public class BatchProcessEndpoint : Endpoint<BatchRequest, BatchResponse>
{
    public override void Configure()
    {
        Post("/api/ultra-fast/batch");
        AllowAnonymous();
        Description(d => d
            .WithName("BatchProcess")
            .WithSummary("Process multiple operations in one request"));
    }

    public override async Task HandleAsync(BatchRequest req, CancellationToken ct)
    {
        var repo = Resolve<IRepository<Message>>();
        var results = new List<string>();
        
        foreach (var operation in req.Operations)
        {
            switch (operation.Type.ToLower())
            {
                case "create":
                    var message = new Message
                    {
                        Id = Guid.NewGuid().ToString(),
                        SenderId = operation.Data["senderId"]?.ToString() ?? "unknown",
                        RecipientId = operation.Data["recipientId"]?.ToString() ?? "unknown",
                        Content = operation.Data["content"]?.ToString() ?? "",
                        Timestamp = DateTime.UtcNow,
                        Status = MessageStatus.Sent
                    };
                    await repo.AddAsync(message);
                    results.Add($"Created message {message.Id}");
                    break;
                    
                case "delete":
                    var messageId = operation.Data["messageId"]?.ToString();
                    if (!string.IsNullOrEmpty(messageId))
                    {
                        await repo.DeleteAsync(messageId);
                        results.Add($"Deleted message {messageId}");
                    }
                    break;
                    
                default:
                    results.Add($"Unknown operation: {operation.Type}");
                    break;
            }
        }
        
        await SendOkAsync(new BatchResponse
        {
            ProcessedCount = req.Operations.Count,
            Results = results,
            ProcessingTimeMs = 42 // placeholder
        }, ct);
    }
}

// request/response models
public class GetMessagesRequest
{
    public int Skip { get; set; } = 0;
    public int Take { get; set; } = 20;
}

public class GetMessagesResponse
{
    public List<Message> Messages { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public string PerformanceNote { get; set; } = "";
}

public class SendMessageRequest
{
    public string SenderId { get; set; } = "";
    public string RecipientId { get; set; } = "";
    public string Content { get; set; } = "";
}

public class SendMessageResponse
{
    public string MessageId { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime Timestamp { get; set; }
}

public class GetMessageRequest
{
    public string Id { get; set; } = "";
}

public class BatchRequest
{
    public List<BatchOperation> Operations { get; set; } = new();
}

public class BatchOperation
{
    public string Type { get; set; } = "";
    public Dictionary<string, object> Data { get; set; } = new();
}

public class BatchResponse
{
    public int ProcessedCount { get; set; }
    public List<string> Results { get; set; } = new();
    public int ProcessingTimeMs { get; set; }
}