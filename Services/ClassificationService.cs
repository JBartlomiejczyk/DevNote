using System.ClientModel;
using System.Text.Json;
using Azure.AI.OpenAI;
using DevNote.Models;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace DevNote.Services;

public class ClassificationService
{
    private readonly AzureOpenAIOptions _options;
    private readonly ILogger<ClassificationService> _logger;

    private static readonly string SystemPrompt = """
        Jesteś ekspertem od klasyfikacji problemów biznesowych. Na podstawie opisu problemu dokonaj klasyfikacji według poniższych kategorii:

        A = Małe lokalne rozwiązanie — problem można rozwiązać za pomocą skryptu, arkusza kalkulacyjnego, automatyzacji lokalnej lub zmiany procesu. NIE wymaga budowy aplikacji. Dla tej kategorii ZAWSZE zasugeruj konkretne rozwiązania nie-kodowe (zmiana procesu, arkusz, automatyzacja w istniejących narzędziach).

        B = Rozwiązanie departamentalne — wewnętrzne narzędzie o ograniczonym zakresie, obsługujące jeden dział lub zespół. Umiarkowana złożoność.

        C = Duże rozwiązanie — obejmuje wrażliwe dane, wielu użytkowników z różnych działów, lub wymaga formalnej weryfikacji bezpieczeństwa/prawnej.

        ZASADY KLASYFIKACJI:
        - Klasyfikuj konserwatywnie: preferuj A nad B, B nad C
        - Jeśli problem można rozwiązać BEZ pisania kodu — to jest A
        - Nigdy nie przypisuj C bez wyraźnych przesłanek (wrażliwe dane, duża skala, wymogi prawne)

        ODPOWIEDŹ: Zwróć JSON zgodny z podanym schematem. Wszystkie pola tekstowe wypełnij po polsku.
        """;

    private static readonly BinaryData JsonSchema = BinaryData.FromBytes("""
        {
            "type": "object",
            "properties": {
                "classification": {
                    "type": "string",
                    "enum": ["A", "B", "C"],
                    "description": "Klasyfikacja problemu"
                },
                "justification": {
                    "type": "string",
                    "description": "Uzasadnienie klasyfikacji"
                },
                "problem": {
                    "type": "string",
                    "description": "Podsumowanie problemu"
                },
                "users": {
                    "type": "string",
                    "description": "Kim są użytkownicy rozwiązania"
                },
                "currentProcess": {
                    "type": "string",
                    "description": "Opis obecnego procesu"
                },
                "timeWaste": {
                    "type": "string",
                    "description": "Gdzie tracony jest czas"
                },
                "inputData": {
                    "type": "string",
                    "description": "Wymagane dane wejściowe"
                },
                "expectedOutput": {
                    "type": "string",
                    "description": "Oczekiwany wynik"
                },
                "recommendedPath": {
                    "type": "string",
                    "description": "Rekomendowana ścieżka rozwiązania"
                },
                "mvpScope": {
                    "type": "string",
                    "description": "Zakres MVP"
                },
                "outOfScope": {
                    "type": "string",
                    "description": "Co jest poza zakresem"
                },
                "acceptanceCriteria": {
                    "type": "string",
                    "description": "Kryteria akceptacji"
                },
                "nextStep": {
                    "type": "string",
                    "description": "Następny krok do podjęcia"
                }
            },
            "required": ["classification", "justification", "problem", "users", "currentProcess", "timeWaste", "inputData", "expectedOutput", "recommendedPath", "mvpScope", "outOfScope", "acceptanceCriteria", "nextStep"],
            "additionalProperties": false
        }
        """u8.ToArray());

    public ClassificationService(IOptions<AzureOpenAIOptions> options, ILogger<ClassificationService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ClassificationResult> ClassifyAsync(WizardData data, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Endpoint) || string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("Azure OpenAI nie jest skonfigurowane. Ustaw Endpoint i ApiKey w konfiguracji.");
        }

        var userMessage = $"""
            ## Problem
            {data.Problem}

            ## Obecny proces
            {data.Process}

            ## Strata czasu
            {data.TimeWaste}

            ## Dane wejściowe
            {data.InputData}

            ## Oczekiwany wynik
            {data.Output}

            ## Ryzyka
            {data.Risks}

            ## Użytkownicy
            {data.Users}

            ## Skala
            {data.Scale}
            """;

        var azureClient = new AzureOpenAIClient(
            new Uri(_options.Endpoint),
            new ApiKeyCredential(_options.ApiKey));

        var client = azureClient.GetChatClient(_options.DeploymentName);

        var chatOptions = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "classification_result",
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

        var json = completion.Value.Content[0].Text;
        _logger.LogDebug("Classification response: {Json}", json);

        var result = ParseResponse(json);
        return result;
    }

    private static ClassificationResult ParseResponse(string json)
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
}
