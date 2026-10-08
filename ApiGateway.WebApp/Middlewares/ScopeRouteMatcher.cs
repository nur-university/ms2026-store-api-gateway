using Microsoft.AspNetCore.Routing.Template;
using Nur.Store2026.Access.Contracts;

namespace ApiGateway.WebApp.Middlewares;

internal class ScopeRouteMatcher
{
    private readonly List<(TemplateMatcher Matcher, ScopeAccessRule Rule)> _compiledRules;

    public ScopeRouteMatcher(IEnumerable<ScopeAccessRule> rules)
    {
        _compiledRules = rules.Select(rule =>
        {
            var template = TemplateParser.Parse(rule.RouteTemplate);
            var matcher = new TemplateMatcher(template, new RouteValueDictionary());
            return (matcher, rule);
        }).ToList();
    }

    public ScopeAccessRule? Match(HttpContext context)
    {
        var path = context.Request.Path.Value?.TrimEnd('/') ?? "/";
        var method = context.Request.Method.ToUpperInvariant();

        foreach (var (matcher, rule) in _compiledRules)
        {
            var values = new RouteValueDictionary();
            if (matcher.TryMatch(path, values) && rule.HttpMethod == method)
            {
                return rule;
            }
        }

        return null;
    }
}
