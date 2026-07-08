using System.Text.Json;
using DevNote.Models;

namespace DevNote.Services;

public sealed class ClassificationResponseValidator
{
    public ClassificationResult ParseAndValidate(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var classStr = root.GetProperty("classification").GetString();
            var classification = classStr switch
            {
                "A" => Classification.A,
                "B" => Classification.B,
                "C" => Classification.C,
                _ => Classification.B
            };

            return new ClassificationResult
            {
                Classification = classification,
                Justification = root.GetProperty("justification").GetString() ?? string.Empty,
                Problem = root.GetProperty("problem").GetString() ?? string.Empty,
                Users = root.GetProperty("users").GetString() ?? string.Empty,
                CurrentProcess = root.GetProperty("currentProcess").GetString() ?? string.Empty,
                TimeWaste = root.GetProperty("timeWaste").GetString() ?? string.Empty,
                InputData = root.GetProperty("inputData").GetString() ?? string.Empty,
                ExpectedOutput = root.GetProperty("expectedOutput").GetString() ?? string.Empty,
                RecommendedPath = root.GetProperty("recommendedPath").GetString() ?? string.Empty,
                MvpScope = root.GetProperty("mvpScope").GetString() ?? string.Empty,
                OutOfScope = root.GetProperty("outOfScope").GetString() ?? string.Empty,
                AcceptanceCriteria = root.GetProperty("acceptanceCriteria").GetString() ?? string.Empty,
                NextStep = root.GetProperty("nextStep").GetString() ?? string.Empty
            };
        }
        catch (JsonException ex)
        {
            throw new ClassificationResponseValidationException("Odpowiedź klasyfikacji nie jest poprawnym JSON-em.", ex);
        }
        catch (KeyNotFoundException ex)
        {
            throw new ClassificationResponseValidationException("Odpowiedź klasyfikacji nie zawiera wymaganych pól.", ex);
        }
        catch (InvalidOperationException ex)
        {
            throw new ClassificationResponseValidationException("Odpowiedź klasyfikacji ma nieprawidłowy format pól.", ex);
        }
    }
}
