using MessagePlatform.Core.Interfaces;
using Microsoft.Azure.ServiceBus;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;

namespace MessagePlatform.Infrastructure.Services
{
    public class NotificationService : INotificationService
    {
        private readonly string _serviceBusConnectionString;
        private readonly IQueueClient _queueClient;

        public NotificationService(IConfiguration configuration)
        {
            _serviceBusConnectionString = configuration.GetConnectionString("ServiceBus");
            _queueClient = new QueueClient(_serviceBusConnectionString, "notifications");
        }

        public async Task SendPushNotificationAsync(string userId, string title, string message)
        {
            var notification = new
            {
                UserId = userId,
                Title = title,
                Body = message,
                Type = "push",
                Timestamp = DateTime.UtcNow
            };

            var messageBody = JsonSerializer.Serialize(notification);
            var serviceBusMessage = new Message(Encoding.UTF8.GetBytes(messageBody))
            {
                ContentType = "application/json"
            };

            await _queueClient.SendAsync(serviceBusMessage);
        }

        public async Task SendEmailNotificationAsync(string email, string subject, string body)
        {
            var emailNotification = new
            {
                To = email,
                Subject = subject,
                Body = body,
                Type = "email",
                Timestamp = DateTime.UtcNow
            };

            var messageBody = JsonSerializer.Serialize(emailNotification);
            var serviceBusMessage = new Message(Encoding.UTF8.GetBytes(messageBody))
            {
                ContentType = "application/json"
            };

            await _queueClient.SendAsync(serviceBusMessage);
        }
    }
}