// Risk: Risk #5 — Edit-revert-reclassify end-to-end
// Seed: DevNote.E2eTests/Infrastructure/E2eTestBase.cs + UserHelper.cs
// Protects: opening a Completed note in edit mode reverts to Draft,
//           and after re-classification note is Completed again.

using DevNote.E2eTests.Infrastructure;
using Microsoft.Playwright;
using Xunit;

namespace DevNote.E2eTests.Tests;

public class EditRevertReclassifyTests : E2eTestBase, IClassFixture<PlaywrightWebApplicationFactory>
{
    public EditRevertReclassifyTests(PlaywrightWebApplicationFactory factory)
        : base(factory) { }

    [Fact]
    public async Task EditNote_CompletedNote_ShowsDraftAfterLoad_ThenCompletedAfterReclassify()
    {
        var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var email = $"editor-{ts}@test.com";

        // Setup: create user, seed a Completed note, log in
        var userId = await UserHelper.CreateUserAsync(ScopeFactory, email, "Test1234!");
        var noteId = Guid.NewGuid();
        await UserHelper.SeedCompletedNoteAsync(ScopeFactory, userId, noteId);
        await UserHelper.LoginAsync(Page, BaseUri, email, "Test1234!");

        // Assert initial state in /notes — Completed + B badge
        await Page.GotoAsync($"{BaseUri}notes");
        await Page.GetByRole(AriaRole.Heading, new() { Name = "Moje notatki" }).WaitForAsync();
        await Page.GetByText("Ukończona").WaitForAsync();
        await Page.GetByText("B").First.WaitForAsync();

        // Navigate to edit — triggers RevertToDraftAsync on the server
        await Page.GotoAsync($"{BaseUri}edit/{noteId}");
        await Page.GetByRole(AriaRole.Heading, new() { Name = "Edytuj notatkę" }).WaitForAsync();

        // Assert intermediate Draft state in /notes
        await Page.GotoAsync($"{BaseUri}notes");
        await Page.GetByRole(AriaRole.Heading, new() { Name = "Moje notatki" }).WaitForAsync();
        await Page.GetByText("Szkic").WaitForAsync();
        Assert.Empty(await Page.GetByText("Ukończona").AllAsync());
        Assert.Empty(await Page.GetByText("B").AllAsync());

        // Re-classify: navigate back to edit, expand Problem, update, click Klasyfikuj
        await Page.GotoAsync($"{BaseUri}edit/{noteId}");
        await Page.GetByRole(AriaRole.Heading, new() { Name = "Edytuj notatkę" }).WaitForAsync();

        // Wait for Blazor circuit before interacting
        await Page.Locator("body[data-blazor-ready='true']").WaitForAsync();

        var problemHeader = Page.GetByText("Problem", new() { Exact = true }).First;
        await problemHeader.ClickAsync();
        var problemField = Page.GetByPlaceholder("Opisz problem biznesowy, który chcesz rozwiązać");
        await problemField.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await problemField.FillAsync($"updated-problem-{ts}");

        await Page.GetByRole(AriaRole.Button, new() { Name = "Klasyfikuj" }).ClickAsync();
        await Page.GetByText("Notatka zaktualizowana.").WaitForAsync();

        // Assert final Completed state in /notes
        await Page.GotoAsync($"{BaseUri}notes");
        await Page.GetByRole(AriaRole.Heading, new() { Name = "Moje notatki" }).WaitForAsync();
        await Page.GetByText("Ukończona").WaitForAsync();
        await Page.GetByText("B").First.WaitForAsync();
    }
}
