using DevNote.Data;
using DevNote.Models;
using Microsoft.EntityFrameworkCore;

namespace DevNote.Services;

public class NoteService
{
    private readonly ApplicationDbContext _db;

    public NoteService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<ConversationNote> CreateNoteAsync(
        string userId, WizardData wizardData, ClassificationResult result)
    {
        var now = DateTimeOffset.UtcNow;
        var title = BuildTitle(wizardData.Problem);

        var note = new ConversationNote
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = title,
            Status = NoteStatus.Completed,
            Classification = result.Classification,
            Justification = result.Justification,
            Problem = wizardData.Problem,
            Process = wizardData.Process,
            TimeWaste = wizardData.TimeWaste,
            InputData = wizardData.InputData,
            Output = wizardData.Output,
            Risks = wizardData.Risks,
            Users = wizardData.Users,
            Scale = wizardData.Scale,
            SummaryProblem = result.Problem,
            SummaryUsers = result.Users,
            CurrentProcess = result.CurrentProcess,
            SummaryTimeWaste = result.TimeWaste,
            SummaryInputData = result.InputData,
            ExpectedOutput = result.ExpectedOutput,
            RecommendedPath = result.RecommendedPath,
            MvpScope = result.MvpScope,
            OutOfScope = result.OutOfScope,
            AcceptanceCriteria = result.AcceptanceCriteria,
            NextStep = result.NextStep,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.ConversationNotes.Add(note);
        await _db.SaveChangesAsync();

        return note;
    }

    public async Task<List<ConversationNote>> GetNotesForUserAsync(string userId)
    {
        return await _db.ConversationNotes
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();
    }

    public async Task<ConversationNote?> GetNoteAsync(Guid noteId, string userId)
    {
        return await _db.ConversationNotes
            .FirstOrDefaultAsync(n => n.Id == noteId && n.UserId == userId);
    }

    public async Task<ConversationNote> UpdateNoteAsync(
        Guid noteId, string userId, WizardData wizardData, ClassificationResult result)
    {
        var note = await _db.ConversationNotes
            .FirstOrDefaultAsync(n => n.Id == noteId && n.UserId == userId)
            ?? throw new InvalidOperationException($"Note {noteId} not found for user.");

        note.Title = BuildTitle(wizardData.Problem);
        note.Status = NoteStatus.Completed;
        note.Classification = result.Classification;
        note.Justification = result.Justification;
        note.Problem = wizardData.Problem;
        note.Process = wizardData.Process;
        note.TimeWaste = wizardData.TimeWaste;
        note.InputData = wizardData.InputData;
        note.Output = wizardData.Output;
        note.Risks = wizardData.Risks;
        note.Users = wizardData.Users;
        note.Scale = wizardData.Scale;
        note.SummaryProblem = result.Problem;
        note.SummaryUsers = result.Users;
        note.CurrentProcess = result.CurrentProcess;
        note.SummaryTimeWaste = result.TimeWaste;
        note.SummaryInputData = result.InputData;
        note.ExpectedOutput = result.ExpectedOutput;
        note.RecommendedPath = result.RecommendedPath;
        note.MvpScope = result.MvpScope;
        note.OutOfScope = result.OutOfScope;
        note.AcceptanceCriteria = result.AcceptanceCriteria;
        note.NextStep = result.NextStep;
        note.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return note;
    }

    public async Task RevertToDraftAsync(Guid noteId, string userId)
    {
        var note = await _db.ConversationNotes
            .FirstOrDefaultAsync(n => n.Id == noteId && n.UserId == userId)
            ?? throw new InvalidOperationException($"Note {noteId} not found for user.");

        if (note.Status == NoteStatus.Completed)
        {
            note.Status = NoteStatus.Draft;
            note.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

    private static string BuildTitle(string problem)
    {
        if (string.IsNullOrWhiteSpace(problem)) return "Notatka";
        return problem.Length > 80 ? problem[..80] + "…" : problem;
    }
}
