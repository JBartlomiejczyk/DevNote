using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DevNote.Models;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace DevNote.Services;

public sealed class HelperQuestionsConfigurationException : InvalidOperationException
{
    public HelperQuestionsConfigurationException(string message) : base(message)
    {
    }
}

public sealed class HelperQuestionsSchemaException : Exception
{
    public HelperQuestionsSchemaException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

public sealed class HelperQuestionsResponseException : Exception
{
    public HelperQuestionsResponseException(string message)
        : base(message)
    {
    }
}

public class HelperQuestionsService : IHelperQuestionsService
{
    private readonly AzureOpenAIOptions _options;
    private readonly ILogger<HelperQuestionsService> _logger;

    private static readonly string SystemPrompt = """
        Jesteś asystentem prowadzącym developera przez rozmowę biznesową.
        Dla wskazanej sekcji wygeneruj od 3 do 5 pytań pomocniczych po polsku.
        Pytania mają:
        - być krótkie i konkretne,
        - pogłębiać brakujące informacje,
        - unikać duplikatów i truizmów,
        - nawiązywać do dostarczonego kontekstu z poprzednich sekcji.
        Zwróć wyłącznie JSON zgodny ze schematem.
        """;

    private static readonly BinaryData JsonSchema = BinaryData.FromBytes("""
        {
          "type": "object",
          "properties": {
            "questions": {
              "type": "array",
              "minItems": 3,
              "maxItems": 5,
              "items": {
                "type": "string",
                "minLength": 1
              }
            }
          },
          "required": ["questions"],
          "additionalProperties": false
        }
        """u8.ToArray());

    public HelperQuestionsService(
        IOptions<AzureOpenAIOptions> options,
        ILogger<HelperQuestionsService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<HelperQuestionsResult> GenerateAsync(
        HelperQuestionsRequest request,
        CancellationToken ct = default)
    {
        ValidateConfiguration();
        ValidateRequest(request);

        var contextSnapshots = request.Data.GetPriorSectionSnapshots(request.SectionKey);
        var contextHashInput = request.Data.BuildPriorContextHashInput(request.SectionKey);
        var contextHash = BuildContextHash(request.SectionKey, contextHashInput, request.QuestionCount, request.Locale);

        var userMessage = BuildUserMessage(request, contextSnapshots);

        var client = LlmChatClientFactory.Create(_options);
        var chatOptions = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "helper_questions",
                jsonSchema: JsonSchema,
                jsonSchemaIsStrict: true)
        };

        var completion = await client.CompleteChatAsync(
            [
                new SystemChatMessage(SystemPrompt),
                new UserChatMessage(userMessage)
            ],
            chatOptions,
            ct);

        if (completion.Value.Content.Count == 0 ||
            string.IsNullOrWhiteSpace(completion.Value.Content[0].Text))
        {
            throw new HelperQuestionsResponseException("Model nie zwrocil tresci pytań pomocniczych.");
        }

        var json = completion.Value.Content[0].Text;
        _logger.LogDebug(
            "Helper questions response for {Section}: {Json}",
            request.SectionKey,
            json);

        var questions = ParseQuestions(json);

        return new HelperQuestionsResult
        {
            Questions = questions,
            ContextHash = contextHash,
            GeneratedAt = DateTimeOffset.UtcNow
        };
    }

    private void ValidateConfiguration()
    {
        try
        {
            LlmChatClientFactory.ValidateConfiguration(_options, nameof(HelperQuestionsService));
        }
        catch (InvalidOperationException ex)
        {
            throw new HelperQuestionsConfigurationException(ex.Message);
        }
    }

    private static void ValidateRequest(HelperQuestionsRequest request)
    {
        if (request.QuestionCount is < 3 or > 5)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.QuestionCount),
                request.QuestionCount,
                "QuestionCount must be between 3 and 5.");
        }

        if (request.Data is null)
        {
            throw new ArgumentNullException(nameof(request.Data));
        }
    }

    private static string BuildUserMessage(
        HelperQuestionsRequest request,
        IReadOnlyList<WizardSectionSnapshot> contextSnapshots)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Sekcja docelowa: {WizardData.GetSectionTitle(request.SectionKey)}");
        builder.AppendLine($"Liczba pytań: {request.QuestionCount}");
        builder.AppendLine($"Język: {request.Locale}");
        builder.AppendLine();
        builder.AppendLine("Kontekst z poprzednich sekcji:");

        if (contextSnapshots.Count == 0)
        {
            builder.AppendLine("- Brak wypełnionego kontekstu z poprzednich sekcji");
        }
        else
        {
            foreach (var snapshot in contextSnapshots)
            {
                builder.AppendLine($"## {snapshot.SectionTitle}");
                builder.AppendLine(snapshot.Value);
                builder.AppendLine();
            }
        }

        return builder.ToString();
    }

    private static IReadOnlyList<string> ParseQuestions(string json)
    {
        JsonElement root;
        try
        {
            using var doc = JsonDocument.Parse(json);
            root = doc.RootElement.Clone();
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            throw new HelperQuestionsSchemaException("Nie udało się sparsować odpowiedzi helper questions jako poprawnego JSON.", ex);
        }

        if (!root.TryGetProperty("questions", out var questionsElement) ||
            questionsElement.ValueKind != JsonValueKind.Array)
        {
            throw new HelperQuestionsSchemaException("Odpowiedź helper questions nie zawiera poprawnego pola `questions`.");
        }

        var questions = questionsElement
            .EnumerateArray()
            .Select(item => item.GetString()?.Trim() ?? string.Empty)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToList();

        if (questions.Count is < 3 or > 5)
        {
            throw new HelperQuestionsResponseException(
                $"Odpowiedź helper questions musi zawierać 3-5 pytań, otrzymano: {questions.Count}.");
        }

        return questions;
    }

    public static string BuildContextHash(
        WizardSectionKey sectionKey,
        string contextHashInput,
        int questionCount,
        string locale)
    {
        var hashInput = $"section:{sectionKey}|{contextHashInput}|count:{questionCount}|locale:{locale}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(hashInput));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
