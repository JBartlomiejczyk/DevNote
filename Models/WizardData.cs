namespace DevNote.Models;

public class WizardData
{
    public string Problem { get; set; } = string.Empty;
    public string Process { get; set; } = string.Empty;
    public string TimeWaste { get; set; } = string.Empty;
    public string InputData { get; set; } = string.Empty;
    public string Output { get; set; } = string.Empty;
    public string Risks { get; set; } = string.Empty;
    public string Users { get; set; } = string.Empty;
    public string Scale { get; set; } = string.Empty;

    public bool HasAnyContent() =>
        !string.IsNullOrWhiteSpace(Problem) ||
        !string.IsNullOrWhiteSpace(Process) ||
        !string.IsNullOrWhiteSpace(TimeWaste) ||
        !string.IsNullOrWhiteSpace(InputData) ||
        !string.IsNullOrWhiteSpace(Output) ||
        !string.IsNullOrWhiteSpace(Risks) ||
        !string.IsNullOrWhiteSpace(Users) ||
        !string.IsNullOrWhiteSpace(Scale);
}
