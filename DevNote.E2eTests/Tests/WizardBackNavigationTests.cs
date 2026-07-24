using DevNote.E2eTests.Infrastructure;
using Microsoft.Playwright;
using Xunit;

namespace DevNote.E2eTests.Tests;

public class WizardBackNavigationTests : E2eTestBase, IClassFixture<PlaywrightWebApplicationFactory>
{
    public WizardBackNavigationTests(PlaywrightWebApplicationFactory factory)
        : base(factory) { }

    [Fact]
    public async Task WizardBackNavigation_SectionToggleFlow_DoesNotRedirectOrCrash()
    {
        var pageErrors = new List<string>();
        Page.PageError += (_, error) => pageErrors.Add(error);

        var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var email = $"wizard-user-{ts}@test.com";

        await UserHelper.CreateUserAsync(ScopeFactory, email, "Test1234!");
        await UserHelper.LoginAsync(Page, BaseUri, email, "Test1234!");

        // Wait for Blazor Server circuit to connect and first render to complete
        await Page.Locator("body[data-blazor-ready='true']").WaitForAsync();

        var problemValue = $"problem-{ts}";
        var processValue = $"process-{ts}";

        var problemHeader = Page.GetByText("Problem", new() { Exact = true }).First;
        await problemHeader.ClickAsync();
        var problemField = Page.GetByPlaceholder("Opisz problem biznesowy, który chcesz rozwiązać");
        await problemField.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await problemField.FillAsync(problemValue);

        var processHeader = Page.GetByText("Obecny proces", new() { Exact = true }).First;
        await processHeader.ClickAsync();
        var processField = Page.GetByPlaceholder("Jak wygląda obecny proces? Kto jest zaangażowany?");
        await processField.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await processField.FillAsync(processValue);

        await problemHeader.ClickAsync();
        await processHeader.ClickAsync();
        await problemHeader.ClickAsync();
        await problemField.WaitForAsync(new() { State = WaitForSelectorState.Visible });

        Assert.True(pageErrors.Count == 0, $"Unexpected runtime errors: {string.Join(" | ", pageErrors)}");
        Assert.StartsWith(BaseUri.ToString(), Page.Url, StringComparison.OrdinalIgnoreCase);
    }
}
