namespace Ai.Domain.Interfaces;

public record PronunciationEvaluationResult(
    string Transcript,
    int MatchScore,
    string Feedback,
    bool IsAcceptable
);

public interface IPronunciationEvaluator
{
    Task<PronunciationEvaluationResult> EvaluateAsync(
        string expectedText,
        byte[] audioContent,
        string mimeType,
        CancellationToken cancellationToken = default);
}
