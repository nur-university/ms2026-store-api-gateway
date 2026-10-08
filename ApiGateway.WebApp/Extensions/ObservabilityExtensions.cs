using Consul;
using Nur.Store2026.Observability;
using Nur.Store2026.Observability.Config;
using System;

namespace ApiGateway.WebApp.Extensions;

public static class ObservabilityExtensions
{
    public static IServiceCollection AddObservability(this IServiceCollection services, string serviceName, IHostEnvironment environment)
    {
        var jaegerSettings = services.BuildServiceProvider().GetRequiredService<JeagerSettings>();

        services.AddObservability(serviceName, jaegerSettings,
            builder =>
            {
                builder.AddSource("Joseco.HttpGateway");
            },
            shouldIncludeHttpInstrumentation: true);

        //Add health check for this web application
        services.AddHealthChecks();

        if (environment.IsDevelopment())
        {
            // HealthChecksUI polls each registered service's own https://localhost:PORT/health
            // directly (not through YARP), using an IHttpClientFactory client. That's the
            // ASP.NET Core dev certificate, same as the YARP clusters above - accept it here
            // too so the dashboard doesn't error every 10s. Dev-only, never for production.
            services.ConfigureHttpClientDefaults(builder => builder.ConfigurePrimaryHttpMessageHandler(() =>
                new HttpClientHandler { ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator }));
        }

        services.AddHealthChecksUI(setupSettings: setup =>
        {
            setup.SetEvaluationTimeInSeconds(10); // cada 10 seg
            setup.MaximumHistoryEntriesPerEndpoint(50);

            var registerdServices = services.BuildServiceProvider()
                .GetServices<IConsulClient>()
                .SelectMany(client => client.Agent.Services().Result.Response.Values)
                .Select(service => service)
                .ToList();


            foreach (var service in registerdServices)
            {
                setup.AddHealthCheckEndpoint(service.Service, $"{service.Address}:{service.Port}/health");
            }
        })
        .AddInMemoryStorage();

        return services;
    }
}
