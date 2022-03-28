using MessagePlatform.Infrastructure;
using MessagePlatform.API.GraphQL;
using MessagePlatform.API.Hubs;
using MediatR;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using EventStore.ClientAPI;
using MessagePlatform.Infrastructure.EventSourcing;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add infrastructure services
builder.Services.AddInfrastructureServices(builder.Configuration);

// Add MediatR
builder.Services.AddMediatR(typeof(Program).Assembly, typeof(MessagePlatform.Core.Commands.CreateMessageCommand).Assembly);

// Add GraphQL
builder.Services
    .AddGraphQLServer()
    .AddQueryType(d => d.Name("Query"))
    .AddMutationType(d => d.Name("Mutation"))
    .AddTypeExtension<MessageQueries>()
    .AddTypeExtension<MessageMutations>()
    .AddProjections()
    .AddFiltering()
    .AddSorting();

// Add SignalR with Redis backplane
builder.Services.AddSignalR()
    .AddStackExchangeRedis(builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379");

// Add EventStore
var eventStoreConnection = EventStoreConnection.Create(
    builder.Configuration.GetConnectionString("EventStore") ?? "ConnectTo=tcp://admin:changeit@localhost:1113");
await eventStoreConnection.ConnectAsync();
builder.Services.AddSingleton(eventStoreConnection);
builder.Services.AddScoped<IEventStore, EventStoreService>();

// Add OpenTelemetry
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .SetResourceBuilder(ResourceBuilder.CreateDefault()
            .AddService("MessagePlatform.API"))
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddConsoleExporter());

// Add health checks
builder.Services.AddHealthChecks()
    .AddSqlServer(builder.Configuration.GetConnectionString("SqlServer") ?? "Server=(localdb)\\mssqllocaldb;Database=MessagePlatform;Trusted_Connection=True;")
    .AddRedis(builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379");

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGraphQL();
app.MapHub<MessageHub>("/messageHub");
app.MapHealthChecks("/health");

await app.RunAsync();