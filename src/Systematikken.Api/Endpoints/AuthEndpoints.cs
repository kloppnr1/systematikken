using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Systematikken.Api.Data;

namespace Systematikken.Api.Endpoints;

/// <summary>
/// Log in with Google or Microsoft. We store no passwords. A plain e-mail login exists only when Auth:DevLogin is on,
/// for development and tests.
/// </summary>
public static class AuthEndpoints
{
    public static void Map(WebApplication app)
    {
        var auth = app.MapGroup("/auth").AllowAnonymous();

        auth.MapGet("/login/{provider}", (string provider, string? returnUrl, SignInManager<AppUser> signIn) =>
        {
            var props = signIn.ConfigureExternalAuthenticationProperties(provider, $"/auth/callback?returnUrl={Uri.EscapeDataString(Safe(returnUrl))}");
            return Results.Challenge(props, [provider]);
        });

        auth.MapGet("/callback", async (string? returnUrl, SignInManager<AppUser> signIn, UserManager<AppUser> users) =>
        {
            var info = await signIn.GetExternalLoginInfoAsync();
            if (info is null) return Results.Redirect("/login?fejl=afbrudt");

            var result = await signIn.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: true, bypassTwoFactor: true);
            if (result.Succeeded) return Results.Redirect(Safe(returnUrl));

            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(email)) return Results.Redirect("/login?fejl=email");

            var user = await users.FindByEmailAsync(email) ?? await Create(users, email, info.Principal.FindFirstValue(ClaimTypes.Name) ?? email);
            if (user is null) return Results.Redirect("/login?fejl=oprettelse");

            await users.AddLoginAsync(user, info);
            await signIn.SignInAsync(user, isPersistent: true);
            return Results.Redirect(Safe(returnUrl));
        });

        auth.MapPost("/dev", async (DevLogin body, IConfiguration config, SignInManager<AppUser> signIn, UserManager<AppUser> users) =>
        {
            if (!config.GetValue<bool>("Auth:DevLogin")) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(body.Email) || !body.Email.Contains('@')) return Http.Invalid("email", "Skriv en e-mailadresse.");
            var email = body.Email.Trim().ToLowerInvariant();
            var user = await users.FindByEmailAsync(email) ?? await Create(users, email, email.Split('@')[0]);
            if (user is null) return Http.Invalid("email", "Brugeren kunne ikke oprettes.");
            await signIn.SignInAsync(user, isPersistent: true);
            return Results.NoContent();
        });

        // The demo: one button logs anyone in as the demo user. Only when Demo:Enabled is on.
        auth.MapPost("/demo", async (IConfiguration config, SignInManager<AppUser> signIn, UserManager<AppUser> users) =>
        {
            if (!config.GetValue<bool>("Demo:Enabled")) return Results.NotFound();
            var user = await users.FindByEmailAsync(Services.DemoData.DemoEmail);
            if (user is null) return Results.Problem("Demoen er ved at blive sat op. Prøv igen om lidt.", statusCode: 503);
            await signIn.SignInAsync(user, isPersistent: false);
            return Results.NoContent();
        });

        auth.MapPost("/logout", async (SignInManager<AppUser> signIn) =>
        {
            await signIn.SignOutAsync();
            return Results.NoContent();
        });

        // Who am I, and what can I do next. Answers for anonymous users too, so the app knows to show the login.
        app.MapGet("/api/me", async (HttpContext ctx, SignInManager<AppUser> signIn, UserManager<AppUser> users, IConfiguration config, AppDb db, Tenant tenant, TimeProvider clock) =>
        {
            var providers = (await signIn.GetExternalAuthenticationSchemesAsync()).Select(s => new { s.Name, s.DisplayName }).ToList();
            var devLogin = config.GetValue<bool>("Auth:DevLogin");
            var demo = config.GetValue<bool>("Demo:Enabled");
            if (ctx.User.Identity?.IsAuthenticated != true)
                return Results.Ok(new { signedIn = false, providers, devLogin, demo });

            var institution = tenant.InstitutionId is { } id
                ? await db.Institutions.Where(i => i.Id == id).Select(i => new { i.Id, i.Name, i.Slug }).SingleAsync()
                : null;
            var email = ctx.User.Email();
            var now = clock.GetUtcNow().UtcDateTime;
            var invitations = await db.Invitations.IgnoreQueryFilters()
                .Where(i => i.Email == email && i.AcceptedAt == null && i.ExpiresAt > now)
                .Join(db.Institutions, i => i.InstitutionId, s => s.Id, (i, s) => new { i.Token, InstitutionName = s.Name })
                .ToListAsync();

            var user = await users.GetUserAsync(ctx.User);
            var name = string.IsNullOrWhiteSpace(user?.DisplayName) ? email : user.DisplayName;
            return Results.Ok(new { signedIn = true, email, name, institution, invitations, providers, devLogin, demo });
        }).AllowAnonymous();
    }

    public sealed record DevLogin(string Email);

    private static async Task<AppUser?> Create(UserManager<AppUser> users, string email, string name)
    {
        var user = new AppUser { UserName = email, Email = email, EmailConfirmed = true, DisplayName = name };
        return (await users.CreateAsync(user)).Succeeded ? user : null;
    }

    private static string Safe(string? returnUrl) =>
        returnUrl is { Length: > 0 } r && r.StartsWith('/') && !r.StartsWith("//") && !r.StartsWith("/\\") ? r : "/";
}
