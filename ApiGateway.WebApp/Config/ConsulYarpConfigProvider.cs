using Consul;
using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;


namespace ApiGateway.WebApp.Config;

public class ConsulYarpConfigProvider : IProxyConfigProvider
{
    private readonly ILogger<ConsulYarpConfigProvider> _logger;
    private readonly IConsulClient _consulClient;
    private readonly IHostEnvironment _environment;
    private readonly Timer _timer;
    private volatile ConsulProxyConfig _config;

    public ConsulYarpConfigProvider(ILogger<ConsulYarpConfigProvider> logger, IConsulClient consulClient, IHostEnvironment environment)
    {
        _logger = logger;
        _consulClient = consulClient;
        _environment = environment;
        _config = new ConsulProxyConfig([], [], new CancellationTokenSource());

        _timer = new Timer(Refresh, null, TimeSpan.Zero, TimeSpan.FromSeconds(30)); // refresca cada 30s

    }

    public IProxyConfig GetConfig() => _config;

    private async void Refresh(object state)
    {
        try
        {
            // Catalog.Services() just lists service NAMES known to the agent - it says
            // nothing about health. Health.Service(name, passingOnly: true) is what
            // actually filters out instances whose checks are failing, so a crashed
            // (but still-registered) instance stops receiving traffic automatically.
            var serviceNames = await _consulClient.Catalog.Services();
            var clusters = new Dictionary<string, ClusterConfig>();
            var routes = new Dictionary<string, Yarp.ReverseProxy.Configuration.RouteConfig>();

            foreach (var serviceName in serviceNames.Response.Keys)
            {
                if (serviceName == "consul")
                {
                    continue;
                }

                var healthyInstances = await _consulClient.Health.Service(serviceName, tag: null, passingOnly: true);

                if (healthyInstances.Response.Length == 0)
                {
                    continue;
                }

                var clusterId = $"{serviceName}-cluster";
                var routePrefix = $"/api/{serviceName}";// /api/identity/security/login => api/security/login

                var destinations = healthyInstances.Response.ToDictionary(
                    entry => entry.Service.ID,
                    entry => new Yarp.ReverseProxy.Configuration.DestinationConfig
                    {
                        Address = $"{entry.Service.Address}:{entry.Service.Port}"
                    });

                clusters[clusterId] = new ClusterConfig
                {
                    ClusterId = clusterId,
                    Destinations = destinations,
                    // Dev-only: services are registered with the ASP.NET Core HTTPS dev
                    // certificate (self-signed). Validating it fully requires the machine's
                    // trusted-root store to have it, which the account running this process
                    // may not have write access to (e.g. no admin rights) - so instead of
                    // requiring an OS-level trust change, accept it explicitly here, scoped
                    // to Development only. Never do this for a real/production cluster.
                    HttpClient = _environment.IsDevelopment()
                        ? new HttpClientConfig { DangerousAcceptAnyServerCertificate = true }
                        : null
                };

                routes[clusterId] = new Yarp.ReverseProxy.Configuration.RouteConfig
                {
                    RouteId = $"{serviceName}-route",
                    ClusterId = clusterId,
                    Match = new RouteMatch
                    {
                        Path = $"{routePrefix}/{{**catch-all}}"
                    },
                    Transforms = new List<Dictionary<string, string>>
                    {
                        new() { { "PathRemovePrefix", routePrefix } },
                        new() { { "PathPrefix", "/api" } }
                    }
                };
            }

            var oldConfig = _config;
            _config = new ConsulProxyConfig([.. routes.Values], [.. clusters.Values], new CancellationTokenSource());
            // Cancelling the OLD config's token is what tells YARP "call GetConfig() again" -
            // without this, YARP keeps using whatever config it first read forever, since its
            // change-token subscription never fires (see ConsulProxyConfig.ChangeToken below).
            oldConfig.SignalChange();

            _logger.LogInformation("Configuración de YARP actualizada desde Consul con {RouteCount} rutas y {ClusterCount} clusters (solo instancias healthy)", routes.Count, clusters.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actualizando configuración de YARP desde Consul");
        }
    }

    private class ConsulProxyConfig(IReadOnlyList<Yarp.ReverseProxy.Configuration.RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters, CancellationTokenSource changeTokenSource) : Yarp.ReverseProxy.Configuration.IProxyConfig
    {
        public IReadOnlyList<Yarp.ReverseProxy.Configuration.RouteConfig> Routes { get; } = routes;
        public IReadOnlyList<ClusterConfig> Clusters { get; } = clusters;
        public IChangeToken ChangeToken { get; } = new CancellationChangeToken(changeTokenSource.Token);

        public void SignalChange() => changeTokenSource.Cancel();
    }
}