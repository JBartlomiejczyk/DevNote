using DevNote.E2eTests.Infrastructure;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace DevNote.E2eTests.Tests;

public class CrossUserNoteAccessTests : E2eTestBase, IClassFixture<PlaywrightWebApplicationFactory>
{
    public CrossUserNoteAccessTests(PlaywrightWebApplicationFactory factory)
        : base(factory) { }

    [Fact]
    public async Task EditNotePage_OtherUsersNote_RedirectsToNotesList()
    {
        var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var userAId = await UserHelper.CreateUserAsync(ScopeFactory, $"user-a-{ts}@test.com", "Test1234!");
        await UserHelper.CreateUserAsync(ScopeFactory, $"user-b-{ts}@test.com", "Test1234!");

        var noteId = Guid.NewGuid();
        await UserHelper.SeedCompletedNoteAsync(ScopeFactory, userAId, noteId);

        await UserHelper.LoginAsync(Page, BaseUri, $"user-b-{ts}@test.com", "Test1234!");

        await Page.GotoAsync($"{BaseUri}edit/{noteId}");
        await Page.WaitForURLAsync("**/notes**");

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Edytuj notatkę" })).Not.ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Moje notatki" })).ToBeVisibleAsync();
    }
}
