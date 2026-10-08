using ApiGateway.WebApp.Middlewares;
using Consul;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Swashbuckle.AspNetCore.SwaggerUI;

namespace ApiGateway.WebApp.Extensions;

public static class ApplicationBuilderExtensions
{
    // Must be kept alive for the app's lifetime - an unreferenced Timer is eligible
    // for GC at any point, which would silently stop the refresh.
    private static System.Threading.Timer? _swaggerRefreshTimer;

    public static IApplicationBuilder UseSwaggerWithUi(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI();

        return app;
    }

    public static IApplicationBuilder UseSwaggerWithUiWithConsul(this WebApplication app)
    {
        app.UseSwagger();

        var consul = app.Services.GetRequiredService<IConsulClient>();
        var logger = app.Services.GetRequiredService<ILogger<Program>>();

        var uiOptions = new SwaggerUIOptions();
        uiOptions.SwaggerEndpoint("/swagger/v1/swagger.json", "API Gateway");

        // Aspire (or just starting the services in a different order) can bring the
        // gateway up before the other 5 have registered in Consul. The original code
        // only read Consul.Agent.Services() ONCE here at startup, so anything not
        // registered yet at that exact moment never appeared in the dropdown for the
        // life of the process. This refreshes uiOptions.ConfigObject.Urls in place on
        // a timer - Swashbuckle re-serializes ConfigObject on every request to
        // /swagger/index.js, so later registrations do show up without a restart.
        // Reuses the same health-filtered lookup as ConsulYarpConfigProvider (an
        // instance whose check is failing drops out here too, same reasoning).
        async void RefreshSwaggerEndpoints(object? state)
        {
            try
            {
                var serviceNames = await consul.Catalog.Services();
                var urls = new List<UrlDescriptor>
                {
                    new() { Url = "/swagger/v1/swagger.json", Name = "API Gateway" }
                };

                foreach (var serviceName in serviceNames.Response.Keys)
                {
                    if (serviceName == "consul")
                    {
                        continue;
                    }

                    var healthyInstances = await consul.Health.Service(serviceName, tag: null, passingOnly: true);
                    if (healthyInstances.Response.Length == 0)
                    {
                        continue;
                    }

                    var instance = healthyInstances.Response[0].Service;
                    urls.Add(new UrlDescriptor
                    {
                        Url = $"{instance.Address}:{instance.Port}/swagger/v1/swagger.json",
                        Name = $"{serviceName} API"
                    });
                }

                uiOptions.ConfigObject.Urls = urls;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error actualizando la lista de Swagger desde Consul");
            }
        }

        _swaggerRefreshTimer = new System.Threading.Timer(RefreshSwaggerEndpoints, null, TimeSpan.Zero, TimeSpan.FromSeconds(15));

        app.UseSwaggerUI(uiOptions);

        return app;
    }

    public static IApplicationBuilder UseHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        });

        app.MapHealthChecksUI(options =>
        {
            options.UIPath = "/health-ui";
        });

        return app;
    }

    public static IApplicationBuilder UseAdvancedScopeAuthorization(this WebApplication app)
    {
        // app.UseMiddleware<YarpTracingMiddleware>();
       // app.UseMiddleware<CustomTracingSpanMiddleware>();
        app.UseMiddleware<AdvancedScopeAuthorizationMiddleware>();
        return app;
    }

    public static IApplicationBuilder UseCorrelationId(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            context.Request.Headers["X-Correlation-Id"] = Guid.NewGuid().ToString();
            await next();
        });
        return app;
    }
}
