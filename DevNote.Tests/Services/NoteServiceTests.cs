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
}
