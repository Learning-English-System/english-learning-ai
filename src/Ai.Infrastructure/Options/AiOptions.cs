namespace Ai.Infrastructure.Options;

public class AiOptions
{
    public string Provider { get; set; } = "fake"; // "gemini" or "fake"
    public string? GeminiApiKey { get; set; }
    public string GeminiModel { get; set; } = "gemini-2.5-flash";
    public int CacheDurationDays { get; set; } = 7;
    public string PromptVersion { get; set; } = "v1";
}
