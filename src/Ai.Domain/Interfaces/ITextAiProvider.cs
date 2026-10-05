namespace Ai.Domain.Interfaces;

public class TextGenerationOptions
{
    public double Temperature { get; set; } = 0.2;
    public int MaxOutputTokens { get; set; } = 1024;
    public string? ResponseMimeType { get; set; } = "application/json";
}

public interface ITextAiProvider
{
    Task<string> GenerateTextAsync(string prompt, TextGenerationOptions? options = null, CancellationToken cancellationToken = default);
}
