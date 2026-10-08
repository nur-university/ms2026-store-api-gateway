using ApiGateway.WebApp.Middlewares;
using Nur.Store2026.Security;
using Nur.Store2026.Security.Config;

namespace ApiGateway.WebApp.Extensions;

public static class SecurityExtensions
{
    public static IServiceCollection AddSecurityAccessRules(this IServiceCollection services, IConfiguration configuration)
    {
        JwtOptions jwtOptions = services.BuildServiceProvider()
            .GetRequiredService<JwtOptions>();
        services.AddSecurity(jwtOptions, Nur.Store2026.Access.Permission.PermissionsList);
        services.AddSingleton(new ScopeRouteMatcher(Nur.Store2026.Access.Routes.RouteList));
        return services;
    }
}
