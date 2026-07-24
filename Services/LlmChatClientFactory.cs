using System.ClientModel;
using Azure.AI.OpenAI;
using OpenAI;
using OpenAI.Chat;

namespace DevNote.Services;

/// <summary>
/// Creates a <see cref="ChatClient"/> for either Azure OpenAI or OpenAI based on
/// <see cref="AzureOpenAIOptions.Provider"/>.
/// </summary>
public static class LlmChatClientFactory
{
    public static ChatClient Create(AzureOpenAIOptions options)
    {
        if (options.IsOpenAI)
        {
            var client = new OpenAIClient(new ApiKeyCredential(options.ApiKey));
            return client.GetChatClient(options.DeploymentName);
        }

        var azureClient = new AzureOpenAIClient(
            new Uri(options.Endpoint),
            new ApiKeyCredential(options.ApiKey));
        return azureClient.GetChatClient(options.DeploymentName);
    }

    /// <summary>
    /// Validates that the minimum required config is present for the chosen provider.
    /// Throws <see cref="InvalidOperationException"/> with a descriptive message if not.
    /// </summary>
    public static void ValidateConfiguration(AzureOpenAIOptions options, string callerName)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
            throw new InvalidOperationException(
                $"{callerName}: ApiKey is required. Set AzureOpenAI__ApiKey in configuration.");

        if (!options.IsOpenAI && string.IsNullOrWhiteSpace(options.Endpoint))
            throw new InvalidOperationException(
                $"{callerName}: Endpoint is required when Provider is \"Azure\". " +
                "Set AzureOpenAI__Endpoint, or switch to Provider=\"OpenAI\".");
    }
}
