# syntax=docker/dockerfile:1.7

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
ARG SERVICE_DEFAULTS_VERSION=1.0.89-alpha
ARG MESSAGING_CONTRACTS_VERSION=1.0.94-alpha
WORKDIR /src

COPY ["nuget.config", "."]
COPY ["Directory.Build.props", "."]
COPY ["Maliev.SearchService.Api/Maliev.SearchService.Api.csproj", "Maliev.SearchService.Api/"]
COPY ["Maliev.SearchService.Application/Maliev.SearchService.Application.csproj", "Maliev.SearchService.Application/"]
COPY ["Maliev.SearchService.Domain/Maliev.SearchService.Domain.csproj", "Maliev.SearchService.Domain/"]
COPY ["Maliev.SearchService.Infrastructure/Maliev.SearchService.Infrastructure.csproj", "Maliev.SearchService.Infrastructure/"]

RUN --mount=type=secret,id=nuget_username,required=true \
    --mount=type=secret,id=nuget_password,required=true \
    NUGET_USERNAME="$(cat /run/secrets/nuget_username)" \
    NUGET_PASSWORD="$(cat /run/secrets/nuget_password)" \
    dotnet restore "Maliev.SearchService.Api/Maliev.SearchService.Api.csproj" \
      /p:GITHUB_ACTIONS=true \
      /p:ServiceDefaultsVersion="$SERVICE_DEFAULTS_VERSION" \
      /p:MessagingContractsVersion="$MESSAGING_CONTRACTS_VERSION"

COPY . .
WORKDIR /src/Maliev.SearchService.Api
RUN dotnet publish "Maliev.SearchService.Api.csproj" \
    --configuration "$BUILD_CONFIGURATION" \
    --output /app/publish \
    --no-restore \
    /p:GITHUB_ACTIONS=true \
    /p:ServiceDefaultsVersion="$SERVICE_DEFAULTS_VERSION" \
    /p:MessagingContractsVersion="$MESSAGING_CONTRACTS_VERSION" \
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
