using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Systematikken.Api;
using Systematikken.Api.Data;
using Systematikken.Api.Endpoints;
using Systematikken.Api.Services;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// All state lives in one folder: the SQLite database and the cookie keys. Back up this folder.
var dataDir = config["DataDirectory"] ?? Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(dataDir);

builder.Services.AddScoped<Tenant>();
builder.Services.AddDbContext<AppDb>(o => o.UseSqlite(config.GetConnectionString("Db") ?? $"Data Source={Path.Combine(dataDir, "systematikken.db")}"));
builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataDir, "keys"))).SetApplicationName("systematikken");

builder.Services.AddIdentity<AppUser, IdentityRole>(o =>
    {
        o.User.RequireUniqueEmail = true;
        o.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<AppDb>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(o =>
{
    o.Cookie.Name = "systematikken";
    o.Cookie.SameSite = SameSiteMode.Lax;
    o.Cookie.HttpOnly = true;
    o.ExpireTimeSpan = TimeSpan.FromDays(14);
    o.SlidingExpiration = true;
    // The app is a single-page app: answer 401/403 instead of redirecting to a login page.
    o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
    o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
});

var auth = builder.Services.AddAuthentication();
if (config["Auth:Google:ClientId"] is { Length: > 0 } googleId)
    auth.AddGoogle(o => { o.ClientId = googleId; o.ClientSecret = config["Auth:Google:ClientSecret"]!; });
if (config["Auth:Microsoft:ClientId"] is { Length: > 0 } msId)
    auth.AddMicrosoftAccount(o => { o.ClientId = msId; o.ClientSecret = config["Auth:Microsoft:ClientSecret"]!; });

builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("public", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(10) }));
});

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<BillingService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<ChildService>();
builder.Services.AddScoped<StatsService>();
builder.Services.AddScoped<ExportService>();
builder.Services.AddSingleton<RetentionService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<RetentionService>());
builder.Services.AddSingleton<BackupService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<BackupService>());

var demo = config.GetValue<bool>("Demo:Enabled");
builder.Services.AddSingleton<DemoData>();
if (demo) builder.Services.AddHostedService<DemoResetService>();

var app = builder.Build();

if (demo)
{
    // The demo starts from the same made-up institution every time.
    await app.Services.GetRequiredService<DemoData>().ResetAsync();
}
else
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    await db.Database.MigrateAsync();
    await Seed.MunicipalitiesAsync(db);
}

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();

// The React app, built into wwwroot. index.html is never cached so a deploy reaches everyone at once.
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        if (ctx.File.Name == "index.html") ctx.Context.Response.Headers.CacheControl = "no-cache";
        else if (ctx.Context.Request.Path.StartsWithSegments("/assets")) ctx.Context.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
    },
});

app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<TenantMiddleware>();
app.UseMiddleware<CsrfMiddleware>();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok("ok")).AllowAnonymous();
AuthEndpoints.Map(app);
InstitutionEndpoints.Map(app);
HomeEndpoints.Map(app);
ChildEndpoints.Map(app);
WaitingListEndpoints.Map(app);
BillingEndpoints.Map(app);
PublicEndpoints.Map(app);
StatsEndpoints.Map(app);

// Every other path belongs to the React app's router.
app.MapFallbackToFile("index.html").AllowAnonymous();

app.Run();

public partial class Program;
