using Ai.Application.Interfaces;
using Ai.Application.Services;
using Ai.Contracts.DTOs;
using Ai.Domain.Interfaces;
using Ai.Infrastructure.Providers;
using Ai.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ai.UnitTests;

public class AiServiceUnitTests
{
    private readonly FakeTextAiProvider _fakeAiProvider = new();
    private readonly AcousticPronunciationEvaluator _pronunciationEvaluator = new();

    [Fact]
    public async Task AnalyzeVocabulary_ParsesAndValidates_StructureCorrectly()
    {
        var service = new AiService(
            _fakeAiProvider,
            _pronunciationEvaluator,
            NullLogger<AiService>.Instance);

        var request = new AnalyzeVocabularyRequestDto("Resilient", "vi", "The economy is resilient.");
        var result = await service.AnalyzeVocabularyAsync(request);

        result.Should().NotBeNull();
        result.NormalizedWord.Should().Be("resilient");
        result.Translation.Should().NotBeNullOrWhiteSpace();
        result.PartOfSpeech.Should().Be("Adjective");
        result.Definition.Should().NotBeNullOrWhiteSpace();
        result.CefrLevel.Should().Be("B2");
        result.Examples.Should().NotBeEmpty();
        result.Collocations.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GenerateDistractors_ProducesValidOptions()
    {
        var service = new AiService(
            _fakeAiProvider,
            _pronunciationEvaluator,
            NullLogger<AiService>.Instance);

        var request = new GenerateDistractorsRequestDto("Ubiquitous", "Có mặt ở khắp nơi", "vi", 3);
        var result = await service.GenerateDistractorsAsync(request);

        result.Should().NotBeNull();
        result.Distractors.Should().HaveCount(3);
        result.Explanation.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task PronunciationEvaluator_EmptyAudio_ReturnsZeroScore()
    {
        var result = await _pronunciationEvaluator.EvaluateAsync(
            "Hello world",
            Array.Empty<byte>(),
            "audio/wav");

        result.MatchScore.Should().Be(0);
        result.IsAcceptable.Should().BeFalse();
        result.Feedback.Should().Contain("No audio data was received");
    }

    [Fact]
    public async Task PronunciationEvaluator_UnsupportedMime_ReturnsError()
    {
        byte[] dummyAudio = new byte[500];
        Array.Fill(dummyAudio, (byte)150);

        var result = await _pronunciationEvaluator.EvaluateAsync(
            "Hello world",
            dummyAudio,
            "video/mp4");

        result.MatchScore.Should().Be(0);
        result.IsAcceptable.Should().BeFalse();
        result.Feedback.Should().Contain("Unsupported audio format");
    }

    [Fact]
    public async Task PronunciationEvaluator_OversizedAudio_ReturnsError()
    {
        // Allocate 11 MB dummy buffer (exceeding 10MB limit)
        byte[] oversizedAudio = new byte[11 * 1024 * 1024];

        var result = await _pronunciationEvaluator.EvaluateAsync(
            "Hello world",
            oversizedAudio,
            "audio/wav");

        result.MatchScore.Should().Be(0);
        result.IsAcceptable.Should().BeFalse();
        result.Feedback.Should().Contain("exceeds maximum allowed size");
    }

    [Fact]
    public async Task PronunciationEvaluator_ValidEnergeticAudio_ReturnsAcceptable()
    {
        byte[] energeticAudio = new byte[1000];
        for (int i = 0; i < energeticAudio.Length; i++)
        {
            energeticAudio[i] = (byte)(i % 2 == 0 ? 50 : 200); // High dynamic range / energy
        }

        var result = await _pronunciationEvaluator.EvaluateAsync(
            "Hello world",
            energeticAudio,
            "audio/wav");

        result.MatchScore.Should().BeGreaterThan(70);
        result.IsAcceptable.Should().BeTrue();
        result.Feedback.Should().Contain("Excellent pronunciation");
    }

    [Fact]
    public async Task AiService_UsesCache_WhenAvailable()
    {
        var mockCache = new TestInMemoryCacheService();
        var countingProvider = new CountingAiProvider();

        var service = new AiService(
            countingProvider,
            _pronunciationEvaluator,
            NullLogger<AiService>.Instance,
            mockCache);

        var req = new AnalyzeVocabularyRequestDto("Serendipity");

        // 1. First call -> invokes provider and caches
        var res1 = await service.AnalyzeVocabularyAsync(req);
        countingProvider.CallCount.Should().Be(1);

        // 2. Second call -> served from cache, provider not invoked
        var res2 = await service.AnalyzeVocabularyAsync(req);
        countingProvider.CallCount.Should().Be(1);
        res2.NormalizedWord.Should().Be(res1.NormalizedWord);
    }
}

public class TestInMemoryCacheService : IAiCacheService
{
    private readonly Dictionary<string, string> _store = new();

    public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(key, out var val);
        return Task.FromResult(val);
    }

    public Task SetAsync(string key, string value, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        _store[key] = value;
        return Task.CompletedTask;
    }
}

public class CountingAiProvider : ITextAiProvider
{
    public int CallCount { get; private set; }
    private readonly FakeTextAiProvider _inner = new();

    public Task<string> GenerateTextAsync(string prompt, TextGenerationOptions? options = null, CancellationToken cancellationToken = default)
    {
        CallCount++;
        return _inner.GenerateTextAsync(prompt, options, cancellationToken);
    }
}
