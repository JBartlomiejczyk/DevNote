using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Xunit;

namespace DevNote.E2eTests.Infrastructure;

public abstract class E2eTestBase : IAsyncLifetime
{
    private readonly PlaywrightWebApplicationFactory _factory;
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;
    private IBrowserContext _context = null!;

    protected IPage Page { get; private set; } = null!;
    protected Uri BaseUri => _factory.ServerAddress;
    protected IServiceScopeFactory ScopeFactory => _factory.ScopeFactory;

    protected E2eTestBase(PlaywrightWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true });
        _context = await _browser.NewContextAsync();
        Page = await _context.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        await Page.CloseAsync();
        await _context.CloseAsync();
        await _browser.CloseAsync();
        _playwright.Dispose();
    }
}
