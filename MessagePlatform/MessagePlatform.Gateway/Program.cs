using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.LoadBalancing;
using Yarp.ReverseProxy.Health;

var builder = WebApplication.CreateBuilder(args);

// add YARP reverse proxy
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddLoadBalancingPolicies()
    .AddActiveHealthChecks();

// add authentication for secured routes
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.Authority = "https://your-auth0-domain.auth0.com/";
        options.Audience = "message-platform-api";
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// custom middleware for request/response logging
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    
    logger.LogInformation("YARP Gateway: {Method} {Path} from {RemoteIp}", 
        context.Request.Method, 
        context.Request.Path, 
        context.Connection.RemoteIpAddress);
    
    await next();
    
    logger.LogInformation("YARP Gateway: Response {StatusCode} for {Path}", 
        context.Response.StatusCode, 
        context.Request.Path);
});

// health check endpoint
app.MapGet("/gateway/health", () => new
{
    Status = "Healthy",
    Gateway = "YARP",
    Version = "2.0",
    Timestamp = DateTime.UtcNow
});

// metrics endpoint
app.MapGet("/gateway/metrics", (IProxyStateLookup proxyState) =>
{
    var clusters = proxyState.GetClusters();
    var routes = proxyState.GetRoutes();
    
    return new
    {
        TotalClusters = clusters.Count(),
        TotalRoutes = routes.Count(),
        HealthyClusters = clusters.Count(c => c.Health == ClusterHealthState.Healthy),
        UnhealthyClusters = clusters.Count(c => c.Health == ClusterHealthState.Unhealthy),
        Note = "Real-time cluster health and routing metrics"
    };
});

// enable auth for protected routes
app.UseAuthentication();
app.UseAuthorization();

// map YARP routes
app.MapReverseProxy();

app.Run();