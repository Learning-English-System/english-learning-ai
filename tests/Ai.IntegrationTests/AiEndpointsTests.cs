using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Ai.Contracts.DTOs;
using Ai.Contracts.Grpc;
using BuildingBlocks.Security.Constants;
using FluentAssertions;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ai.IntegrationTests;

public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string AuthenticationScheme = "TestScheme";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? authHeader = Request.Headers.Authorization;
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        string tokenValue = authHeader["Bearer ".Length..].Trim();

        if (tokenValue.Equals("internal-service-token", StringComparison.OrdinalIgnoreCase))
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "internal-test-service"),
                new Claim("sub", "internal-test-service"),
                new Claim("scope", $"{SecurityConstants.Scopes.InternalAiInvoke} {SecurityConstants.Scopes.InternalLearningRead}")
            };
            var identity = new ClaimsIdentity(claims, AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, AuthenticationScheme)));
        }

        if (Guid.TryParse(tokenValue, out var guid))
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, guid.ToString()),
                new Claim("sub", guid.ToString()),
                new Claim(ClaimTypes.Role, "Learner"),
                new Claim("scope", "openid profile email")
            };
            var identity = new ClaimsIdentity(claims, AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, AuthenticationScheme)));
        }

        return Task.FromResult(AuthenticateResult.Fail("Invalid test bearer token"));
    }
}

public class AiTestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.AuthenticationScheme;
                options.DefaultChallengeScheme = TestAuthHandler.AuthenticationScheme;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.AuthenticationScheme, null);
        });
    }
}

public class AiEndpointsTests : IClassFixture<AiTestWebApplicationFactory>
{
    private readonly AiTestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AiEndpointsTests(AiTestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Endpoints_Unauthenticated_ReturnUnauthorized()
    {
        var res1 = await _client.PostAsJsonAsync("/api/ai/vocabulary/analyze", new AnalyzeVocabularyRequestDto("eloquent"));
        res1.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var res2 = await _client.PostAsJsonAsync("/api/ai/vocabulary/distractors", new GenerateDistractorsRequestDto("eloquent", "hùng hồn"));
        res2.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var res3 = await _client.PostAsJsonAsync("/api/ai/pronunciation/assess", new AssessPronunciationRequestDto("eloquent", ""));
        res3.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AnalyzeVocabulary_AuthenticatedLearner_ReturnsAnalysis()
    {
        var userId = Guid.NewGuid();
        using var authedClient = _factory.CreateClient();
        authedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userId.ToString());

        var req = new AnalyzeVocabularyRequestDto("Resilient", "vi", "A resilient economic structure.");
        var response = await authedClient.PostAsJsonAsync("/api/ai/vocabulary/analyze", req);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<VocabularyAnalysisDto>();
        result.Should().NotBeNull();
        result!.NormalizedWord.Should().Be("resilient");
        result.Translation.Should().NotBeNullOrWhiteSpace();
        result.CefrLevel.Should().NotBeNullOrWhiteSpace();
        result.Examples.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GenerateDistractors_AuthenticatedLearner_ReturnsDistractors()
    {
        var userId = Guid.NewGuid();
        using var authedClient = _factory.CreateClient();
        authedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userId.ToString());

        var req = new GenerateDistractorsRequestDto("Ubiquitous", "Có mặt ở khắp nơi", "vi", 3);
        var response = await authedClient.PostAsJsonAsync("/api/ai/vocabulary/distractors", req);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<DistractorResultDto>();
        result.Should().NotBeNull();
        result!.Distractors.Should().HaveCount(3);
    }

    [Fact]
    public async Task AssessPronunciation_WithAudio_ReturnsEvaluation()
    {
        var userId = Guid.NewGuid();
        using var authedClient = _factory.CreateClient();
        authedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userId.ToString());

        byte[] fakeAudio = new byte[800];
        for (int i = 0; i < fakeAudio.Length; i++) fakeAudio[i] = (byte)(i % 2 == 0 ? 40 : 220);
        string audioB64 = Convert.ToBase64String(fakeAudio);

        var req = new AssessPronunciationRequestDto("The resilient economy", audioB64, "audio/wav");
        var response = await authedClient.PostAsJsonAsync("/api/ai/pronunciation/assess", req);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PronunciationAssessmentDto>();
        result.Should().NotBeNull();
        result!.MatchScore.Should().BeGreaterThan(70);
        result.IsAcceptable.Should().BeTrue();
        result.Feedback.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task InternalGrpc_AiInternalService_WithServiceToken_Succeeds()
    {
        var channel = GrpcChannel.ForAddress(_client.BaseAddress!, new GrpcChannelOptions
        {
            HttpClient = _client
        });
        var grpcClient = new AiInternalService.AiInternalServiceClient(channel);

        // 1. Unauthenticated call fails
        var unauthedMeta = new Metadata();
        Func<Task> callUnauthed = async () =>
        {
            await grpcClient.AnalyzeVocabularyAsync(new AnalyzeVocabularyRequest
            {
                Word = "resilient",
                TargetLanguage = "vi"
            }, unauthedMeta);
        };
        await callUnauthed.Should().ThrowAsync<RpcException>();

        // 2. Authenticated with internal service token succeeds
        var authedMeta = new Metadata
        {
            { "Authorization", "Bearer internal-service-token" }
        };

        var grpcResponse = await grpcClient.AnalyzeVocabularyAsync(new AnalyzeVocabularyRequest
        {
            Word = "resilient",
            TargetLanguage = "vi"
        }, authedMeta);

        grpcResponse.Should().NotBeNull();
        grpcResponse.NormalizedWord.Should().Be("resilient");
        grpcResponse.Translation.Should().NotBeNullOrWhiteSpace();
        grpcResponse.Examples.Should().NotBeEmpty();

        // 3. Test GenerateDistractors RPC
        var distractorResponse = await grpcClient.GenerateDistractorsAsync(new GenerateDistractorsRequest
        {
            Word = "resilient",
            CorrectMeaning = "kiên cường",
            TargetLanguage = "vi",
            Count = 3
        }, authedMeta);

        distractorResponse.Should().NotBeNull();
        distractorResponse.Distractors.Should().HaveCount(3);
    }
}
