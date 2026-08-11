# syntax=docker/dockerfile:1.7

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src/Maliev.SearchService

COPY ["nuget.config", "."]
COPY ["Directory.Build.props", "."]
COPY ["Maliev.SearchService.Api/Maliev.SearchService.Api.csproj", "Maliev.SearchService.Api/"]
COPY ["Maliev.SearchService.Application/Maliev.SearchService.Application.csproj", "Maliev.SearchService.Application/"]
COPY ["Maliev.SearchService.Domain/Maliev.SearchService.Domain.csproj", "Maliev.SearchService.Domain/"]
COPY ["Maliev.SearchService.Infrastructure/Maliev.SearchService.Infrastructure.csproj", "Maliev.SearchService.Infrastructure/"]
COPY [".ci-shared/Maliev.Aspire/Maliev.Aspire.ServiceDefaults/Maliev.Aspire.ServiceDefaults.csproj", "/src/Maliev.Aspire/Maliev.Aspire.ServiceDefaults/"]
COPY [".ci-shared/Maliev.MessagingContracts/generated/csharp/Maliev.MessagingContracts.csproj", "/src/Maliev.MessagingContracts/generated/csharp/"]

RUN dotnet restore "Maliev.SearchService.Api/Maliev.SearchService.Api.csproj" \
      /p:GITHUB_ACTIONS=false

COPY . .
COPY [".ci-shared/Maliev.Aspire", "/src/Maliev.Aspire"]
COPY [".ci-shared/Maliev.MessagingContracts", "/src/Maliev.MessagingContracts"]
WORKDIR /src/Maliev.SearchService/Maliev.SearchService.Api
RUN dotnet publish "Maliev.SearchService.Api.csproj" \
    --configuration "$BUILD_CONFIGURATION" \
    --output /app/publish \
    --no-restore \
    /p:GITHUB_ACTIONS=false \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --chown=app:app --from=build /app/publish .

USER app
EXPOSE 8080
EXPOSE 8081
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0

ENTRYPOINT ["dotnet", "Maliev.SearchService.Api.dll"]
