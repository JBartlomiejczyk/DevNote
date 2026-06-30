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

    public void ResetUiStateMap(Dictionary<WizardSectionKey, HelperQuestionsUiState> states)
    {
        states.Clear();
        foreach (var section in Enum.GetValues<WizardSectionKey>())
        {
            states[section] = new HelperQuestionsUiState();
        }
    }

    public Dictionary<WizardSectionKey, HelperQuestionsUiState> CreateUiStateMap()
    {
        var map = new Dictionary<WizardSectionKey, HelperQuestionsUiState>();
        ResetUiStateMap(map);
        return map;
    }

    public async Task LoadSectionUiStateAsync(
        Dictionary<WizardSectionKey, HelperQuestionsUiState> states,
        WizardSectionKey sectionKey,
        bool forceRefresh,
        CancellationToken ct = default)
    {
        var sectionState = states[sectionKey];
        sectionState.IsLoading = true;
        sectionState.Error = null;

        try
        {
            var generated = await GetForSectionAsync(sectionKey, forceRefresh, ct: ct);
            sectionState.Questions = generated.Questions;
            sectionState.LastContextHash = generated.ContextHash;
        }
        catch (Exception)
        {
            sectionState.Error = "Nie udało się wygenerować pytań pomocniczych. Spróbuj ponownie.";
            sectionState.Questions = Array.Empty<string>();
        }
        finally
        {
            sectionState.IsLoading = false;
        }
    }

    private static string BuildCacheKey(WizardSectionKey sectionKey, string contextHash) =>
        $"{sectionKey}:{contextHash}";
}
