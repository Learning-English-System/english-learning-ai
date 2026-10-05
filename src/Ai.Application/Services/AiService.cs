using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Ai.Application.Interfaces;
using Ai.Contracts.DTOs;
using Ai.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Ai.Application.Services;

public class AiService : IAiService
{
    private readonly ITextAiProvider _aiProvider;
    private readonly IPronunciationEvaluator _pronunciationEvaluator;
    private readonly IAiCacheService? _cacheService;
    private readonly ILogger<AiService> _logger;

    public const string PromptVersion = "v1";
    public const string DefaultModel = "gemini-2.5-flash";

    public AiService(
        ITextAiProvider aiProvider,
        IPronunciationEvaluator pronunciationEvaluator,
        ILogger<AiService> logger,
        IAiCacheService? cacheService = null)
    {
        _aiProvider = aiProvider;
        _pronunciationEvaluator = pronunciationEvaluator;
        _logger = logger;
        _cacheService = cacheService;
    }

    public async Task<VocabularyAnalysisDto> AnalyzeVocabularyAsync(
        AnalyzeVocabularyRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Word))
        {
            throw new ArgumentException("Word cannot be empty.", nameof(request));
        }

        string normalizedWord = request.Word.Trim().ToLowerInvariant();
        string targetLang = string.IsNullOrWhiteSpace(request.TargetLanguage) ? "vi" : request.TargetLanguage.Trim().ToLowerInvariant();
        string context = request.ContextSentence?.Trim() ?? string.Empty;

        string cacheKey = ComputeCacheKey("analysis", $"{normalizedWord}:{targetLang}:{context}");

        // 1. Try Redis cache
        var cachedJson = await GetFromCacheAsync(cacheKey);
        if (!string.IsNullOrEmpty(cachedJson))
        {
            try
            {
                var cachedResult = JsonSerializer.Deserialize<VocabularyAnalysisDto>(cachedJson);
                if (cachedResult != null)
                {
                    _logger.LogInformation("Cache hit for vocabulary analysis: {Word}", normalizedWord);
                    return cachedResult;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize cached analysis for {Word}", normalizedWord);
            }
        }

        // 2. Build prompt
        var prompt = $$"""
        Analyze the English vocabulary word '{{request.Word.Trim()}}' for a language learner.
        Target translation language: '{{targetLang}}'.
        {{(string.IsNullOrWhiteSpace(context) ? "" : $"Context sentence: '{context}'.")}}

        Return a single JSON object with EXACTLY this structure:
        {
          "normalized_word": "normalized lowercase word",
          "translation": "natural and accurate translation in {{targetLang}}",
          "part_of_speech": "Noun, Verb, Adjective, etc.",
          "definition": "Clear concise English definition",
          "cefr_level": "A1, A2, B1, B2, C1, or C2",
          "suggested_categories": ["Category 1", "Category 2"],
          "examples": [
            {
              "sentence": "English sentence containing the word",
              "translation": "Translation in {{targetLang}}"
            }
          ],
          "collocations": ["collocation 1", "collocation 2"],
          "phonetic": "/IPA transcription/"
        }
        Do not enclose in markdown block quotes. Output pure JSON.
        """;

        // 3. Call AI provider
        var rawResponse = await _aiProvider.GenerateTextAsync(prompt, new TextGenerationOptions
        {
            Temperature = 0.2,
            ResponseMimeType = "application/json"
        }, cancellationToken);

        // 4. Parse & Validate
        var result = ParseVocabularyAnalysis(rawResponse, request.Word);

        // 5. Store in Redis cache (TTL: 7 days)
        await SetInCacheAsync(cacheKey, JsonSerializer.Serialize(result), TimeSpan.FromDays(7));

        return result;
    }

    public async Task<DistractorResultDto> GenerateDistractorsAsync(
        GenerateDistractorsRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Word) || string.IsNullOrWhiteSpace(request.CorrectMeaning))
        {
            throw new ArgumentException("Word and CorrectMeaning cannot be empty.");
        }

        string word = request.Word.Trim();
        string correct = request.CorrectMeaning.Trim();
        string targetLang = string.IsNullOrWhiteSpace(request.TargetLanguage) ? "vi" : request.TargetLanguage.Trim().ToLowerInvariant();
        int count = Math.Clamp(request.Count, 2, 6);

        string cacheKey = ComputeCacheKey("distractors", $"{word.ToLowerInvariant()}:{correct}:{targetLang}:{count}");

        // 1. Check Redis cache
        var cachedJson = await GetFromCacheAsync(cacheKey);
        if (!string.IsNullOrEmpty(cachedJson))
        {
            try
            {
                var cachedResult = JsonSerializer.Deserialize<DistractorResultDto>(cachedJson);
                if (cachedResult != null)
                {
                    return cachedResult;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize cached distractors for {Word}", word);
            }
        }

        // 2. Build prompt
        var prompt = $$"""
        Generate {{count}} plausible but distinctly incorrect multiple-choice distractor options for the English word '{{word}}'.
        The correct meaning/translation is: '{{correct}}'.
        Target language: '{{targetLang}}'.

        Requirements:
        1. Distractors must be plausible, believable choices in {{targetLang}} that learners might confuse, but definitively incorrect for '{{word}}'.
        2. Do NOT duplicate the correct meaning.

        Return a single JSON object with EXACTLY this structure:
        {
          "distractors": ["distractor 1", "distractor 2", "distractor 3"],
          "explanation": "Brief rationale for why these distractors are challenging and educational"
        }
        """;

        // 3. Call AI provider
        var rawResponse = await _aiProvider.GenerateTextAsync(prompt, new TextGenerationOptions
        {
            Temperature = 0.3,
            ResponseMimeType = "application/json"
        }, cancellationToken);

        // 4. Parse & Validate
        var result = ParseDistractors(rawResponse, count);

        // 5. Cache (TTL: 7 days)
        await SetInCacheAsync(cacheKey, JsonSerializer.Serialize(result), TimeSpan.FromDays(7));

        return result;
    }

    public async Task<PronunciationAssessmentDto> AssessPronunciationAsync(
        string expectedText,
        byte[] audioBytes,
        string mimeType,
        CancellationToken cancellationToken = default)
    {
        var evalResult = await _pronunciationEvaluator.EvaluateAsync(
            expectedText,
            audioBytes,
            mimeType,
            cancellationToken);

        return new PronunciationAssessmentDto(
            Transcript: evalResult.Transcript,
            MatchScore: evalResult.MatchScore,
            Feedback: evalResult.Feedback,
            IsAcceptable: evalResult.IsAcceptable
        );
    }

    private static VocabularyAnalysisDto ParseVocabularyAnalysis(string rawJson, string fallbackWord)
    {
        var cleanJson = CleanJsonString(rawJson);
        var doc = JsonNode.Parse(cleanJson);
        if (doc == null)
        {
            throw new FormatException("Failed to parse JSON response from AI provider.");
        }

        string norm = doc["normalized_word"]?.GetValue<string>() ?? fallbackWord.Trim().ToLowerInvariant();
        string trans = doc["translation"]?.GetValue<string>() ?? string.Empty;
        string pos = doc["part_of_speech"]?.GetValue<string>() ?? "Noun";
        string def = doc["definition"]?.GetValue<string>() ?? string.Empty;
        string cefr = doc["cefr_level"]?.GetValue<string>() ?? "B1";
        string? phonetic = doc["phonetic"]?.GetValue<string>();

        var categories = new List<string>();
        if (doc["suggested_categories"] is JsonArray catArr)
        {
            foreach (var item in catArr)
            {
                if (item != null) categories.Add(item.GetValue<string>());
            }
        }
        if (categories.Count == 0) categories.Add("General");

        var examples = new List<ExampleSentenceDto>();
        if (doc["examples"] is JsonArray egArr)
        {
            foreach (var eg in egArr)
            {
                if (eg is JsonObject egObj)
                {
                    string s = egObj["sentence"]?.GetValue<string>() ?? string.Empty;
                    string t = egObj["translation"]?.GetValue<string>() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(s))
                    {
                        examples.Add(new ExampleSentenceDto(s, t));
                    }
                }
            }
        }

        var collocations = new List<string>();
        if (doc["collocations"] is JsonArray colArr)
        {
            foreach (var item in colArr)
            {
                if (item != null) collocations.Add(item.GetValue<string>());
            }
        }

        return new VocabularyAnalysisDto(
            NormalizedWord: norm,
            Translation: trans,
            PartOfSpeech: pos,
            Definition: def,
            CefrLevel: cefr.ToUpperInvariant(),
            SuggestedCategories: categories,
            Examples: examples,
            Collocations: collocations,
            Phonetic: phonetic
        );
    }

    private static DistractorResultDto ParseDistractors(string rawJson, int expectedCount)
    {
        var cleanJson = CleanJsonString(rawJson);
        var doc = JsonNode.Parse(cleanJson);
        if (doc == null)
        {
            throw new FormatException("Failed to parse distractors JSON response.");
        }

        var list = new List<string>();
        if (doc["distractors"] is JsonArray arr)
        {
            foreach (var item in arr)
            {
                if (item != null) list.Add(item.GetValue<string>());
            }
        }

        string? explanation = doc["explanation"]?.GetValue<string>();

        return new DistractorResultDto(list, explanation);
    }

    private static string CleanJsonString(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[7..];
        }
        else if (trimmed.StartsWith("```", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[3..];
        }

        if (trimmed.EndsWith("```", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^3];
        }

        return trimmed.Trim();
    }

    private static string ComputeCacheKey(string prefix, string input)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        var hashHex = Convert.ToHexString(hashBytes)[..16].ToLowerInvariant();
        return $"ai:{prefix}:{DefaultModel}:{PromptVersion}:{hashHex}";
    }

    private async Task<string?> GetFromCacheAsync(string key)
    {
        if (_cacheService == null) return null;
        try
        {
            return await _cacheService.GetAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache read failed for key {Key}", key);
            return null;
        }
    }

    private async Task SetInCacheAsync(string key, string value, TimeSpan expiry)
    {
        if (_cacheService == null) return;
        try
        {
            await _cacheService.SetAsync(key, value, expiry);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache write failed for key {Key}", key);
        }
    }
}
