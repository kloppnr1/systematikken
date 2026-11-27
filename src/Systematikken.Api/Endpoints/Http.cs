using System.Security.Claims;

namespace Systematikken.Api.Endpoints;

/// <summary>Small helpers so endpoints read plainly.</summary>
public static class Http
{
    /// <summary>A validation error the form shows next to the field.</summary>
    public static IResult Invalid(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });

    public static IResult Invalid(Dictionary<string, string[]> errors) => Results.ValidationProblem(errors);

    public static string UserId(this ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier)!;
    public static string Email(this ClaimsPrincipal user) => (user.FindFirstValue(ClaimTypes.Email) ?? user.Identity?.Name ?? "").ToLowerInvariant();

    public static string BaseUrl(this HttpRequest request) => $"{request.Scheme}://{request.Host}";

    public static bool TryMonth(string? value, out DateOnly month)
    {
        month = default;
        if (value is null || !DateOnly.TryParseExact(value + "-01", "yyyy-MM-dd", out var d)) return false;
        month = d;
        return true;
    }

    public static string? Clean(this string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

/// <summary>Collects field errors, then returns them all at once.</summary>
public sealed class Errors
{
    private readonly Dictionary<string, string[]> _errors = [];
    public Errors Add(string field, string message) { _errors[field] = [message]; return this; }
    public Errors Require(string field, string? value, string message) => string.IsNullOrWhiteSpace(value) ? Add(field, message) : this;
    public bool Any => _errors.Count > 0;
    public IResult Result() => Http.Invalid(_errors);
}
