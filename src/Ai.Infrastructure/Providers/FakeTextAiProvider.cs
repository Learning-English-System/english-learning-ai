using System.Text.Json;
using Ai.Domain.Interfaces;

namespace Ai.Infrastructure.Providers;

public class FakeTextAiProvider : ITextAiProvider
{
    public Task<string> GenerateTextAsync(
        string prompt,
        TextGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // If prompt is for distractors
        if (prompt.Contains("distractor", StringComparison.OrdinalIgnoreCase))
        {
            var distractorsResponse = new
            {
                distractors = new[]
                {
                    "Option A (plausible distractor)",
                    "Option B (similar category)",
                    "Option C (commonly confused)"
                },
                explanation = "Distractors chosen based on semantic proximity to test discriminative comprehension."
            };
            return Task.FromResult(JsonSerializer.Serialize(distractorsResponse));
        }

        // Default: Vocabulary analysis prompt
        var analysisResponse = new
        {
            normalized_word = "resilient",
            translation = "kiên cường, có khả năng phục hồi nhanh",
            part_of_speech = "Adjective",
            definition = "Able to withstand or recover quickly from difficult conditions.",
            cefr_level = "B2",
            suggested_categories = new[] { "Psychology", "General", "Academic" },
            examples = new[]
            {
                new
                {
                    sentence = "Babies are generally remarkably resilient.",
                    translation = "Trẻ nhỏ thường có khả năng phục hồi một cách đáng kinh ngạc."
                },
                new
                {
                    sentence = "He is resilient to emotional trauma.",
                    translation = "Anh ấy có sức đề kháng kiên cường trước chấn thương tâm lý."
                }
            },
            collocations = new[]
            {
                "highly resilient",
                "resilient economy",
                "resilient spirit"
            },
            phonetic = "/rɪˈzɪl.jənt/"
        };

        return Task.FromResult(JsonSerializer.Serialize(analysisResponse));
    }
}
