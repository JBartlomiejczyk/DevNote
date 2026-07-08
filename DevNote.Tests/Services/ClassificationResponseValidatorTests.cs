using DevNote.Models;
using DevNote.Services;
using Xunit;

namespace DevNote.Tests.Services;

public class ClassificationResponseValidatorTests
{
    private readonly ClassificationResponseValidator _validator = new();

    [Fact]
    public void ParseAndValidate_ValidPayload_MapsToClassificationResult()
    {
        var json = """
            {
              "classification": "A",
              "justification": "uzasadnienie",
              "problem": "problem",
              "users": "uzytkownicy",
              "currentProcess": "proces",
              "timeWaste": "strata",
              "inputData": "wejscie",
              "expectedOutput": "wyjscie",
              "recommendedPath": "sciezka",
              "mvpScope": "mvp",
              "outOfScope": "poza",
              "acceptanceCriteria": "kryteria",
              "nextStep": "nastepny"
            }
            """;

        var result = _validator.ParseAndValidate(json);

        Assert.Equal(Classification.A, result.Classification);
        Assert.Equal("problem", result.Problem);
        Assert.Equal("nastepny", result.NextStep);
    }

    [Fact]
    public void ParseAndValidate_InvalidJson_ThrowsValidationException()
    {
        var invalidJson = "{ this-is-not-valid-json }";

        Assert.Throws<ClassificationResponseValidationException>(() => _validator.ParseAndValidate(invalidJson));
    }
}
