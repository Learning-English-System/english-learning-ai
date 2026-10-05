FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /app

COPY Directory.Build.props ./
COPY src/BuildingBlocks/ src/BuildingBlocks/
COPY src/Ai/ src/Ai/

RUN dotnet restore src/Ai/Ai.Api/Ai.Api.csproj
RUN dotnet publish src/Ai/Ai.Api/Ai.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5004
EXPOSE 5004

ENTRYPOINT ["dotnet", "Ai.Api.dll"]
