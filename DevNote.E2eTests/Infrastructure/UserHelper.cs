using DevNote.Data;
using DevNote.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace DevNote.E2eTests.Infrastructure;

public static class UserHelper
{
    public static async Task<string> CreateUserAsync(
        IServiceScopeFactory scopeFactory,
        string email,
        string password)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser { UserName = email, Email = email };
        var result = await userManager.CreateAsync(user, password);

        if (!result.Succeeded)
            throw new InvalidOperationException(
                $"Failed to create user '{email}': {string.Join(", ", result.Errors.Select(e => e.Description))}");

        return user.Id;
    }

    public static async Task SeedCompletedNoteAsync(
        IServiceScopeFactory scopeFactory,
        string userId,
        Guid noteId)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        db.ConversationNotes.Add(new ConversationNote
        {
            Id = noteId,
            UserId = userId,
            Title = "Test note",
            Status = NoteStatus.Completed,
            Classification = Classification.B,
            Justification = "[seed]",
            Problem = "[seed]",
            Process = "[seed]",
            TimeWaste = "[seed]",
            InputData = "[seed]",
            Output = "[seed]",
            Risks = "[seed]",
            Users = "[seed]",
            Scale = "[seed]",
            SummaryProblem = "[seed]",
            SummaryUsers = "[seed]",
            CurrentProcess = "[seed]",
            SummaryTimeWaste = "[seed]",
            SummaryInputData = "[seed]",
            ExpectedOutput = "[seed]",
            RecommendedPath = "[seed]",
            MvpScope = "[seed]",
            OutOfScope = "[seed]",
            AcceptanceCriteria = "[seed]",
            NextStep = "[seed]",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });

        await db.SaveChangesAsync();
    }

    public static async Task LoginAsync(
        IPage page,
        Uri baseUri,
        string email,
        string password)
    {
        _ = password; // unused — backdoor bypasses password check

        var loginUrl = $"{baseUri}api/test/login?email={Uri.EscapeDataString(email)}";
        var response = await page.GotoAsync(loginUrl);
        if (response is null || !response.Ok)
            throw new InvalidOperationException(
                $"Backdoor login for '{email}' failed while navigating to {loginUrl}. " +
                $"HTTP status: {response?.Status}");

        // Verify auth actually took hold through browser navigation.
        var whoamiUrl = $"{baseUri}api/test/whoami";
        await page.GotoAsync(whoamiUrl);
        var identity = await page.InnerTextAsync("body");
        if (identity.Contains("anonymous"))
            throw new InvalidOperationException(
                $"Backdoor login for '{email}' succeeded but cookie wasn't honoured in browser context: whoami='{identity}'");

        // Navigate to root so callers have a clean starting page
        await page.GotoAsync(baseUri.ToString());
    }
}
