using Ai.Application.Interfaces;
using Ai.Contracts.DTOs;
using Ai.Contracts.Grpc;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;

namespace Ai.Infrastructure.Grpc;

[Authorize(Policy = "InternalAiInvoke")]
public class AiInternalGrpcService : AiInternalService.AiInternalServiceBase
{
    private readonly IAiService _aiService;

    public AiInternalGrpcService(IAiService aiService)
    {
        _aiService = aiService;
    }

    public override async Task<AnalyzeVocabularyResponse> AnalyzeVocabulary(
        AnalyzeVocabularyRequest request,
        ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.Word))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Word parameter is required."));
        }

        var analysis = await _aiService.AnalyzeVocabularyAsync(new AnalyzeVocabularyRequestDto(
            Word: request.Word,
            TargetLanguage: string.IsNullOrWhiteSpace(request.TargetLanguage) ? "vi" : request.TargetLanguage,
            ContextSentence: request.ContextSentence
        ), context.CancellationToken);

        var response = new AnalyzeVocabularyResponse
        {
            NormalizedWord = analysis.NormalizedWord,
            Translation = analysis.Translation,
            PartOfSpeech = analysis.PartOfSpeech,
            Definition = analysis.Definition,
            CefrLevel = analysis.CefrLevel
        };

        foreach (var cat in analysis.SuggestedCategories)
        {
            response.SuggestedCategories.Add(cat);
        }

        foreach (var eg in analysis.Examples)
        {
            response.Examples.Add(new ExampleSentenceItem
            {
                Sentence = eg.Sentence,
                Translation = eg.Translation
            });
        }

        return response;
    }

    public override async Task<GenerateDistractorsResponse> GenerateDistractors(
        GenerateDistractorsRequest request,
        ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.Word) || string.IsNullOrWhiteSpace(request.CorrectMeaning))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Word and correct_meaning are required."));
        }

        var result = await _aiService.GenerateDistractorsAsync(new GenerateDistractorsRequestDto(
            Word: request.Word,
            CorrectMeaning: request.CorrectMeaning,
            TargetLanguage: string.IsNullOrWhiteSpace(request.TargetLanguage) ? "vi" : request.TargetLanguage,
            Count: request.Count > 0 ? request.Count : 3
        ), context.CancellationToken);

        var response = new GenerateDistractorsResponse();
        foreach (var d in result.Distractors)
        {
            response.Distractors.Add(d);
        }

        return response;
    }

    public override async Task<CheckPronunciationResponse> CheckPronunciation(
        CheckPronunciationRequest request,
        ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.ExpectedText))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "expected_text is required."));
        }

        byte[] audioBytes = request.AudioContent.ToByteArray();
        string mime = string.IsNullOrWhiteSpace(request.AudioMimeType) ? "audio/wav" : request.AudioMimeType;

        var assessment = await _aiService.AssessPronunciationAsync(
            request.ExpectedText,
            audioBytes,
            mime,
            context.CancellationToken);

        return new CheckPronunciationResponse
        {
            Transcript = assessment.Transcript,
            MatchScore = assessment.MatchScore,
            Feedback = assessment.Feedback,
            IsAcceptable = assessment.IsAcceptable
        };
    }
}
