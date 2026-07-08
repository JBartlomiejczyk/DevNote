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
        Assert.Equal("uzasadnienie", result.Justification);
        Assert.Equal("problem", result.Problem);
        Assert.Equal("uzytkownicy", result.Users);
        Assert.Equal("proces", result.CurrentProcess);
        Assert.Equal("strata", result.TimeWaste);
        Assert.Equal("wejscie", result.InputData);
        Assert.Equal("wyjscie", result.ExpectedOutput);
        Assert.Equal("sciezka", result.RecommendedPath);
        Assert.Equal("mvp", result.MvpScope);
        Assert.Equal("poza", result.OutOfScope);
        Assert.Equal("kryteria", result.AcceptanceCriteria);
        Assert.Equal("nastepny", result.NextStep);
    }

    [Fact]
    public void ParseAndValidate_InvalidJson_ThrowsValidationException()
    {
        var invalidJson = "{ this-is-not-valid-json }";

        Assert.Throws<ClassificationResponseValidationException>(() => _validator.ParseAndValidate(invalidJson));
    }

    [Fact]
    public void ParseAndValidate_UnknownClassification_ThrowsValidationException()
    {
        var json = """
            {
              "classification": "D",
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

        Assert.Throws<ClassificationResponseValidationException>(() => _validator.ParseAndValidate(json));
    }

    [Fact]
    public void ParseAndValidate_MissingRequiredField_ThrowsValidationException()
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
              "acceptanceCriteria": "kryteria",
              "nextStep": "nastepny"
            }
            """;

        Assert.Throws<ClassificationResponseValidationException>(() => _validator.ParseAndValidate(json));
    }

    [Fact]
    public void ParseAndValidate_EmptyRequiredField_ThrowsValidationException()
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
              "outOfScope": " ",
              "acceptanceCriteria": "kryteria",
              "nextStep": "nastepny"
            }
            """;

        Assert.Throws<ClassificationResponseValidationException>(() => _validator.ParseAndValidate(json));
    }
}
