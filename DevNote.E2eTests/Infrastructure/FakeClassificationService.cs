using DevNote.Models;
using DevNote.Services;

namespace DevNote.E2eTests.Infrastructure;

public class FakeClassificationService : IClassificationService
{
    public Task<ClassificationResult> ClassifyAsync(WizardData data, CancellationToken ct = default)
    {
        return Task.FromResult(new ClassificationResult
        {
            Classification = Classification.B,
            Justification = "[test-justification]",
            Problem = "[test]",
            Users = "[test]",
            CurrentProcess = "[test]",
            TimeWaste = "[test]",
            InputData = "[test]",
            ExpectedOutput = "[test]",
            RecommendedPath = "[test]",
            MvpScope = "[test]",
            OutOfScope = "[test]",
            AcceptanceCriteria = "[test]",
            NextStep = "[test]",
        });
    }
}
