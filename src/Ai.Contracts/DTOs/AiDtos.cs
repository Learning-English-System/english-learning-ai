namespace Ai.Contracts.DTOs;

public record AnalyzeVocabularyRequestDto(
    string Word,
    string TargetLanguage = "vi",
    string? ContextSentence = null
);

public record ExampleSentenceDto(
    string Sentence,
    string Translation
);

public record VocabularyAnalysisDto(
    string NormalizedWord,
    string Translation,
    string PartOfSpeech,
    string Definition,
    string CefrLevel,
    IReadOnlyList<string> SuggestedCategories,
    IReadOnlyList<ExampleSentenceDto> Examples,
    IReadOnlyList<string> Collocations,
    string? Phonetic = null
);

public record GenerateDistractorsRequestDto(
    string Word,
    string CorrectMeaning,
    string TargetLanguage = "vi",
    int Count = 3
);

public record DistractorResultDto(
    IReadOnlyList<string> Distractors,
    string? Explanation = null
);

public record AssessPronunciationRequestDto(
    string ExpectedText,
    string AudioBase64,
    string AudioMimeType = "audio/wav"
);

public record PronunciationAssessmentDto(
    string Transcript,
    int MatchScore,
    string Feedback,
    bool IsAcceptable
);
