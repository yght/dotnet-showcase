using Carter;
using MessagePlatform.Core.Entities;
using MessagePlatform.Core.Interfaces;

namespace MessagePlatform.API.MinimalApis;

// Carter makes minimal APIs much cleaner than default approach
public class MessageModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/fast/messages").WithTags("Fast Messages");

        // GET endpoint - super clean syntax
        group.MapGet("/", GetAllMessages);
        group.MapGet("/{id}", GetMessageById);
        group.MapGet("/user/{userId}", GetUserMessages);
        
        // POST endpoints
        group.MapPost("/", CreateMessage);
        group.MapPost("/bulk", CreateBulkMessages);
        
        // PUT/DELETE
        group.MapPut("/{id}", UpdateMessage);
        group.MapDelete("/{id}", DeleteMessage);
    }

    // individual handler methods - much cleaner than lambdas
    private static async Task<IResult> GetAllMessages(IRepository<Message> repo, int skip = 0, int take = 20)
    {
        var messages = await repo.GetAllAsync();
        var pagedMessages = messages.Skip(skip).Take(take);
        
        return Results.Ok(new { 
            Messages = pagedMessages, 
            Count = pagedMessages.Count(),
            Note = "Fast endpoint using Carter minimal APIs" 
        });
    }

    private static async Task<IResult> GetMessageById(string id, IRepository<Message> repo)
    {
        var message = await repo.GetByIdAsync(id);
        
        if (message == null)
            return Results.NotFound($"Message {id} not found");
            
        return Results.Ok(message);
    }

    private static async Task<IResult> GetUserMessages(
        string userId, 
        IRepository<Message> repo,
        int skip = 0,
        int take = 50)
    {
        // this would normally be cached but keeping it simple
        var userMessages = await repo.FindAsync(m => 
            m.SenderId == userId || m.RecipientId == userId);
            
        var result = userMessages
            .OrderByDescending(m => m.Timestamp)
            .Skip(skip)
            .Take(take);
            
        return Results.Ok(result);
    }

    private static async Task<IResult> CreateMessage(
        CreateMessageRequest request, 
        IRepository<Message> repo)
    {
        var message = new Message
        {
            Id = Guid.NewGuid().ToString(),
            SenderId = request.SenderId,
            RecipientId = request.RecipientId,
            Content = request.Content,
            GroupId = request.GroupId,
            Timestamp = DateTime.UtcNow,
            Status = MessageStatus.Sent
        };

        var created = await repo.AddAsync(message);
        return Results.Created($"/api/fast/messages/{created.Id}", created);
    }

    private static async Task<IResult> CreateBulkMessages(
        BulkMessageRequest request,
        IRepository<Message> repo)
    {
        var messages = new List<Message>();
        
        foreach (var msg in request.Messages)
        {
            var message = new Message
            {
                Id = Guid.NewGuid().ToString(),
                SenderId = msg.SenderId,
                RecipientId = msg.RecipientId,
                Content = msg.Content,
                Timestamp = DateTime.UtcNow,
                Status = MessageStatus.Sent
            };
            
            messages.Add(await repo.AddAsync(message));
        }
        
        return Results.Ok(new { 
            Created = messages.Count, 
            MessageIds = messages.Select(m => m.Id).ToList() 
        });
    }

    private static async Task<IResult> UpdateMessage(
        string id,
        UpdateMessageRequest request,
        IRepository<Message> repo)
    {
        var message = await repo.GetByIdAsync(id);
        if (message == null)
            return Results.NotFound();

        message.Content = request.Content;
        message.EditedAt = DateTime.UtcNow;
        
        await repo.UpdateAsync(message);
        return Results.Ok(message);
    }

    private static async Task<IResult> DeleteMessage(string id, IRepository<Message> repo)
    {
        await repo.DeleteAsync(id);
        return Results.NoContent();
    }
}

// request models - keeping them simple
public record CreateMessageRequest(
    string SenderId,
    string RecipientId,
    string Content,
    string? GroupId = null
);

public record BulkMessageRequest(
    List<CreateMessageRequest> Messages
);

public record UpdateMessageRequest(string Content);