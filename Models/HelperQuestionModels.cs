namespace DevNote.Models;

public enum WizardSectionKey
{
    Problem = 0,
    Process = 1,
    TimeWaste = 2,
    InputData = 3,
    Output = 4,
    Risks = 5,
    Users = 6,
    Scale = 7
}

public sealed record WizardSectionSnapshot(
    WizardSectionKey SectionKey,
    string SectionTitle,
    string Value);

public sealed class HelperQuestionsRequest
{
    public WizardSectionKey SectionKey { get; init; }
    public WizardData Data { get; init; } = new();
    public int QuestionCount { get; init; } = 4;
    public string Locale { get; init; } = "pl-PL";
}

public sealed class HelperQuestionsResult
{
    public IReadOnlyList<string> Questions { get; init; } = Array.Empty<string>();
    public string ContextHash { get; init; } = string.Empty;
    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class HelperQuestionsUiState
{
    public bool IsLoading { get; set; }
    public string? Error { get; set; }
    public IReadOnlyList<string> Questions { get; set; } = Array.Empty<string>();
    public string? LastContextHash { get; set; }
}
