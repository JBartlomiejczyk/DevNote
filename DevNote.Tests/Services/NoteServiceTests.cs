using DevNote.Data;
using DevNote.Models;
using DevNote.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DevNote.Tests.Services;

public class NoteServiceTests
{
    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;
        return new ApplicationDbContext(options);
    }

    private static ConversationNote SeedNote(ApplicationDbContext db, string userId)
    {
        var note = new ConversationNote
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = "Test note",
            Status = NoteStatus.Completed,
            Classification = Classification.A,
            Justification = "Old justification",
            SummaryProblem = "Old problem",
            SummaryUsers = "Old users",
            CurrentProcess = "Old process",
            SummaryTimeWaste = "Old time waste",
            SummaryInputData = "Old input data",
            ExpectedOutput = "Old output",
            RecommendedPath = "Old path",
            MvpScope = "Old MVP scope",
            OutOfScope = "Old out of scope",
            AcceptanceCriteria = "Old criteria",
            NextStep = "Old next step",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.ConversationNotes.Add(note);
        db.SaveChanges();
        return note;
    }

    [Fact]
    public async Task GetNoteAsync_WrongOwner_ReturnsNull()
    {
        using var db = CreateDb();
        var note = SeedNote(db, "user-a");
        var sut = new NoteService(db);

        var result = await sut.GetNoteAsync(note.Id, "user-b");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetNoteAsync_CorrectOwner_ReturnsNote()
    {
        using var db = CreateDb();
        var note = SeedNote(db, "user-a");
        var sut = new NoteService(db);

        var result = await sut.GetNoteAsync(note.Id, "user-a");

        Assert.NotNull(result);
        Assert.Equal(note.Id, result.Id);
    }

    [Fact]
    public async Task UpdateNoteAsync_WrongOwner_ThrowsInvalidOperation()
    {
        using var db = CreateDb();
        var note = SeedNote(db, "user-a");
        var sut = new NoteService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.UpdateNoteAsync(note.Id, "user-b", new WizardData(), new ClassificationResult()));
    }

    [Fact]
    public async Task RevertToDraftAsync_WrongOwner_ThrowsInvalidOperation()
    {
        using var db = CreateDb();
        var note = SeedNote(db, "user-a");
        var sut = new NoteService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.RevertToDraftAsync(note.Id, "user-b"));
    }

    [Fact]
    public async Task RevertToDraftAsync_CompletedNote_ClearsGeneratedOutput()
    {
        using var db = CreateDb();
        var note = SeedNote(db, "user-a");
        var sut = new NoteService(db);

        await sut.RevertToDraftAsync(note.Id, "user-a");

        Assert.Equal(NoteStatus.Draft, note.Status);
        Assert.Null(note.Classification);
        Assert.Empty(note.Justification);
        Assert.Empty(note.SummaryProblem);
        Assert.Empty(note.SummaryUsers);
        Assert.Empty(note.CurrentProcess);
        Assert.Empty(note.SummaryTimeWaste);
        Assert.Empty(note.SummaryInputData);
        Assert.Empty(note.ExpectedOutput);
        Assert.Empty(note.RecommendedPath);
        Assert.Empty(note.MvpScope);
        Assert.Empty(note.OutOfScope);
        Assert.Empty(note.AcceptanceCriteria);
        Assert.Empty(note.NextStep);
    }

    [Fact]
    public async Task RevertToDraftAsync_CleanDraft_DoesNotChangeUpdatedAt()
    {
        using var db = CreateDb();
        var note = SeedNote(db, "user-a");
        note.Status = NoteStatus.Draft;
        note.Classification = null;
        note.Justification = string.Empty;
        note.SummaryProblem = string.Empty;
        note.SummaryUsers = string.Empty;
        note.CurrentProcess = string.Empty;
        note.SummaryTimeWaste = string.Empty;
        note.SummaryInputData = string.Empty;
        note.ExpectedOutput = string.Empty;
        note.RecommendedPath = string.Empty;
        note.MvpScope = string.Empty;
        note.OutOfScope = string.Empty;
        note.AcceptanceCriteria = string.Empty;
        note.NextStep = string.Empty;
        note.UpdatedAt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        await db.SaveChangesAsync();
        var previousUpdatedAt = note.UpdatedAt;
        var sut = new NoteService(db);

        await sut.RevertToDraftAsync(note.Id, "user-a");

        Assert.Equal(previousUpdatedAt, note.UpdatedAt);
    }

    [Fact]
    public async Task EditLifecycle_ReclassificationReplacesGeneratedOutputOnSameNote()
    {
        using var db = CreateDb();
        var note = SeedNote(db, "user-a");
        var noteId = note.Id;
        var sut = new NoteService(db);

        await sut.RevertToDraftAsync(noteId, "user-a");

        Assert.Equal(NoteStatus.Draft, note.Status);
        Assert.Null(note.Classification);
        Assert.Empty(note.SummaryProblem);

        var wizardData = new WizardData
        {
            Problem = "New problem",
            Process = "New process",
            TimeWaste = "New time waste",
            InputData = "New input data",
            Output = "New output",
            Risks = "New risks",
            Users = "New users",
            Scale = "New scale"
        };
        var classificationResult = new ClassificationResult
        {
            Classification = Classification.C,
            Justification = "New justification",
            Problem = "New summary problem",
            Users = "New summary users",
            CurrentProcess = "New current process",
            TimeWaste = "New summary time waste",
            InputData = "New summary input data",
            ExpectedOutput = "New expected output",
            RecommendedPath = "New recommended path",
            MvpScope = "New MVP scope",
            OutOfScope = "New out of scope",
            AcceptanceCriteria = "New acceptance criteria",
            NextStep = "New next step"
        };

        var updated = await sut.UpdateNoteAsync(noteId, "user-a", wizardData, classificationResult);

        Assert.Equal(noteId, updated.Id);
        Assert.Equal(1, await db.ConversationNotes.CountAsync());
        Assert.Equal(NoteStatus.Completed, updated.Status);
        Assert.Equal(Classification.C, updated.Classification);
        Assert.Equal("New justification", updated.Justification);
        Assert.Equal("New problem", updated.Problem);
        Assert.Equal("New process", updated.Process);
        Assert.Equal("New time waste", updated.TimeWaste);
        Assert.Equal("New input data", updated.InputData);
        Assert.Equal("New output", updated.Output);
        Assert.Equal("New risks", updated.Risks);
        Assert.Equal("New users", updated.Users);
        Assert.Equal("New scale", updated.Scale);
        Assert.Equal("New summary problem", updated.SummaryProblem);
        Assert.Equal("New summary users", updated.SummaryUsers);
        Assert.Equal("New current process", updated.CurrentProcess);
        Assert.Equal("New summary time waste", updated.SummaryTimeWaste);
        Assert.Equal("New summary input data", updated.SummaryInputData);
        Assert.Equal("New expected output", updated.ExpectedOutput);
        Assert.Equal("New recommended path", updated.RecommendedPath);
        Assert.Equal("New MVP scope", updated.MvpScope);
        Assert.Equal("New out of scope", updated.OutOfScope);
        Assert.Equal("New acceptance criteria", updated.AcceptanceCriteria);
        Assert.Equal("New next step", updated.NextStep);
    }
}
