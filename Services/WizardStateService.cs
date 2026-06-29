using DevNote.Models;

namespace DevNote.Services;

public class WizardStateService
{
    public WizardData Data { get; private set; } = new();

    public void Reset() => Data = new WizardData();

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
    }
}
