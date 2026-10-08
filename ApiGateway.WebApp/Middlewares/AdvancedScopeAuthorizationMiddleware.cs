using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApiGateway.WebApp.Middlewares;

internal class AdvancedScopeAuthorizationMiddleware(RequestDelegate _next, ScopeRouteMatcher _matcher)
{

    public async Task InvokeAsync(HttpContext context)
    {
        var matchingRule = _matcher.Match(context);

        if (matchingRule == null)
        {
            // Si no hay regla, puedes permitir el acceso o negarlo por defecto
            await _next(context);
            return;
        }


        if (matchingRule.AllowAnonymous)
        {
            // Ruta pública: continuar sin validación
            await _next(context);
            return;
        }

        if (!context.User.Identity?.IsAuthenticated ?? true)
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Unauthorized.");
            return;
        }

        if (string.IsNullOrEmpty(matchingRule.RequiredScope))
        {
            // Si no se requiere un scope específico, continuar
            await _next(context);
            return;
        }

        var hasScope = context.User.Claims.Any(c =>
            c.Type == "scope" &&
            c.Value.Split(" ").Contains(matchingRule.RequiredScope));

        if (!hasScope)
        {
            context.Response.StatusCode = 403;
            await context.Response.WriteAsync($"Forbidden: Missing scope '{matchingRule.RequiredScope}'.");
            return;
        }

        await _next(context);
    }
}
