using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Systematikken.Api.Data;

namespace Systematikken.Api;

/// <summary>Finds the institution of the signed-in user and sets it on the request's <see cref="Tenant"/>.</summary>
public sealed class TenantMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, Tenant tenant, AppDb db)
    {
        if (context.User.FindFirstValue(ClaimTypes.NameIdentifier) is { } userId)
        {
            tenant.InstitutionId = await db.Memberships
                .Where(m => m.UserId == userId)
                .OrderBy(m => m.Id)
                .Select(m => (Guid?)m.InstitutionId)
                .FirstOrDefaultAsync();
        }
        await next(context);
    }
}

/// <summary>Endpoints that need the user to belong to an institution answer 409 "no-institution" otherwise.</summary>
public sealed class RequireInstitution(Tenant tenant) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next) =>
        tenant.InstitutionId is null
            ? Results.Problem(title: "no-institution", detail: "Opret eller vælg en institution først.", statusCode: StatusCodes.Status409Conflict)
            : await next(context);
}

/// <summary>
/// Cookie login plus a custom header on every change: a page on another site cannot set the header,
/// so it cannot make changes on the user's behalf.
/// </summary>
public sealed class CsrfMiddleware(RequestDelegate next)
{
    public const string Header = "X-Requested-With";

    public Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;
        var changes = !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method);
        if (changes && (path.StartsWithSegments("/api") || path.StartsWithSegments("/auth")) && !context.Request.Headers.ContainsKey(Header))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return context.Response.WriteAsync("Missing request header.");
        }
        return next(context);
    }
}
