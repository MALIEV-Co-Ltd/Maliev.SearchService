# Maliev.SearchService Repository Guide

This document contains instructions for AI agents operating in this codebase.

## 1. Environment & Build Commands

This is a .NET 10.0 project using Visual Studio Solution (`.slnx`).

All commands run from within this service directory (`B:\maliev\Maliev.SearchService`).

```powershell
dotnet build Maliev.SearchService.slnx
dotnet test Maliev.SearchService.slnx --verbosity normal
dotnet test --filter "FullyQualifiedName~SearchIndexServiceTests"
dotnet test Maliev.SearchService.slnx --collect:"XPlat Code Coverage"
dotnet format Maliev.SearchService.slnx
dotnet ef migrations add <Name> --project Maliev.SearchService.Infrastructure --startup-project Maliev.SearchService.Infrastructure
```

## 2. Project Structure

Clean Architecture is mandatory:

```text
Maliev.SearchService/
├── Maliev.SearchService.Api/            # Controllers, MassTransit consumers, startup
├── Maliev.SearchService.Application/    # DTOs, interfaces, permission matching
├── Maliev.SearchService.Domain/         # Search document entity
├── Maliev.SearchService.Infrastructure/ # EF Core DbContext and index implementation
├── Maliev.SearchService.Tests/          # Unit and integration tests
├── Directory.Build.props
└── Maliev.SearchService.slnx
```

## 3. Code Style & Conventions

- Namespaces are file-scoped.
- Public members require XML documentation.
- Use `[ApiController]`, `[ApiVersion("1.0")]`, and versioned routes.
- All endpoints require `[RequirePermission]`; do not use plain `[Authorize]`.
- Permission constants live in `SearchPermissions` and use GCP-style plural resources, for example `search.documents.read`.
- Use structured logging with placeholders.
- Use manual mapping only; AutoMapper is banned.
- Use DataAnnotations or manual validation only; FluentValidation is banned.
- Use Scalar API docs at `/search/scalar`; Swagger/Swashbuckle is banned.
- Use Testcontainers with real PostgreSQL for database tests; InMemoryDatabase and SQLite are banned.

## 4. Testing Rules

- Framework: xUnit with standard `Assert.*`.
- Integration tests use PostgreSQL Testcontainers.
- MassTransit consumers must have consumer tests when behavior changes.
- Eventual consistency tests use polling helpers, never `Task.Delay`.
- Keep `TreatWarningsAsErrors=true`; warnings must be fixed, not suppressed.

## 5. Mandatory Rules

- SearchService owns the index only; source services own their domain data.
- Search results must be filtered by the indexed `requiredPermission` before returning to callers.
- Unauthorized resources are omitted from results.
- The index is eventually consistent through centralized `Maliev.MessagingContracts`.
- EF Core Design belongs only in the Infrastructure project.
- Secrets must come from GCP Secret Manager or environment variables.

## 6. Git Rules

- This folder is an independent git repo.
- Commit meaningful units of work locally.
- Do not push without being asked.
- Never use `git checkout` to discard changes; commit first, then revert safely if needed.
