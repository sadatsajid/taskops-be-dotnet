# Architecture Notes

TaskOps is intentionally still one ASP.NET Core API project. The structure now follows a modular-monolith style inside that single deployable API without introducing Clean Architecture ceremony, MediatR, generic repositories, or module projects too early.

## Current Shape

```text
Modules/
  Identity/
  Organizations/
  Projects/
  Issues/
  System/
Infrastructure/
Persistence/
Shared/
```

## Rules

- New product behavior should start inside `Modules/<ModuleName>`.
- Modules own request DTOs, response DTOs, endpoint mapping, validators, authorization checks relevant to the module, and handler logic.
- EF Core is used directly through `TaskOpsDbContext`.
- Do not expose EF entities as API responses.
- Organization access must be scoped through membership checks, not global roles.
- Keep startup code in `Program.cs` small; compose module endpoint mapping through `MapTaskOpsEndpoints()`.
- Keep infrastructure registration focused on platform wiring; compose module services through `AddTaskOpsModules()`.

## Validation And API Contracts

- Request DTOs are validated with module-local FluentValidation validators.
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
