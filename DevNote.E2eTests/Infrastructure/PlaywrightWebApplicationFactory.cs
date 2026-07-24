using DevNote.Data;
using DevNote.Models;
using DevNote.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace DevNote.E2eTests.Infrastructure;

/// <summary>
/// Starts the full Blazor Server app on a real Kestrel ephemeral port for Playwright.
/// Bypasses WebApplicationFactory entirely to avoid the EnsureServer() TestServer cast
/// issue that arises from Blazor Server's WebSocket requirement.
/// Replicates Program.cs configuration with test overrides (InMemory DB + fake LLM services).
/// </summary>
public class PlaywrightWebApplicationFactory : IAsyncLifetime
{
    private WebApplication? _app;

    public Uri ServerAddress { get; private set; } = null!;
    public IServiceScopeFactory ScopeFactory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        // Use the main app's directory as content root so static assets are found
        var appRoot = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = appRoot,
            ApplicationName = "dev-note",       // points UseStaticWebAssets() at dev-note.staticwebassets.runtime.json
            EnvironmentName = "Development",    // enables UseStaticWebAssets() → blazor.web.js served
            Args = Array.Empty<string>()
        });
        builder.WebHost.UseStaticWebAssets();

        // InMemory DB instead of Postgres.
        // Keep one stable DB name per factory instance so seeding and HTTP requests
        // hit the same in-memory store across different scopes/contexts.
        var inMemoryDbName = $"E2eTestDb_{Guid.NewGuid()}";
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(inMemoryDbName));

        builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequiredLength = 8;
            options.SignIn.RequireConfirmedEmail = false;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        builder.Services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/login";
            options.AccessDeniedPath = "/access-denied";
            options.ExpireTimeSpan = TimeSpan.FromDays(14);
            options.SlidingExpiration = true;
        });

        builder.Services.Configure<SmtpOptions>(_ => { });
        builder.Services.AddTransient<IEmailSender<ApplicationUser>, SmtpEmailSender>();

        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        builder.Services.AddScoped<WizardStateService>();
        builder.Services.Configure<AzureOpenAIOptions>(_ => { });
        builder.Services.AddScoped<ClassificationResponseValidator>();
        builder.Services.AddScoped<IClassificationService, FakeClassificationService>();
        builder.Services.AddScoped<IHelperQuestionsService, FakeHelperQuestionsService>();
        builder.Services.AddScoped<HelperQuestionsCoordinator>();
        builder.Services.AddScoped<NoteService>();

        _app = builder.Build();

        _app.UseStaticFiles();
        _app.UseAntiforgery();
        _app.UseAuthentication();
        _app.UseAuthorization();

        _app.MapGet("/healthz", () => Results.Ok("Healthy")).AllowAnonymous();
        _app.MapPost("/api/auth/logout", async (SignInManager<ApplicationUser> sm) =>
        {
            await sm.SignOutAsync();
            return Results.Redirect("/login");
        }).RequireAuthorization().DisableAntiforgery();

        // Test-only backdoor login registered BEFORE MapRazorComponents to avoid catch-all shadowing.
        // Email is a query-string param (?email=...) to avoid %40 path-segment decode issues.
        // LoginAsync calls this via IBrowserContext.APIRequest (shares the cookie jar) — no browser
        // navigation needed, so redirect handling is irrelevant.
        _app.MapGet("/api/test/login", async (string email,
            UserManager<ApplicationUser> um, SignInManager<ApplicationUser> sm) =>
        {
            var user = await um.FindByEmailAsync(email);
            if (user is null) return Results.NotFound($"User '{email}' not found in test database");
            await sm.SignInAsync(user, isPersistent: true);
            return Results.Ok($"Signed in as {email}");
        }).AllowAnonymous().DisableAntiforgery();

        // Test-only: returns current user identity for diagnostics
        _app.MapGet("/api/test/whoami", (HttpContext ctx) =>
        {
            var user = ctx.User;
            return user.Identity?.IsAuthenticated == true
                ? Results.Ok(user.Identity.Name ?? "authenticated")
                : Results.Ok("anonymous");
        }).AllowAnonymous();

        _app.MapRazorComponents<DevNote.Components.App>()
            .AddInteractiveServerRenderMode();

        _app.Urls.Add("http://127.0.0.1:0");

        await _app.StartAsync();

        ServerAddress = new Uri(_app.Urls.First());
        ScopeFactory = _app.Services.GetRequiredService<IServiceScopeFactory>();
    }

    public async Task DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}
