using DevNote.Models;

namespace DevNote.Services;

public interface IHelperQuestionsService
{
    Task<HelperQuestionsResult> GenerateAsync(
        HelperQuestionsRequest request,
        CancellationToken ct = default);
}
