using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;

namespace MessagePlatform.Infrastructure.Outbox;

// concrete implementation for Azure Service Bus
public class ServiceBusPublisher : IMessagePublisher, IAsyncDisposable
{
    private readonly ServiceBusClient _client;
    private readonly ServiceBusSender _sender;
    private readonly ILogger<ServiceBusPublisher> _logger;

    public ServiceBusPublisher(ServiceBusClient client, ILogger<ServiceBusPublisher> logger)
    {
        _client = client;
        _sender = _client.CreateSender("message-events"); // topic name
        _logger = logger;
    }

    public async Task PublishAsync(string eventType, string payload, string partitionKey = null)
    {
        var message = new ServiceBusMessage(payload)
        {
            Subject = eventType, // helps with filtering
            ContentType = "application/json",
            MessageId = Guid.NewGuid().ToString()
        };

        // partition key helps with ordering and scaling
        if (!string.IsNullOrEmpty(partitionKey))
        {
            message.PartitionKey = partitionKey;
        }

        // add some useful properties
        message.ApplicationProperties["EventType"] = eventType;
        message.ApplicationProperties["PublishedAt"] = DateTime.UtcNow;
        
        await _sender.SendMessageAsync(message);
        
        _logger.LogDebug("Published message {MessageId} of type {EventType} to Service Bus", 
            message.MessageId, eventType);
    }

    public async ValueTask DisposeAsync()
    {
        await _sender.DisposeAsync();
        await _client.DisposeAsync();
    }
}