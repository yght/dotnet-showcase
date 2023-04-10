using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace MessagePlatform.Infrastructure.Auth;

// custom authorization requirements - this is how pros do it
public class HasPermissionRequirement : IAuthorizationRequirement
{
    public string Permission { get; }
    
    public HasPermissionRequirement(string permission)
    {
        Permission = permission;
    }
}

public class PermissionHandler : AuthorizationHandler<HasPermissionRequirement>
{
    private readonly IAuth0Service _auth0Service;
    private readonly ILogger<PermissionHandler> _logger;

    public PermissionHandler(IAuth0Service auth0Service, ILogger<PermissionHandler> logger)
    {
        _auth0Service = auth0Service;
        _logger = logger;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        HasPermissionRequirement requirement)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("No user ID found in claims for permission check");
            context.Fail();
            return;
        }

        try
        {
            // check Auth0 for real-time permissions
            var hasPermission = await _auth0Service.HasPermissionAsync(userId, requirement.Permission);
            
            if (hasPermission)
            {
                context.Succeed(requirement);
                _logger.LogDebug("User {UserId} has permission {Permission}", userId, requirement.Permission);
            }
            else
            {
                _logger.LogWarning("User {UserId} denied access - missing permission {Permission}", 
                    userId, requirement.Permission);
                context.Fail();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking permission {Permission} for user {UserId}", 
                requirement.Permission, userId);
            context.Fail();
        }
    }
}

// some common permissions for messaging platform
public static class Permissions
{
    public const string ReadMessages = "read:messages";
    public const string SendMessages = "send:messages";
    public const string CreateGroups = "create:groups";
    public const string ModerateGroups = "moderate:groups";
    public const string ManageUsers = "manage:users";
    public const string ViewAnalytics = "view:analytics";
    public const string SendBroadcasts = "send:broadcasts";
    public const string AccessAdminPanel = "access:admin";
}

// multi-tenant authorization - very common in SaaS
public class OrganizationRequirement : IAuthorizationRequirement
{
    public string OrganizationId { get; }
    
    public OrganizationRequirement(string organizationId)
    {
        OrganizationId = organizationId;
    }
}

public class OrganizationHandler : AuthorizationHandler<OrganizationRequirement>
{
    private readonly ILogger<OrganizationHandler> _logger;

    public OrganizationHandler(ILogger<OrganizationHandler> logger)
    {
        _logger = logger;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OrganizationRequirement requirement)
    {
        var userOrg = context.User.FindFirst("org_id")?.Value;
        
        if (string.IsNullOrEmpty(userOrg))
        {
            _logger.LogWarning("No organization ID found in user claims");
            context.Fail();
            return Task.CompletedTask;
        }

        if (userOrg == requirement.OrganizationId)
        {
            context.Succeed(requirement);
            _logger.LogDebug("User authorized for organization {OrgId}", requirement.OrganizationId);
        }
        else
        {
            _logger.LogWarning("User from org {UserOrg} denied access to org {RequiredOrg}", 
                userOrg, requirement.OrganizationId);
            context.Fail();
        }

        return Task.CompletedTask;
    }
}