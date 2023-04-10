using Auth0.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using MessagePlatform.Infrastructure.Auth;
using System.Security.Claims;

namespace MessagePlatform.API;

public static class Auth0Configuration
{
    public static IServiceCollection AddAuth0Authentication(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        var auth0Settings = configuration.GetSection("Auth0").Get<Auth0Settings>();
        services.Configure<Auth0Settings>(configuration.GetSection("Auth0"));

        // configure Auth0 authentication
        services.AddAuth0WebAppAuthentication(options =>
        {
            options.Domain = auth0Settings.Domain;
            options.ClientId = auth0Settings.ClientId;
            options.ClientSecret = auth0Settings.ClientSecret;
        });

        // also support JWT bearer tokens for API access
        services.AddAuthentication()
            .AddJwtBearer("Auth0-JWT", options =>
            {
                options.Authority = $"https://{auth0Settings.Domain}/";
                options.Audience = auth0Settings.Audience;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = ClaimTypes.NameIdentifier,
                    RoleClaimType = "https://schemas.auth0.com/roles",
                    ValidateIssuer = true,
                    ValidIssuer = $"https://{auth0Settings.Domain}/",
                    ValidateAudience = true,
                    ValidAudience = auth0Settings.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(5) // allow some clock drift
                };
                
                // add custom token validation
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        // add custom claims or validation logic here
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILogger<JwtBearerEvents>>();
                        
                        var userId = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                        logger.LogDebug("JWT token validated for user {UserId}", userId);
                        
                        return Task.CompletedTask;
                    },
                    
                    OnAuthenticationFailed = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILogger<JwtBearerEvents>>();
                        
                        logger.LogError(context.Exception, "JWT authentication failed: {Error}", 
                            context.Exception.Message);
                        
                        return Task.CompletedTask;
                    }
                };
            });

        // set default auth scheme
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = "Auth0-JWT";
            options.DefaultChallengeScheme = "Auth0-JWT";
        });

        return services;
    }

    public static IServiceCollection AddAuth0Authorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            // permission-based policies
            options.AddPolicy("RequireReadMessages", policy =>
                policy.Requirements.Add(new HasPermissionRequirement(Permissions.ReadMessages)));
                
            options.AddPolicy("RequireSendMessages", policy =>
                policy.Requirements.Add(new HasPermissionRequirement(Permissions.SendMessages)));
                
            options.AddPolicy("RequireUserManagement", policy =>
                policy.Requirements.Add(new HasPermissionRequirement(Permissions.ManageUsers)));
                
            options.AddPolicy("RequireCreateGroups", policy =>
                policy.Requirements.Add(new HasPermissionRequirement(Permissions.CreateGroups)));
                
            options.AddPolicy("RequireServiceAccess", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireClaim("gty", "client-credentials"); // M2M tokens only
            });

            // organization-based policy (multi-tenant)
            options.AddPolicy("RequireOrganizationAccess", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new OrganizationRequirement("org_123")); // example
            });

            // role-based policies (simpler approach)
            options.AddPolicy("RequireAdminRole", policy =>
                policy.RequireClaim("https://schemas.auth0.com/roles", "admin"));
                
            options.AddPolicy("RequireModeratorRole", policy =>
                policy.RequireClaim("https://schemas.auth0.com/roles", "moderator", "admin"));
        });

        // register authorization handlers
        services.AddScoped<IAuthorizationHandler, PermissionHandler>();
        services.AddScoped<IAuthorizationHandler, OrganizationHandler>();

        return services;
    }

    public static IServiceCollection AddAuth0Services(this IServiceCollection services)
    {
        // register Auth0 services
        services.AddScoped<IAuth0Service, Auth0Service>();
        services.AddScoped<IM2MTokenService, M2MTokenService>();
        
        // add HttpClient for M2M service
        services.AddHttpClient<IM2MTokenService>();

        return services;
    }
}