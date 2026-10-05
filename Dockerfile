FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /app

COPY nuget.config Directory.Build.props ./
COPY src/ src/

RUN dotnet restore src/Ai.Api/Ai.Api.csproj
RUN dotnet publish src/Ai.Api/Ai.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5004
EXPOSE 5004

ENTRYPOINT ["dotnet", "Ai.Api.dll"]
