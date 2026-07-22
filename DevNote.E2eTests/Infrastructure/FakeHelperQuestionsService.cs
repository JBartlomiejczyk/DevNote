using DevNote.Models;
using DevNote.Services;

namespace DevNote.E2eTests.Infrastructure;

public class FakeHelperQuestionsService : IHelperQuestionsService
{
    public Task<HelperQuestionsResult> GenerateAsync(
        HelperQuestionsRequest request,
        CancellationToken ct = default)
    {
        return Task.FromResult(new HelperQuestionsResult
        {
            Questions = Array.Empty<string>(),
            ContextHash = "test",
        });
    }
}
