namespace DevNote.Models;

public class ConversationNote
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public NoteStatus Status { get; set; } = NoteStatus.Draft;

    // Classification
    public Classification? Classification { get; set; }
    public string Justification { get; set; } = string.Empty;

    // Wizard fields
    public string Problem { get; set; } = string.Empty;
    public string Process { get; set; } = string.Empty;
    public string TimeWaste { get; set; } = string.Empty;
    public string InputData { get; set; } = string.Empty;
    public string Output { get; set; } = string.Empty;
    public string Risks { get; set; } = string.Empty;
    public string Users { get; set; } = string.Empty;
    public string Scale { get; set; } = string.Empty;

    // Summary fields
    public string SummaryProblem { get; set; } = string.Empty;
    public string SummaryUsers { get; set; } = string.Empty;
    public string CurrentProcess { get; set; } = string.Empty;
    public string SummaryTimeWaste { get; set; } = string.Empty;
    public string SummaryInputData { get; set; } = string.Empty;
    public string ExpectedOutput { get; set; } = string.Empty;
    public string RecommendedPath { get; set; } = string.Empty;
    public string MvpScope { get; set; } = string.Empty;
    public string OutOfScope { get; set; } = string.Empty;
    public string AcceptanceCriteria { get; set; } = string.Empty;
    public string NextStep { get; set; } = string.Empty;

    // Timestamps
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation
    public ApplicationUser User { get; set; } = null!;
}
