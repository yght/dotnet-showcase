using Auth0.ManagementApi;
using Auth0.ManagementApi.Models;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace MessagePlatform.Infrastructure.Auth;

public class Auth0Settings
{
    public string Domain { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string ManagementApiClientId { get; set; } = string.Empty;
    public string ManagementApiClientSecret { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
}

public interface IAuth0Service
{
    Task<Auth0User> GetUserAsync(string userId);
    Task<Auth0User> CreateUserAsync(CreateUserRequest request);
    Task UpdateUserMetadataAsync(string userId, object metadata);
    Task AssignRoleToUserAsync(string userId, string roleId);
    Task<bool> HasPermissionAsync(string userId, string permission);
    Task SendPasswordResetEmailAsync(string email);
    Task BlockUserAsync(string userId, bool blocked = true);
}

// this shows real-world Auth0 integration patterns
public class Auth0Service : IAuth0Service
{
    private readonly ManagementApiClient _managementClient;
    private readonly Auth0Settings _settings;
    private readonly ILogger<Auth0Service> _logger;
    private readonly ConcurrentDictionary<string, Auth0User> _userCache;
    private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(15);

    public Auth0Service(
        IOptions<Auth0Settings> settings,
        ILogger<Auth0Service> logger)
    {
        _settings = settings.Value;
        _logger = logger;
        _userCache = new ConcurrentDictionary<string, Auth0User>();
        
        // initialize management client
        _managementClient = new ManagementApiClient(
            _settings.ManagementApiClientSecret,
            _settings.Domain);
    }

    public async Task<Auth0User> GetUserAsync(string userId)
    {
        // check cache first - performance optimization
        if (_userCache.TryGetValue(userId, out var cachedUser))
        {
            return cachedUser;
        }

        try
        {
            var user = await _managementClient.Users.GetAsync(userId);
            
            var auth0User = new Auth0User
            {
                UserId = user.UserId,
                Email = user.Email,
                Name = user.Name,
                Picture = user.Picture,
                EmailVerified = user.EmailVerified ?? false,
                LastLogin = user.LastLogin,
                CreatedAt = user.CreatedAt,
                Metadata = user.UserMetadata,
                AppMetadata = user.AppMetadata
            };

            // cache for next time
            _userCache.TryAdd(userId, auth0User);
            
            return auth0User;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get user {UserId} from Auth0", userId);
            throw;
        }
    }

    public async Task<Auth0User> CreateUserAsync(CreateUserRequest request)
    {
        try
        {
            var userCreateRequest = new UserCreateRequest
            {
                Email = request.Email,
                Password = request.Password,
                Connection = "Username-Password-Authentication", // default connection
                EmailVerified = false,
                Name = request.Name,
                UserMetadata = new
                {
                    created_from = "message_platform",
                    registration_source = request.Source ?? "web",
                    preferences = new
                    {
                        theme = "light",
                        notifications = true
                    }
                }
            };

            var createdUser = await _managementClient.Users.CreateAsync(userCreateRequest);
            
            _logger.LogInformation("Created new user {UserId} with email {Email}", 
                createdUser.UserId, createdUser.Email);

            return new Auth0User
            {
                UserId = createdUser.UserId,
                Email = createdUser.Email,
                Name = createdUser.Name,
                EmailVerified = createdUser.EmailVerified ?? false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create user with email {Email}", request.Email);
            throw;
        }
    }

    public async Task UpdateUserMetadataAsync(string userId, object metadata)
    {
        try
        {
            var updateRequest = new UserUpdateRequest
            {
                UserMetadata = metadata
            };

            await _managementClient.Users.UpdateAsync(userId, updateRequest);
            
            // invalidate cache
            _userCache.TryRemove(userId, out _);
            
            _logger.LogDebug("Updated metadata for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update metadata for user {UserId}", userId);
            throw;
        }
    }

    public async Task AssignRoleToUserAsync(string userId, string roleId)
    {
        try
        {
            var assignRolesRequest = new AssignRolesRequest
            {
                Roles = new[] { roleId }
            };

            await _managementClient.Users.AssignRolesAsync(userId, assignRolesRequest);
            
            _logger.LogInformation("Assigned role {RoleId} to user {UserId}", roleId, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to assign role {RoleId} to user {UserId}", roleId, userId);
            throw;
        }
    }

    public async Task<bool> HasPermissionAsync(string userId, string permission)
    {
        try
        {
            var userPermissions = await _managementClient.Users.GetPermissionsAsync(userId);
            return userPermissions.Any(p => p.PermissionName == permission);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check permission {Permission} for user {UserId}", 
                permission, userId);
            return false;
        }
    }

    public async Task SendPasswordResetEmailAsync(string email)
    {
        try
        {
            var request = new PasswordChangeTicketRequest
            {
                Email = email,
                ResultUrl = "https://your-app.com/password-reset-success" // configure this
            };

            await _managementClient.Tickets.CreatePasswordChangeTicketAsync(request);
            
            _logger.LogInformation("Password reset email sent to {Email}", email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password reset email to {Email}", email);
            throw;
        }
    }

    public async Task BlockUserAsync(string userId, bool blocked = true)
    {
        try
        {
            var updateRequest = new UserUpdateRequest
            {
                Blocked = blocked
            };

            await _managementClient.Users.UpdateAsync(userId, updateRequest);
            
            _logger.LogWarning("User {UserId} has been {Status}", userId, blocked ? "blocked" : "unblocked");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to {Action} user {UserId}", 
                blocked ? "block" : "unblock", userId);
            throw;
        }
    }
}

public class Auth0User
{
    public string UserId { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Name { get; set; }
    public string? Picture { get; set; }
    public bool EmailVerified { get; set; }
    public DateTime? LastLogin { get; set; }
    public DateTime? CreatedAt { get; set; }
    public object? Metadata { get; set; }
    public object? AppMetadata { get; set; }
}

public class CreateUserRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Source { get; set; }
}