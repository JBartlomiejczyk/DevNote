using DevNote.E2eTests.Infrastructure;
using Microsoft.Playwright;
using Xunit;

namespace DevNote.E2eTests.Tests;

public class UnauthenticatedRedirectTests : E2eTestBase, IClassFixture<PlaywrightWebApplicationFactory>
{
    public UnauthenticatedRedirectTests(PlaywrightWebApplicationFactory factory)
        : base(factory) { }

    [Fact]
    public async Task WizardPage_Unauthenticated_RedirectsToLoginPage()
    {
        await Page.GotoAsync(BaseUri.ToString());
        await Page.WaitForURLAsync("**/login**");
        Assert.True(await Page.GetByRole(AriaRole.Heading, new() { Name = "Logowanie" }).IsVisibleAsync());
    }

    [Fact]
    public async Task NotesPage_Unauthenticated_RedirectsToLoginPage()
    {
        await Page.GotoAsync($"{BaseUri}notes");
        await Page.WaitForURLAsync("**/login**");
        Assert.True(await Page.GetByRole(AriaRole.Heading, new() { Name = "Logowanie" }).IsVisibleAsync());
    }

    [Fact]
    public async Task EditNotePage_Unauthenticated_RedirectsToLoginPage()
    {
        await Page.GotoAsync($"{BaseUri}edit/{Guid.NewGuid()}");
        await Page.WaitForURLAsync("**/login**");
        Assert.True(await Page.GetByRole(AriaRole.Heading, new() { Name = "Logowanie" }).IsVisibleAsync());
    }
}
