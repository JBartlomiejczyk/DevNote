using DevNote.Models;

namespace DevNote.Services;

public class WizardStateService
{
    private readonly Dictionary<string, HelperQuestionsResult> _helperQuestionsCache = new();

    public WizardData Data { get; private set; } = new();

    public void Reset()
    {
        Data = new WizardData();
        ClearHelperQuestionsCache();
    }

    public void LoadFromNote(ConversationNote note)
    {
        Data = new WizardData
        {
            Problem = note.Problem,
            Process = note.Process,
            TimeWaste = note.TimeWaste,
            InputData = note.InputData,
            Output = note.Output,
            Risks = note.Risks,
            Users = note.Users,
            Scale = note.Scale
        };

        ClearHelperQuestionsCache();
    }

    public bool TryGetCachedHelperQuestions(string cacheKey, out HelperQuestionsResult? result)
    {
        if (_helperQuestionsCache.TryGetValue(cacheKey, out var cached))
        {
            result = cached;
            return true;
        }

        result = null;
        return false;
    }

    public void SetCachedHelperQuestions(string cacheKey, HelperQuestionsResult result)
    {
        _helperQuestionsCache[cacheKey] = result;
    }

    public void ClearHelperQuestionsCache()
    {
        _helperQuestionsCache.Clear();
    }
}
