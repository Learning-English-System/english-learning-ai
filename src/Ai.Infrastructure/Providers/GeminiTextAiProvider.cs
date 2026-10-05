using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Ai.Domain.Interfaces;
using Ai.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ai.Infrastructure.Providers;

public class GeminiTextAiProvider : ITextAiProvider
{
    private readonly HttpClient _httpClient;
    private readonly AiOptions _options;
    private readonly ILogger<GeminiTextAiProvider> _logger;

    public GeminiTextAiProvider(
        HttpClient httpClient,
        IOptions<AiOptions> options,
        ILogger<GeminiTextAiProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> GenerateTextAsync(
        string prompt,
        TextGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.GeminiApiKey))
        {
            throw new InvalidOperationException("Gemini API key is not configured.");
        }

        var opt = options ?? new TextGenerationOptions();
        var model = _options.GeminiModel;
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={_options.GeminiApiKey}";

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            },
            generationConfig = new
            {
                temperature = opt.Temperature,
                maxOutputTokens = opt.MaxOutputTokens,
                responseMimeType = opt.ResponseMimeType
            }
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(url, requestBody, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken);
            var text = json?["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.GetValue<string>();

            if (string.IsNullOrWhiteSpace(text))
            {
                throw new InvalidOperationException("Gemini API returned empty text response.");
            }

            return text;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemini API generation failed for model {Model}", model);
            throw;
        }
    }
}
