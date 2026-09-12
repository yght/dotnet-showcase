using System.Security.Claims;
using Messaging;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
builder.Services.AddAuthentication("Bearer").AddJwtBearer(options => options.MapInboundClaims = false);
builder.Services.AddAuthorizationBuilder().AddPolicy("messaging", policy => policy
    .RequireAuthenticatedUser()
    .RequireAssertion(context =>
        !string.IsNullOrWhiteSpace(context.User.FindFirstValue("sub")) &&
        !string.IsNullOrWhiteSpace(context.User.FindFirstValue("tenant_id"))));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(sp => new MessageStore(
    builder.Configuration.GetConnectionString("Messages") ?? "Data Source=messages.db;Default Timeout=10",
    sp.GetRequiredService<TimeProvider>()));
var app = builder.Build();
app.Services.GetRequiredService<MessageStore>().Initialize();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "alive" }));
var messages = app.MapGroup("/api/messages").RequireAuthorization("messaging");
messages.MapPost("/", (SendMessage input, ClaimsPrincipal user, MessageStore store) =>
{
    if (!ValidId(input.RecipientId) || !ValidId(input.ClientMessageId) ||
        string.IsNullOrWhiteSpace(input.Body) || input.Body.Length > 4000)
        return Results.Problem(statusCode: 400, title: "Invalid message",
            detail: "RecipientId and ClientMessageId must be 1–128 nonblank characters; Body must be 1–4000.");
    try
    {
        var result = store.Send(user.FindFirstValue("tenant_id")!, user.FindFirstValue("sub")!, input);
        return Results.Json(result, statusCode: result.Replayed ? 200 : 201);
    }
    catch (IdempotencyConflictException)
    {
        return Results.Problem(statusCode: 409, title: "Idempotency key conflict",
            detail: "This client message ID was already used for different content or a different recipient.");
    }
});
messages.MapGet("/conversations/{peer}", (string peer, long? after, int? limit,
    ClaimsPrincipal user, MessageStore store) =>
{
    var cursor = after ?? 0;
    var size = limit ?? 50;
    if (!ValidId(peer) || cursor < 0 || size is < 1 or > 100)
        return Results.Problem(statusCode: 400, title: "Invalid page",
            detail: "Use a nonnegative after cursor and a limit between 1 and 100.");
    return Results.Ok(store.Conversation(user.FindFirstValue("tenant_id")!,
        user.FindFirstValue("sub")!, peer, cursor, size));
});
app.Run();

static bool ValidId(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 128;
public partial class Program { }
