using Microsoft.AspNetCore.Mvc;
using MessagePlatform.Gateway.Configuration;
using Yarp.ReverseProxy.Configuration;

namespace MessagePlatform.Gateway.Controllers;

[ApiController]
[Route("gateway/admin")]
public class GatewayManagementController : ControllerBase
{
    private readonly DynamicProxyConfigProvider _configProvider;
    private readonly IProxyStateLookup _proxyState;
    private readonly ILogger<GatewayManagementController> _logger;

    public GatewayManagementController(
        DynamicProxyConfigProvider configProvider,
        IProxyStateLookup proxyState,
        ILogger<GatewayManagementController> logger)
    {
        _configProvider = configProvider;
        _proxyState = proxyState;
        _logger = logger;
    }

    [HttpGet("routes")]
    public IActionResult GetRoutes()
    {
        var routes = _proxyState.GetRoutes()
            .Select(r => new
            {
                r.Config.RouteId,
                r.Config.ClusterId,
                Path = r.Config.Match.Path,
                IsHealthy = r.Health == RouteHealthState.Healthy
            });

        return Ok(routes);
    }

    [HttpGet("clusters")]
    public IActionResult GetClusters()
    {
        var clusters = _proxyState.GetClusters()
            .Select(c => new
            {
                c.ClusterId,
                Health = c.Health.ToString(),
                Destinations = c.Destinations.Select(d => new
                {
                    d.DestinationId,
                    d.Config.Address,
                    Health = d.Health.ToString()
                })
            });

        return Ok(clusters);
    }

    [HttpPost("services")]
    public IActionResult AddService([FromBody] AddServiceRequest request)
    {
        try
        {
            _configProvider.AddBackendService(request.ServiceName, request.Address);
            
            _logger.LogInformation("Added new service {ServiceName} at {Address}", 
                request.ServiceName, request.Address);
            
            return Ok(new { Message = "Service added successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add service {ServiceName}", request.ServiceName);
            return StatusCode(500, "Failed to add service");
        }
    }

    [HttpGet("health")]
    public IActionResult GetHealth()
    {
        var clusters = _proxyState.GetClusters();
        var totalClusters = clusters.Count();
        var healthyClusters = clusters.Count(c => c.Health == ClusterHealthState.Healthy);
        
        return Ok(new
        {
            Status = healthyClusters == totalClusters ? "Healthy" : "Degraded",
            TotalClusters = totalClusters,
            HealthyClusters = healthyClusters,
            UnhealthyClusters = totalClusters - healthyClusters,
            LastCheck = DateTime.UtcNow
        });
    }

    [HttpGet("metrics")]
    public IActionResult GetMetrics()
    {
        // in real app, you'd collect actual metrics from monitoring system
        return Ok(new
        {
            RequestsPerSecond = Random.Shared.Next(50, 200),
            AverageResponseTime = Random.Shared.Next(10, 100) + "ms",
            ErrorRate = Random.Shared.NextDouble() * 5,
            Note = "These would be real metrics in production"
        });
    }
}

public record AddServiceRequest(string ServiceName, string Address);