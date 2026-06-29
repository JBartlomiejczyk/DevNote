namespace DevNote.Models;

public class WizardData
{
    private static readonly WizardSectionKey[] SectionOrder =
    [
        WizardSectionKey.Problem,
        WizardSectionKey.Process,
        WizardSectionKey.TimeWaste,
        WizardSectionKey.InputData,
        WizardSectionKey.Output,
        WizardSectionKey.Risks,
        WizardSectionKey.Users,
        WizardSectionKey.Scale
    ];

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

    public IReadOnlyList<WizardSectionSnapshot> GetPriorSectionSnapshots(WizardSectionKey currentSection)
    {
        var snapshots = new List<WizardSectionSnapshot>();
        foreach (var section in SectionOrder)
        {
            if (section == currentSection)
            {
                break;
            }

            var value = GetSectionValue(section).Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            snapshots.Add(new WizardSectionSnapshot(section, GetSectionTitle(section), value));
        }

        return snapshots;
    }

    public string BuildPriorContextHashInput(WizardSectionKey currentSection)
    {
        var snapshots = GetPriorSectionSnapshots(currentSection);
        if (snapshots.Count == 0)
        {
            return $"section:{currentSection}|context:none";
        }

        var lines = snapshots
            .Select(snapshot => $"{snapshot.SectionKey}:{snapshot.Value.Replace("\r\n", "\n").Trim()}");

        return string.Join('\n', lines);
    }

    public static string GetSectionTitle(WizardSectionKey section) =>
        section switch
        {
            WizardSectionKey.Problem => "Problem",
            WizardSectionKey.Process => "Obecny proces",
            WizardSectionKey.TimeWaste => "Strata czasu",
            WizardSectionKey.InputData => "Dane wejściowe",
            WizardSectionKey.Output => "Oczekiwany wynik",
            WizardSectionKey.Risks => "Ryzyka",
            WizardSectionKey.Users => "Użytkownicy",
            WizardSectionKey.Scale => "Skala",
            _ => throw new ArgumentOutOfRangeException(nameof(section), section, "Unsupported section key.")
        };

    public string GetSectionValue(WizardSectionKey section) =>
        section switch
        {
            WizardSectionKey.Problem => Problem,
            WizardSectionKey.Process => Process,
            WizardSectionKey.TimeWaste => TimeWaste,
            WizardSectionKey.InputData => InputData,
            WizardSectionKey.Output => Output,
            WizardSectionKey.Risks => Risks,
            WizardSectionKey.Users => Users,
            WizardSectionKey.Scale => Scale,
            _ => throw new ArgumentOutOfRangeException(nameof(section), section, "Unsupported section key.")
        };
}
