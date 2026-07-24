namespace DevNote.Services;

public class AzureOpenAIOptions
{
    public const string SectionName = "AzureOpenAI";

    /// <summary>
    /// Which API provider to use. Accepted values: "Azure" (default) or "OpenAI".
    /// </summary>
    public string Provider { get; set; } = "Azure";

    /// <summary>Required when Provider = "Azure". The Azure OpenAI resource endpoint URL.</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Deployment name (Azure) or model name (OpenAI), e.g. "gpt-4o-mini".
    /// </summary>
    public string DeploymentName { get; set; } = "gpt-4o-mini";

    public string ApiKey { get; set; } = string.Empty;

    public bool IsOpenAI => string.Equals(Provider, "OpenAI", StringComparison.OrdinalIgnoreCase);
}
