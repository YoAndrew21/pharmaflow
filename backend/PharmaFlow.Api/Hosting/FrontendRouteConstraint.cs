using Microsoft.AspNetCore.Routing;

namespace PharmaFlow.Api.Hosting;

/// <summary>Frontend fallback must never claim infrastructure or API requests.</summary>
public sealed class FrontendRouteConstraint : IRouteConstraint
{
    public bool Match(HttpContext? httpContext, IRouter? route, string routeKey,
        RouteValueDictionary values, RouteDirection routeDirection)
    {
        if (httpContext is null) return false;
        var path = httpContext.Request.Path;
        return !new[] { "/api", "/health", "/openapi", "/swagger", "/assets" }
            .Any(prefix => path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase));
    }
}
