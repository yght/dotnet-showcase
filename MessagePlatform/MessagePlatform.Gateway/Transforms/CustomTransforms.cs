using Yarp.ReverseProxy.Transforms;

namespace MessagePlatform.Gateway.Transforms;

// custom transforms are super powerful in YARP
public class RequestIdTransform : RequestTransform
{
    public override ValueTask ApplyAsync(RequestTransformContext context)
    {
        // add unique request ID for tracing
        var requestId = Guid.NewGuid().ToString("N")[..8];
        context.ProxyRequest.Headers.Add("X-Request-ID", requestId);
        context.HttpContext.Response.Headers.Add("X-Request-ID", requestId);
        
        return default;
    }
}

public class ApiVersionTransform : RequestTransform
{
    public override ValueTask ApplyAsync(RequestTransformContext context)
    {
        // extract API version from path and add as header
        var path = context.HttpContext.Request.Path.Value;
        if (path?.Contains("/api/v") == true)
        {
            var versionPart = path.Split('/').FirstOrDefault(p => p.StartsWith("v"));
            if (versionPart != null)
            {
                context.ProxyRequest.Headers.Add("X-API-Version", versionPart);
            }
        }
        
        return default;
    }
}

public class UserContextTransform : RequestTransform
{
    public override ValueTask ApplyAsync(RequestTransformContext context)
    {
        // forward user information to backend services
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated == true)
        {
            var userId = user.FindFirst("sub")?.Value;
            var email = user.FindFirst("email")?.Value;
            
            if (!string.IsNullOrEmpty(userId))
                context.ProxyRequest.Headers.Add("X-User-ID", userId);
                
            if (!string.IsNullOrEmpty(email))
                context.ProxyRequest.Headers.Add("X-User-Email", email);
        }
        
        return default;
    }
}