using System.Text.Json;
using EventStore.ClientAPI;

namespace MessagePlatform.Infrastructure.EventSourcing;

public interface IEventStore
{
    Task AppendEventsAsync(string streamId, IEnumerable<DomainEvent> events, int expectedVersion);
    Task<IEnumerable<DomainEvent>> GetEventsAsync(string streamId, int fromVersion = 0);
    Task<T?> GetAggregateAsync<T>(string aggregateId) where T : class, new();
}

public class EventStoreService : IEventStore
{
    private readonly IEventStoreConnection _connection;
    private readonly JsonSerializerOptions _jsonOptions;

    public EventStoreService(IEventStoreConnection connection)
    {
        _connection = connection;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public async Task AppendEventsAsync(string streamId, IEnumerable<DomainEvent> events, int expectedVersion)
    {
        var eventData = events.Select(e => new EventData(
            Guid.NewGuid(),
            e.GetType().Name,
            true,
            JsonSerializer.SerializeToUtf8Bytes(e, _jsonOptions),
            null
        )).ToArray();

        await _connection.AppendToStreamAsync(streamId, expectedVersion, eventData);
    }

    public async Task<IEnumerable<DomainEvent>> GetEventsAsync(string streamId, int fromVersion = 0)
    {
        var slice = await _connection.ReadStreamEventsForwardAsync(
            streamId, 
            fromVersion, 
            4096, 
            false);

        if (slice.Status == SliceReadStatus.StreamNotFound)
        {
            return Enumerable.Empty<DomainEvent>();
        }

        var events = new List<DomainEvent>();
        
        foreach (var resolvedEvent in slice.Events)
        {
            var eventType = Type.GetType($"MessagePlatform.Infrastructure.EventSourcing.{resolvedEvent.Event.EventType}");
            if (eventType != null)
            {
                var eventData = JsonSerializer.Deserialize(
                    resolvedEvent.Event.Data, 
                    eventType, 
                    _jsonOptions) as DomainEvent;
                
                if (eventData != null)
                {
                    events.Add(eventData);
                }
            }
        }

        return events;
    }

    public async Task<T?> GetAggregateAsync<T>(string aggregateId) where T : class, new()
    {
        var events = await GetEventsAsync($"{typeof(T).Name}-{aggregateId}");
        
        if (!events.Any())
        {
            return null;
        }

        var aggregate = new T();
        
        // Apply events to rebuild aggregate state
        foreach (var @event in events.OrderBy(e => e.Version))
        {
            ApplyEvent(aggregate, @event);
        }

        return aggregate;
    }

    private static void ApplyEvent<T>(T aggregate, DomainEvent @event)
    {
        var method = aggregate!.GetType().GetMethod($"Apply", new[] { @event.GetType() });
        method?.Invoke(aggregate, new object[] { @event });
    }
}