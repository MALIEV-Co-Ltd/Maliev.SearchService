# MALIEV Search Service

[![Develop CI](https://github.com/MALIEV-Co-Ltd/Maliev.SearchService/actions/workflows/ci-develop.yml/badge.svg)](https://github.com/MALIEV-Co-Ltd/Maliev.SearchService/actions/workflows/ci-develop.yml)
[![Main CI](https://github.com/MALIEV-Co-Ltd/Maliev.SearchService/actions/workflows/ci-main.yml/badge.svg)](https://github.com/MALIEV-Co-Ltd/Maliev.SearchService/actions/workflows/ci-main.yml)

SearchService owns the MALIEV global search index. It stores permission-scoped projections of user-facing resources published by the platform services and returns only documents visible to the current caller.

## Architecture

The service follows the standard MALIEV Clean Architecture layout:

| Project | Responsibility |
| --- | --- |
| `Maliev.SearchService.Api` | Controllers, MassTransit consumers, startup, IAM registration |
| `Maliev.SearchService.Application` | DTOs, service contracts, permission matching |
| `Maliev.SearchService.Domain` | Search document entity |
| `Maliev.SearchService.Infrastructure` | EF Core DbContext, persistence configuration, search implementation |
| `Maliev.SearchService.Tests` | Unit and PostgreSQL integration tests |

The index is eventually consistent. Source services publish `SearchDocumentUpsertedEvent` and `SearchDocumentDeletedEvent` from `Maliev.MessagingContracts`; SearchService consumes those messages and updates PostgreSQL.

## API Endpoints

| Method | Route | Permission | Description |
| --- | --- | --- | --- |
| `GET` | `/search/v1/search?query={text}&limit={n}&type={type}&area={area}` | `search.documents.read` | Searches documents visible to the caller. |
| `POST` | `/search/v1/search/reindex?sourceService={service}` | `search.documents.reindex` | Publishes a reindex command for one source service or all services. |
| `GET` | `/search/scalar` | Development/Staging only | Scalar API documentation. |

## Permissions Model

SearchService checks two permission layers:

1. Endpoint access through `[RequirePermission]`.
2. Per-document filtering against each indexed row's `requiredPermission`.

`platform.owner` and wildcard permission claims are honored by the search permission evaluator. Results for unauthorized resources are omitted rather than shown as locked rows.

## Local Commands

```powershell
dotnet build Maliev.SearchService.slnx
dotnet test Maliev.SearchService.slnx --verbosity normal
dotnet ef migrations add <Name> --project Maliev.SearchService.Infrastructure --startup-project Maliev.SearchService.Infrastructure
```
