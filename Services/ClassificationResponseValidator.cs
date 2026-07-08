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

            var classification = ParseClassification(root);

            return new ClassificationResult
            {
                Classification = classification,
                Justification = ReadRequiredText(root, "justification"),
                Problem = ReadRequiredText(root, "problem"),
                Users = ReadRequiredText(root, "users"),
                CurrentProcess = ReadRequiredText(root, "currentProcess"),
                TimeWaste = ReadRequiredText(root, "timeWaste"),
                InputData = ReadRequiredText(root, "inputData"),
                ExpectedOutput = ReadRequiredText(root, "expectedOutput"),
                RecommendedPath = ReadRequiredText(root, "recommendedPath"),
                MvpScope = ReadRequiredText(root, "mvpScope"),
                OutOfScope = ReadRequiredText(root, "outOfScope"),
                AcceptanceCriteria = ReadRequiredText(root, "acceptanceCriteria"),
                NextStep = ReadRequiredText(root, "nextStep")
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

    private static Classification ParseClassification(JsonElement root)
    {
        var classStr = root.GetProperty("classification").GetString();
        return classStr switch
        {
            "A" => Classification.A,
            "B" => Classification.B,
            "C" => Classification.C,
            _ => throw new ClassificationResponseValidationException("Odpowiedź klasyfikacji zawiera nieobsługiwaną wartość pola classification.")
        };
    }

    private static string ReadRequiredText(JsonElement root, string propertyName)
    {
        var value = root.GetProperty(propertyName).GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ClassificationResponseValidationException($"Odpowiedź klasyfikacji zawiera puste pole wymagane: {propertyName}.");
        }

        return value.Trim();
    }
}
