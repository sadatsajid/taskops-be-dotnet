# Architecture Notes

TaskOps has completed the Phase 10 modular monolith boundary refactor. It remains one deployable API, one process, one database, and one `TaskOpsDbContext`, but product modules are now visible across API, Application, Domain, and Infrastructure.

The intent is clean ownership without framework ceremony: no MediatR, no generic repositories, no interface-per-class layer.

## Current Shape

```text
src/
  TaskOps.Api/
    Modules/           # Minimal API endpoint mapping and org authorization policies
    Infrastructure/    # HTTP middleware, auth setup, OpenAPI
    Shared/Api/        # HTTP response envelope and endpoint result helpers

  TaskOps.Application/
    Modules/           # Contracts, validators, service interfaces, result types
      Identity/
      Organizations/
        Access/        # Deliberate organization access contracts
      Projects/
      Issues/
    SharedKernel/      # ServiceResult, pagination, current user abstraction

  TaskOps.Domain/
    Modules/           # Durable business entities, enums, and module rules
      Identity/
      Organizations/
      Projects/
      Issues/
    SharedKernel/      # Stable base mechanics such as AuditableEntity

  TaskOps.Infrastructure/
    Modules/           # EF-backed module service implementations
    Persistence/       # DbContext, configurations, migrations, database startup
    Security/          # JWT signing options and key provider
```

## Rules

- API endpoints stay thin: bind HTTP inputs, call application services, translate failures to HTTP.
- Application owns request/response DTOs, validators, service interfaces, and expected failure shapes.
- Infrastructure owns EF Core, PostgreSQL behavior, JWT token creation, and concrete service implementations.
- Domain owns entities, enums, and durable business rules. It must not reference EF Core or ASP.NET Core.
- EF Core is still used directly through `TaskOpsDbContext`; do not add repository abstractions.
- Organization access must be scoped through membership checks, not global roles.
- Cross-module access should be deliberate. Organization access contracts are exposed by the Organizations module; they are not generic shared helpers.
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

## Organization Authorization

- Organization access lives in `Modules/Organizations/Access`, owned by the Organizations module rather than `Shared/`.
- Org-scoped endpoints declare one of three named policies: `Organization.Member`, `Organization.Owner`, `Organization.ProjectManagement`. Policies are registered by the Organizations module via `services.Configure<AuthorizationOptions>`.
- A single `OrganizationMembershipHandler` reads `{organizationId}` from the route, resolves membership through `IOrganizationAccessService`, and on success stashes the `OrganizationMember` into a scoped `IOrganizationContext` that services can read when fine-grained role logic is needed (e.g. the assigned-developer status-change override in `IssueService`).
- A custom `IAuthorizationMiddlewareResultHandler` preserves the API's three-way semantics: `401` for unauthenticated, `404` for authenticated non-member (no existence leak), `403` ProblemDetails for member-with-wrong-role.
- Services no longer translate access status into module failure enums; the authorization pipeline short-circuits before the service runs.

## Testing

The integration test project uses Testcontainers PostgreSQL. This is deliberate: persistence behavior should be tested against PostgreSQL, not EF Core's in-memory provider.
