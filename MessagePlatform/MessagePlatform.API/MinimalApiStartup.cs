using Carter;
using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace MessagePlatform.API;

public static class MinimalApiExtensions
{
    public static IServiceCollection AddMinimalApis(this IServiceCollection services)
    {
        // add Carter for module-based minimal APIs
        services.AddCarter();
        
        // add FastEndpoints for high-performance APIs
        services.AddFastEndpoints();
        
        return services;
    }
    
    public static WebApplication UseMinimalApis(this WebApplication app)
    {
        // map Carter modules
        app.MapCarter();
        
        // map FastEndpoints
        app.UseFastEndpoints(c =>
        {
            c.Endpoints.RoutePrefix = "fast-api";
            c.Endpoints.ShortNames = true;
            c.Serializer.Options.PropertyNamingPolicy = null; // keep original casing
        });
        
        return app;
    }
}