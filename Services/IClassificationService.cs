using DevNote.Models;

namespace DevNote.Services;

public interface IClassificationService
{
    Task<ClassificationResult> ClassifyAsync(WizardData data, CancellationToken ct = default);
}
