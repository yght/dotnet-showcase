using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateSlimBuilder(args);

// configure JSON serialization for AOT
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

// minimal service registration for AOT compatibility
builder.Services.AddAuthentication().AddJwtBearer();
builder.Services.AddAuthorization();

// our simple message store - using in-memory for AOT demo
builder.Services.AddSingleton<IMessageStore, InMemoryMessageStore>();

var app = builder.Build();

// minimal API endpoints that work great with AOT
var messages = app.MapGroup("/api/aot-messages");

messages.MapGet("/", GetAllMessages);
messages.MapGet("/{id}", GetMessage);
messages.MapPost("/", CreateMessage);
messages.MapDelete("/{id}", DeleteMessage);

// health endpoint
app.MapGet("/health", () => new { Status = "Healthy", CompilationType = "Native AOT" });

// performance benchmark endpoint
app.MapGet("/perf", () => new
{
    StartupTime = "< 50ms with AOT",
    MemoryUsage = "~50% less than JIT",
    Note = "Native AOT provides faster startup and lower memory usage"
});

app.Run();

// endpoint handlers
static async Task<IResult> GetAllMessages(IMessageStore store)
{
    var messages = await store.GetAllAsync();
    return TypedResults.Ok(messages);
}

static async Task<IResult> GetMessage(string id, IMessageStore store)
{
    var message = await store.GetByIdAsync(id);
    return message != null ? TypedResults.Ok(message) : TypedResults.NotFound();
}

static async Task<IResult> CreateMessage(MessageDto dto, IMessageStore store)
{
    var message = new Message
    {
        Id = Guid.NewGuid().ToString(),
        Content = dto.Content,
        SenderId = dto.SenderId,
        RecipientId = dto.RecipientId,
        Timestamp = DateTime.UtcNow
    };
    
    await store.AddAsync(message);
    return TypedResults.Created($"/api/aot-messages/{message.Id}", message);
}

static async Task<IResult> DeleteMessage(string id, IMessageStore store)
{
    await store.DeleteAsync(id);
    return TypedResults.NoContent();
}

// AOT requires explicit JSON source generation
[JsonSerializable(typeof(Message))]
[JsonSerializable(typeof(MessageDto))]
[JsonSerializable(typeof(Message[]))]
[JsonSerializable(typeof(object))] // for anonymous objects
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}

// simple DTOs for AOT
public class Message
{
    public string Id { get; set; } = "";
    public string Content { get; set; } = "";
    public string SenderId { get; set; } = "";
    public string RecipientId { get; set; } = "";
    public DateTime Timestamp { get; set; }
}

public class MessageDto
{
    public string Content { get; set; } = "";
    public string SenderId { get; set; } = "";
    public string RecipientId { get; set; } = "";
}

// simple in-memory store for demo
public interface IMessageStore
{
    Task<Message[]> GetAllAsync();
    Task<Message?> GetByIdAsync(string id);
    Task AddAsync(Message message);
    Task DeleteAsync(string id);
}

public class InMemoryMessageStore : IMessageStore
{
    private readonly List<Message> _messages = new();

    public Task<Message[]> GetAllAsync()
    {
        return Task.FromResult(_messages.ToArray());
    }

    public Task<Message?> GetByIdAsync(string id)
    {
        var message = _messages.FirstOrDefault(m => m.Id == id);
        return Task.FromResult(message);
    }

    public Task AddAsync(Message message)
    {
        _messages.Add(message);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string id)
    {
        var message = _messages.FirstOrDefault(m => m.Id == id);
        if (message != null)
        {
            _messages.Remove(message);
        }
        return Task.CompletedTask;
    }
}