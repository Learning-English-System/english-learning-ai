using Ai.Application.Interfaces;
using Ai.Contracts.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ai.Api.Controllers;

[ApiController]
[Route("api/ai")]
[Authorize]
public class AiController : ControllerBase
{
    private readonly IAiService _aiService;

    public AiController(IAiService aiService)
    {
        _aiService = aiService;
    }

    [HttpPost("vocabulary/analyze")]
    public async Task<IActionResult> AnalyzeVocabulary(
        [FromBody] AnalyzeVocabularyRequestDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Word))
        {
            return BadRequest(new { error = "Word is required." });
        }

        var result = await _aiService.AnalyzeVocabularyAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("vocabulary/distractors")]
    public async Task<IActionResult> GenerateDistractors(
        [FromBody] GenerateDistractorsRequestDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Word) || string.IsNullOrWhiteSpace(request.CorrectMeaning))
        {
            return BadRequest(new { error = "Word and correct meaning are required." });
        }

        var result = await _aiService.GenerateDistractorsAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("pronunciation/assess")]
    public async Task<IActionResult> AssessPronunciation(
        [FromBody] AssessPronunciationRequestDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ExpectedText))
        {
            return BadRequest(new { error = "ExpectedText is required." });
        }

        byte[] audioBytes = Array.Empty<byte>();
        if (!string.IsNullOrWhiteSpace(request.AudioBase64))
        {
            try
            {
                audioBytes = Convert.FromBase64String(request.AudioBase64);
            }
            catch (FormatException)
            {
                return BadRequest(new { error = "AudioBase64 is not valid Base64." });
            }
        }

        var result = await _aiService.AssessPronunciationAsync(
            request.ExpectedText,
            audioBytes,
            request.AudioMimeType,
            cancellationToken);

        return Ok(result);
    }
}
