# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY KartAnalyticsService.sln nuget.config ./
COPY packages/ packages/
COPY src/Api/Kart.Analytics.Api.csproj src/Api/
COPY src/Application/Kart.Analytics.Application.csproj src/Application/
COPY src/Domain/Kart.Analytics.Domain.csproj src/Domain/
COPY src/Infrastructure/Kart.Analytics.Infrastructure.csproj src/Infrastructure/
COPY tests/UnitTests/Kart.Analytics.UnitTests.csproj tests/UnitTests/
COPY tests/IntegrationTests/Kart.Analytics.IntegrationTests.csproj tests/IntegrationTests/
COPY tests/ContractTests/Kart.Analytics.ContractTests.csproj tests/ContractTests/
# The cache mount persists extracted NuGet packages under a stable id shared by every other
# kart-*-service Dockerfile, so restore stays fast (no re-download) even on a cache-miss here.
RUN --mount=type=cache,target=/root/.nuget/packages,id=nuget-packages \
    dotnet restore src/Api/Kart.Analytics.Api.csproj

# Scoped to what dotnet publish actually needs -- src/ (source) and contracts/
# (message-bus-manifest.json is a <Content> item Kart.Analytics.Api.csproj copies into the
# publish output).
COPY src/ src/
COPY contracts/ contracts/
RUN --mount=type=cache,target=/root/.nuget/packages,id=nuget-packages \
    dotnet publish src/Api/Kart.Analytics.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Kart.Analytics.Api.dll"]
