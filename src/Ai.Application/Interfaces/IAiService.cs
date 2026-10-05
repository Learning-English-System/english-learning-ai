using Ai.Contracts.DTOs;

namespace Ai.Application.Interfaces;

public interface IAiService
{
    Task<VocabularyAnalysisDto> AnalyzeVocabularyAsync(
        AnalyzeVocabularyRequestDto request,
        CancellationToken cancellationToken = default);

    Task<DistractorResultDto> GenerateDistractorsAsync(
        GenerateDistractorsRequestDto request,
        CancellationToken cancellationToken = default);

    Task<PronunciationAssessmentDto> AssessPronunciationAsync(
        string expectedText,
        byte[] audioBytes,
        string mimeType,
        CancellationToken cancellationToken = default);
}
