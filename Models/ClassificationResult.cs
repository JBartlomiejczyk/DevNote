namespace DevNote.Models;

public enum Classification
{
    A,
    B,
    C
}

public class ClassificationResult
{
    public Classification Classification { get; set; }
    public string Justification { get; set; } = string.Empty;
    public string Problem { get; set; } = string.Empty;
    public string Users { get; set; } = string.Empty;
    public string CurrentProcess { get; set; } = string.Empty;
    public string TimeWaste { get; set; } = string.Empty;
    public string InputData { get; set; } = string.Empty;
    public string ExpectedOutput { get; set; } = string.Empty;
    public string RecommendedPath { get; set; } = string.Empty;
    public string MvpScope { get; set; } = string.Empty;
    public string OutOfScope { get; set; } = string.Empty;
    public string AcceptanceCriteria { get; set; } = string.Empty;
    public string NextStep { get; set; } = string.Empty;
}
