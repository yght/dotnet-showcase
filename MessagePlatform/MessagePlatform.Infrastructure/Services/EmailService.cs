using System;
using System.Threading.Tasks;
using SendGrid;
using SendGrid.Helpers.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MessagePlatform.Infrastructure.Services
{
    public class EmailService
    {
        private readonly ISendGridClient _sendGridClient;
        private readonly ILogger<EmailService> _logger;
        private readonly string _fromEmail;
        private readonly string _fromName;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            var apiKey = configuration["SendGrid:ApiKey"];
            _sendGridClient = new SendGridClient(apiKey);
            _fromEmail = configuration["SendGrid:FromEmail"];
            _fromName = configuration["SendGrid:FromName"];
            _logger = logger;
        }

        public async Task SendWelcomeEmailAsync(string toEmail, string userName)
        {
            var msg = new SendGridMessage
            {
                From = new EmailAddress(_fromEmail, _fromName),
                Subject = "Welcome to MessagePlatform"
            };

            msg.AddTo(new EmailAddress(toEmail, userName));
            msg.PlainTextContent = $"Welcome {userName}! Thanks for joining MessagePlatform.";
            msg.HtmlContent = $"<h1>Welcome {userName}!</h1><p>Thanks for joining MessagePlatform.</p>";

            var response = await _sendGridClient.SendEmailAsync(msg);
            
            if (response.StatusCode != System.Net.HttpStatusCode.Accepted)
            {
                _logger.LogError($"Failed to send welcome email to {toEmail}. Status: {response.StatusCode}");
            }
        }

        public async Task SendPasswordResetEmailAsync(string toEmail, string resetToken)
        {
            var msg = new SendGridMessage
            {
                From = new EmailAddress(_fromEmail, _fromName),
                Subject = "Password Reset Request"
            };

            msg.AddTo(new EmailAddress(toEmail));
            var resetLink = $"https://messageplatform.com/reset-password?token={resetToken}";
            msg.PlainTextContent = $"Click the following link to reset your password: {resetLink}";
            msg.HtmlContent = $"<p>Click <a href='{resetLink}'>here</a> to reset your password.</p>";

            await _sendGridClient.SendEmailAsync(msg);
        }
    }
}