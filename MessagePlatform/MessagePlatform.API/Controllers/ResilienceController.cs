using Microsoft.AspNetCore.Mvc;
using MessagePlatform.Infrastructure.Resilience;
using MessagePlatform.Core.Entities;
using Microsoft.Extensions.Logging;

namespace MessagePlatform.API.Controllers;

[ApiController]
[Route("api/resilience")]
public class ResilienceController : ControllerBase
{
    private readonly IExternalApiService _externalApi;
    private readonly ResilientDatabaseService _databaseService;
    private readonly ILogger<ResilienceController> _logger;

    public ResilienceController(
        IExternalApiService externalApi,
        ResilientDatabaseService databaseService,
        ILogger<ResilienceController> logger)
    {
        _externalApi = externalApi;
        _databaseService = databaseService;
        _logger = logger;
    }

    [HttpGet("user/{userId}/profile")]
    public async Task<IActionResult> GetUserProfile(string userId)
    {
        try
        {
            // this will use circuit breaker and retry logic
            var profile = await _externalApi.GetUserProfileAsync(userId);
            
            return Ok(new
            {
                Profile = profile,
                Source = "External API",
                Note = "This call is protected by circuit breaker and retry policies"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get user profile for {UserId}", userId);
            return StatusCode(500, "Service temporarily unavailable");
        }
    }

    [HttpPost("user/{userId}/notify")]
    public async Task<IActionResult> SendNotification(string userId, [FromBody] NotificationRequest request)
    {
        var success = await _externalApi.SendNotificationAsync(userId, request.Message);
        
        if (success)
        {
            return Ok(new { Status = "Sent", Message = "Notification delivered successfully" });
        }
        else
        {
            return Accepted(new { 
                Status = "Queued", 
                Message = "Service unavailable - notification queued for retry" 
            });
        }
    }

    [HttpGet("user/{userId}/recommendations")]
    public async Task<IActionResult> GetRecommendations(string userId)
    {
        // this has fallback logic built in
        var recommendations = await _externalApi.GetRecommendationsAsync(userId);
        
        return Ok(new
        {
            Recommendations = recommendations,
            Note = "Falls back to default recommendations if service is down"
        });
    }

    [HttpGet("messages/{messageId}")]
    public async Task<IActionResult> GetMessage(string messageId)
    {
        var message = await _databaseService.GetMessageSafelyAsync(messageId);
        
        if (message == null)
        {
            return NotFound("Message not found or database unavailable");
        }
        
        return Ok(message);
    }

    [HttpPost("messages")]
    public async Task<IActionResult> CreateMessage([FromBody] CreateMessageRequest request)
    {
        var message = new Message
        {
            Id = Guid.NewGuid().ToString(),
            SenderId = request.SenderId,
            RecipientId = request.RecipientId,
            Content = request.Content,
            Timestamp = DateTime.UtcNow,
            Status = MessageStatus.Sent
        };

        var saved = await _databaseService.SaveMessageSafelyAsync(message);
        
        if (saved)
        {
            return CreatedAtAction(nameof(GetMessage), new { messageId = message.Id }, message);
        }
        else
        {
            return Accepted(new 
            { 
                Message = "Message saved to fallback storage - will be persisted when database recovers",
                MessageId = message.Id
            });
        }
    }

    [HttpGet("health")]
    public async Task<IActionResult> CheckHealth()
    {
        var dbHealthy = await _databaseService.IsDatabaseHealthyAsync();
        
        return Ok(new
        {
            DatabaseHealthy = dbHealthy,
            Status = dbHealthy ? "Healthy" : "Degraded",
            Timestamp = DateTime.UtcNow,
            Note = "This endpoint checks if resilience policies are working"
        });
    }

    [HttpGet("chaos")]
    public IActionResult SimulateChaos([FromQuery] string type = "timeout")
    {
        // this endpoint simulates failures for testing circuit breakers
        return type.ToLower() switch
        {
            "timeout" => StatusCode(408, "Request timeout simulation"),
            "error" => StatusCode(500, "Internal server error simulation"),
            "unavailable" => StatusCode(503, "Service unavailable simulation"),
            _ => Ok("Use ?type=timeout|error|unavailable to simulate failures")
        };
    }
}

public record NotificationRequest(string Message);

public record CreateMessageRequest(
    string SenderId,
    string RecipientId,
    string Content
);