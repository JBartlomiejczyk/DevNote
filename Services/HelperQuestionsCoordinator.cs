using DevNote.Models;

namespace DevNote.Services;

public class HelperQuestionsCoordinator
{
    private readonly WizardStateService _wizardState;
    private readonly HelperQuestionsService _helperQuestionsService;

    public HelperQuestionsCoordinator(
        WizardStateService wizardState,
        HelperQuestionsService helperQuestionsService)
    {
        _wizardState = wizardState;
        _helperQuestionsService = helperQuestionsService;
    }

    public async Task<HelperQuestionsResult> GetForSectionAsync(
        WizardSectionKey sectionKey,
        bool forceRefresh = false,
        int questionCount = 4,
        string locale = "pl-PL",
        CancellationToken ct = default)
    {
        var contextHashInput = _wizardState.Data.BuildPriorContextHashInput(sectionKey);
        var contextHash = HelperQuestionsService.BuildContextHash(
            sectionKey,
            contextHashInput,
            questionCount,
            locale);

        var cacheKey = BuildCacheKey(sectionKey, contextHash);
        if (!forceRefresh && _wizardState.TryGetCachedHelperQuestions(cacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        var generated = await _helperQuestionsService.GenerateAsync(
            new HelperQuestionsRequest
            {
                SectionKey = sectionKey,
                Data = _wizardState.Data,
                QuestionCount = questionCount,
                Locale = locale
            },
            ct);

        _wizardState.SetCachedHelperQuestions(cacheKey, generated);
        return generated;
    }

    private static string BuildCacheKey(WizardSectionKey sectionKey, string contextHash) =>
        $"{sectionKey}:{contextHash}";
}
