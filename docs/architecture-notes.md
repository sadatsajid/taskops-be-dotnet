# Architecture Notes

TaskOps is intentionally still one ASP.NET Core API project. The structure now follows a pragmatic vertical-slice style without introducing Clean Architecture, MediatR, generic repositories, or module projects too early.

## Current Shape

```text
Features/
  Auth/
  Organizations/
  Projects/
  System/
Infrastructure/
Persistence/
Shared/
```

## Rules

- New product behavior should start inside `Features/<FeatureName>`.
- Feature slices own request DTOs, response DTOs, endpoint mapping, validation, and handler logic.
- EF Core is used directly through `TaskOpsDbContext`.
- Do not expose EF entities as API responses.
- Organization access must be scoped through membership checks, not global roles.
- Keep startup code in `Program.cs` small; compose feature endpoint mapping through `MapTaskOpsEndpoints()`.
- Keep infrastructure registration focused on platform wiring; compose feature services through `AddTaskOpsFeatures()`.

## Auth Decisions

- Access tokens contain user identity only.
- Organization roles are not embedded in access tokens.
- Refresh tokens are stored as SHA-256 hashes.
- Refresh tokens are rotated on refresh and revoked on logout.

## Testing

The integration test project uses Testcontainers PostgreSQL. This is deliberate: persistence behavior should be tested against PostgreSQL, not EF Core's in-memory provider.
