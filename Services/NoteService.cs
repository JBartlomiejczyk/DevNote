using DevNote.Data;
using DevNote.Models;

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
        var title = string.IsNullOrWhiteSpace(wizardData.Problem)
            ? "Notatka"
            : wizardData.Problem.Length > 80
                ? wizardData.Problem[..80] + "…"
                : wizardData.Problem;

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
}
