namespace MeshcomWebDesk.Models;

public class AiSettings
{
    public const string SectionName = "Ai";

    public const string ProviderOpenAi      = "openai";
    public const string ProviderGrok        = "grok";
    public const string ProviderAzureOpenAi = "azure";

    /// <summary>When true, QSO summaries via AI API are enabled.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// AI provider to use. Supported values: "openai" (default), "grok", "azure".
    /// </summary>
    public string Provider { get; set; } = ProviderOpenAi;

    /// <summary>API key for the selected provider.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Model to use for summarization.
    /// OpenAI:        "gpt-4o-mini", "gpt-4o", "gpt-3.5-turbo".
    /// Grok:          "grok-3-mini", "grok-3".
    /// Azure OpenAI:  deployment name as configured in Azure Portal (e.g. "gpt-4o-mini").
    /// </summary>
    public string Model { get; set; } = "gpt-4o-mini";

    // ── Azure OpenAI specific ─────────────────────────────────────────────

    /// <summary>
    /// Azure OpenAI resource endpoint.
    /// Example: "https://my-resource.openai.azure.com"
    /// Only used when Provider = "azure".
    /// </summary>
    public string AzureEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// Azure OpenAI API version.
    /// Default: "2024-08-01-preview".
    /// Only used when Provider = "azure".
    /// </summary>
    public string AzureApiVersion { get; set; } = "2024-08-01-preview";

    // ── Common ────────────────────────────────────────────────────────────

    /// <summary>
    /// Minimum number of days since the last QSO before the summary icon (🤖) is shown.
    /// Set to 0 to always show the icon when any messages exist.
    /// Default is 7 days.
    /// </summary>
    public int ThresholdDays { get; set; } = 7;

    /// <summary>
    /// How many days back to look for messages when generating a summary.
    /// Only messages within this period are sent to the AI.
    /// Set to 0 to include all available messages (limited by MaxMessages).
    /// Default is 365 days.
    /// </summary>
    public int SummaryDays { get; set; } = 365;

    /// <summary>
    /// Maximum number of messages sent to the AI for summarization.
    /// Limits token usage. Default is 50.
    /// </summary>
    public int MaxMessages { get; set; } = 50;

    /// <summary>When true, every AI API request is logged at Information level.</summary>
    public bool LogRequests { get; set; } = false;

    /// <summary>
    /// Returns the full API URL for the selected provider.
    /// For Azure OpenAI the deployment name (Model) and API version are embedded in the URL.
    /// </summary>
    public string GetApiUrl() => Provider switch
    {
        ProviderGrok        => "https://api.x.ai/v1/chat/completions",
        ProviderAzureOpenAi => $"{AzureEndpoint.TrimEnd('/')}/openai/deployments/{Model}/chat/completions?api-version={AzureApiVersion}",
        _                   => "https://api.openai.com/v1/chat/completions"
    };
}
