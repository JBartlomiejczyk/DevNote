using DevNote.Data;
using DevNote.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace DevNote.E2eTests.Infrastructure;

/// <summary>
/// Starts the full Blazor Server app on a real Kestrel port for Playwright.
/// WAF adds UseTestServer first; we add UseKestrel afterwards so it wins as IServer.
/// We never call CreateClient()/Server on this factory, so the TestServer cast in
/// EnsureServer() is never triggered.
/// </summary>
public class PlaywrightWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public Uri ServerAddress { get; private set; } = null!;
    public IServiceScopeFactory ScopeFactory { get; private set; } = null!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            var dbDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (dbDescriptor is not null) services.Remove(dbDescriptor);

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase($"E2eTestDb_{Guid.NewGuid()}"));

            var classificationDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IClassificationService));
            if (classificationDescriptor is not null) services.Remove(classificationDescriptor);
            services.AddScoped<IClassificationService, FakeClassificationService>();

            var helperDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IHelperQuestionsService));
            if (helperDescriptor is not null) services.Remove(helperDescriptor);
            services.AddScoped<IHelperQuestionsService, FakeHelperQuestionsService>();
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // WAF already registered UseTestServer(); adding UseKestrel() after it means
        // KestrelServer is the last IServer registered in DI → GetRequiredService<IServer>() returns Kestrel.
        builder.ConfigureWebHost(wb =>
        {
            wb.UseKestrel();
            wb.UseUrls("http://127.0.0.1:0");
        });

        return base.CreateHost(builder);
    }

    async Task IAsyncLifetime.InitializeAsync()
    {
        // Accessing Services triggers EnsureHostStarted(): builds + starts the host.
        // After Start(), Kestrel has bound to its ephemeral port and IServerAddressesFeature is populated.
        var services = Services;

        var server = services.GetRequiredService<IServer>();
        var addressFeature = server.Features.Get<IServerAddressesFeature>()
            ?? throw new InvalidOperationException("IServerAddressesFeature not available");

        ServerAddress = new Uri(addressFeature.Addresses.First());
        ScopeFactory = services.GetRequiredService<IServiceScopeFactory>();

        await Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
    }
}
