using ApiGateway.WebApp.Config;
using ApiGateway.WebApp.Extensions;
using Consul;
using Microsoft.OpenApi.Models;
using Yarp.ReverseProxy.Configuration;

namespace ApiGateway.WebApp;

public static class DependencyInjection
{

    public static void AddServices(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment, string serviceName)
    {
        services.AddSecrets(configuration, environment)
            .AddSecurityAccessRules(configuration)

            .AddEndpointsApiExplorer()
            .AddSwaggerGen(options =>
            {
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Ingresar el token JWT obtenido desde Identity. Solo el token, sin el prefijo 'Bearer '."
                });

                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                        },
                        Array.Empty<string>()
                    }
                });
            })
            .AddControllers();

        services.AddReverseProxyWithConsul(configuration)
            .AddObservability(serviceName, environment);
    }

    private static IServiceCollection AddReverseProxyWithConsul(this IServiceCollection services, IConfiguration configuration)
    {
        string configurationName = "ServiceRegistration:ServiceDiscoveryAddress";
        string? serviceDiscoveryAddress = configuration[configurationName];
        services.AddSingleton<IConsulClient, ConsulClient>(p => new ConsulClient(cfg =>
        {
            cfg.Address = new Uri(serviceDiscoveryAddress!);
        }));
        services.AddSingleton<IProxyConfigProvider, ConsulYarpConfigProvider>();

        services.AddReverseProxy();
        return services;
    }

    

    

    
}
