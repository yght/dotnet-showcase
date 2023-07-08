using Yarp.ReverseProxy.Configuration;
using System.Collections.ObjectModel;

namespace MessagePlatform.Gateway.Configuration;

// this shows how to dynamically update YARP config - very useful
public class DynamicProxyConfigProvider : IProxyConfigProvider
{
    private volatile InMemoryConfigProvider _configProvider;
    private readonly object _lock = new();

    public DynamicProxyConfigProvider()
    {
        _configProvider = new InMemoryConfigProvider(GetDefaultRoutes(), GetDefaultClusters());
    }

    public IProxyConfig GetConfig() => _configProvider.GetConfig();

    // method to update config at runtime
    public void UpdateConfig(IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters)
    {
        lock (_lock)
        {
            var oldProvider = _configProvider;
            _configProvider = new InMemoryConfigProvider(routes, clusters);
            oldProvider.SignalChange();
        }
    }

    // add a new backend service dynamically
    public void AddBackendService(string serviceName, string address)
    {
        var currentConfig = GetConfig();
        var routes = currentConfig.Routes.ToList();
        var clusters = currentConfig.Clusters.ToList();

        // add new route
        var newRoute = new RouteConfig
        {
            RouteId = $"{serviceName}-route",
            ClusterId = $"{serviceName}-cluster",
            Match = new RouteMatch
            {
                Path = $"/{serviceName}/{{**catch-all}}"
            }
        };
        routes.Add(newRoute);

        // add new cluster
        var newCluster = new ClusterConfig
        {
            ClusterId = $"{serviceName}-cluster",
            LoadBalancingPolicy = LoadBalancingPolicies.RoundRobin,
            Destinations = new Dictionary<string, DestinationConfig>
            {
                [$"{serviceName}-1"] = new DestinationConfig { Address = address }
            }
        };
        clusters.Add(newCluster);

        UpdateConfig(routes, clusters);
    }

    private static IReadOnlyList<RouteConfig> GetDefaultRoutes()
    {
        return new[]
        {
            new RouteConfig
            {
                RouteId = "default-api",
                ClusterId = "default-cluster",
                Match = new RouteMatch { Path = "/api/{**catch-all}" }
            }
        };
    }

    private static IReadOnlyList<ClusterConfig> GetDefaultClusters()
    {
        return new[]
        {
            new ClusterConfig
            {
                ClusterId = "default-cluster",
                Destinations = new Dictionary<string, DestinationConfig>
                {
                    ["default"] = new DestinationConfig { Address = "http://localhost:5000/" }
                }
            }
        };
    }
}