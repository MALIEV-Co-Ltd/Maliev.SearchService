# syntax=docker/dockerfile:1.4

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Maliev.SearchService.Api/Maliev.SearchService.Api.csproj", "Maliev.SearchService.Api/"]
COPY ["Maliev.SearchService.Domain/Maliev.SearchService.Domain.csproj", "Maliev.SearchService.Domain/"]
COPY ["Maliev.SearchService.Application/Maliev.SearchService.Application.csproj", "Maliev.SearchService.Application/"]
COPY ["Maliev.SearchService.Infrastructure/Maliev.SearchService.Infrastructure.csproj", "Maliev.SearchService.Infrastructure/"]
COPY ["nuget.config", "."]
RUN --mount=type=secret,id=nuget_username \
  --mount=type=secret,id=nuget_password \
  NUGET_USERNAME=$(cat /run/secrets/nuget_username) \
  NUGET_PASSWORD=$(cat /run/secrets/nuget_password) \
  dotnet restore "./Maliev.SearchService.Api/Maliev.SearchService.Api.csproj"
COPY . .
WORKDIR "/src/Maliev.SearchService.Api"
RUN dotnet build "./Maliev.SearchService.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Maliev.SearchService.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
RUN chown -R app:app /app
USER app
COPY --chown=app:app --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:8080/search/liveness || exit 1
ENTRYPOINT ["dotnet", "Maliev.SearchService.Api.dll"]
