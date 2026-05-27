# Architecture Notes

TaskOps has completed the Phase 9 boundary split. The codebase is now a small layered backend, not a modular monolith yet.

The intent is clean ownership without framework ceremony: no MediatR, no generic repositories, no interface-per-class layer.

## Current Shape

```text
src/
  TaskOps.Api/
    Features/          # Minimal API endpoint mapping
    Infrastructure/    # HTTP middleware, auth setup, OpenAPI
    Shared/Api/        # HTTP response envelope and endpoint result helpers

  TaskOps.Application/
    Features/          # Contracts, validators, service interfaces, result types
    Shared/Api/        # ServiceResult, pagination, validation helpers
    Shared/Security/   # Current user and organization access abstractions

  TaskOps.Domain/
    Entities/          # Durable business entities and enums
    Security/          # Domain-level security rules such as scoped role policies

  TaskOps.Infrastructure/
    Features/          # EF-backed application service implementations
    Persistence/       # DbContext, configurations, migrations, database startup
    Security/          # JWT/token/signing and organization access implementations
```

## Rules

- API endpoints stay thin: bind HTTP inputs, call application services, translate failures to HTTP.
- Application owns request/response DTOs, validators, service interfaces, and expected failure shapes.
- Infrastructure owns EF Core, PostgreSQL behavior, JWT token creation, and concrete service implementations.
- Domain owns entities, enums, and durable business rules. It must not reference EF Core or ASP.NET Core.
- EF Core is still used directly through `TaskOpsDbContext`; do not add repository abstractions.
- Organization access must be scoped through membership checks, not global roles.
- Keep startup code in `Program.cs` small; compose endpoint mapping through `MapTaskOpsEndpoints()`.

## Validation And API Contracts

- Request DTOs are validated with FluentValidation validators in `TaskOps.Application`.
- Validators handle request shape only: required fields, lengths, enum names, date ranges, and similar API contract rules.
- Services remain responsible for business rules that require state or authorization, such as duplicate natural keys, organization membership, project ownership, archived projects, and issue assignment rules.
- Validation failures are returned through the shared validation result path as `400` ProblemDetails responses.
- Response DTOs stay explicit and purpose-built; EF entities are not API contracts.

## Auth Decisions

- Access tokens contain user identity only.
- Organization roles are not embedded in access tokens.
- Refresh tokens are stored as SHA-256 hashes.
- Refresh tokens are rotated on refresh and revoked on logout.

## Testing

The integration test project uses Testcontainers PostgreSQL. This is deliberate: persistence behavior should be tested against PostgreSQL, not EF Core's in-memory provider.
