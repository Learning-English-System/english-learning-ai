# English Learning - AI Service

Independent microservice repository managing Gemini text generation (grammar checks, explanations, hints, contextual examples) and pronunciation acoustic evaluation.

## Architecture & Responsibilities
- **Domain**: Text AI provider abstractions, acoustic pronunciation evaluator interfaces and calculation models.
- **Application**: AI service orchestrating Gemini text prompts and pronunciation score evaluations with Redis distributed caching.
- **Infrastructure**: Gemini SDK client (with fallback fake provider for offline testing), acoustic phoneme scoring, Redis caching layer, and internal gRPC server for low-latency inter-service calls.
- **Contracts**: Exports `EnglishLearning.Ai.Contracts` NuGet package.
- **Api**: REST endpoints on port 5004 and internal gRPC server.

## Getting Started

### Prerequisites
- .NET 10 SDK
- Redis Cache
- Gemini API Key (optional, defaults to deterministic offline provider if absent)

### Build, Test & Run
```bash
# Restore & build
dotnet restore AiService.slnx
dotnet build AiService.slnx

# Run tests
dotnet test AiService.slnx

# Pack shared contracts
dotnet pack src/Ai.Contracts/Ai.Contracts.csproj -c Release -o ../../packages

# Run API
dotnet run --project src/Ai.Api
```

### Docker
```bash
docker build -t english-learning-ai:latest .
docker run -p 5004:5004 english-learning-ai:latest
```
