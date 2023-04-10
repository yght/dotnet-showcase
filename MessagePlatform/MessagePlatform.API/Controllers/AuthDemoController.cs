using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MessagePlatform.Infrastructure.Auth;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace MessagePlatform.API.Controllers;

[ApiController]
[Route("api/auth-demo")]
[Authorize] // all endpoints require authentication
public class AuthDemoController : ControllerBase
{
    private readonly IAuth0Service _auth0Service;
    private readonly IM2MTokenService _m2mTokenService;
    private readonly ILogger<AuthDemoController> _logger;

    public AuthDemoController(
        IAuth0Service auth0Service,
        IM2MTokenService m2mTokenService,
        ILogger<AuthDemoController> logger)
    {
        _auth0Service = auth0Service;
        _m2mTokenService = m2mTokenService;
        _logger = logger;
    }

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        if (string.IsNullOrEmpty(userId))
        {
            return BadRequest("User ID not found");
        }

        try
        {
            var userProfile = await _auth0Service.GetUserAsync(userId);
            return Ok(userProfile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get profile for user {UserId}", userId);
            return StatusCode(500, "Failed to retrieve profile");
        }
    }

    [HttpPost("update-preferences")]
    public async Task<IActionResult> UpdatePreferences([FromBody] UserPreferencesRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        if (string.IsNullOrEmpty(userId))
        {
            return BadRequest("User ID not found");
        }

        try
        {
            var metadata = new
            {
                preferences = new
                {
                    theme = request.Theme,
                    notifications = request.NotificationsEnabled,
                    language = request.Language,
                    updated_at = DateTime.UtcNow
                }
            };

            await _auth0Service.UpdateUserMetadataAsync(userId, metadata);
            
            return Ok(new { Message = "Preferences updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update preferences for user {UserId}", userId);
            return StatusCode(500, "Failed to update preferences");
        }
    }

    [HttpPost("admin/create-user")]
    [Authorize(Policy = "RequireUserManagement")] // custom policy
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        try
        {
            var newUser = await _auth0Service.CreateUserAsync(request);
            
            _logger.LogInformation("Admin created user {UserId} with email {Email}", 
                newUser.UserId, newUser.Email);
            
            return CreatedAtAction(nameof(GetProfile), new { id = newUser.UserId }, newUser);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create user with email {Email}", request.Email);
            return StatusCode(500, "Failed to create user");
        }
    }

    [HttpPost("admin/assign-role")]
    [Authorize(Policy = "RequireUserManagement")]
    public async Task<IActionResult> AssignRole([FromBody] AssignRoleRequest request)
    {
        try
        {
            await _auth0Service.AssignRoleToUserAsync(request.UserId, request.RoleId);
            
            return Ok(new { Message = "Role assigned successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to assign role {RoleId} to user {UserId}", 
                request.RoleId, request.UserId);
            return StatusCode(500, "Failed to assign role");
        }
    }

    [HttpPost("admin/block-user")]
    [Authorize(Policy = "RequireUserManagement")]
    public async Task<IActionResult> BlockUser([FromBody] BlockUserRequest request)
    {
        try
        {
            await _auth0Service.BlockUserAsync(request.UserId, request.Block);
            
            var action = request.Block ? "blocked" : "unblocked";
            _logger.LogWarning("User {UserId} has been {Action} by admin {AdminId}", 
                request.UserId, action, User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            
            return Ok(new { Message = $"User {action} successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to block/unblock user {UserId}", request.UserId);
            return StatusCode(500, "Failed to update user status");
        }
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous] // this endpoint doesn't require auth
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        try
        {
            await _auth0Service.SendPasswordResetEmailAsync(request.Email);
            
            // don't reveal if email exists or not - security best practice
            return Ok(new { Message = "If account exists, password reset email will be sent" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password reset email");
            // still return success to prevent email enumeration
            return Ok(new { Message = "If account exists, password reset email will be sent" });
        }
    }

    [HttpGet("service-token")]
    [Authorize(Policy = "RequireServiceAccess")]
    public async Task<IActionResult> GetServiceToken([FromQuery] string audience, [FromQuery] string scopes)
    {
        try
        {
            var scopeArray = scopes.Split(',', StringSplitOptions.RemoveEmptyEntries);
            var token = await _m2mTokenService.GetAccessTokenAsync(audience, scopeArray);
            
            return Ok(new { AccessToken = token });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get service token for audience {Audience}", audience);
            return StatusCode(500, "Failed to get service token");
        }
    }

    // endpoint that requires specific permission
    [HttpGet("protected-resource")]
    [Authorize(Policy = "RequireReadMessages")]
    public IActionResult GetProtectedResource()
    {
        return Ok(new
        {
            Message = "You have access to this protected resource!",
            UserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
            Permissions = User.FindAll("permissions").Select(c => c.Value).ToArray()
        });
    }
}

public record UserPreferencesRequest(
    string Theme,
    bool NotificationsEnabled,
    string Language
);

public record AssignRoleRequest(
    string UserId,
    string RoleId
);

public record BlockUserRequest(
    string UserId,
    bool Block
);

public record ForgotPasswordRequest(
    string Email
);