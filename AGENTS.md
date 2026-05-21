# AGENTS.md

Guidance for AI coding agents working in this repository.

## Repository Purpose

TaskOps is a pragmatic .NET learning project: a project-management and issue-tracking backend built with real architectural pressure, not a toy CRUD API.

The product direction is documented in `TaskOps_Project_Roadmap.md`. Read it before making broad design changes. The most important product rule is organization-scoped access:

```text
User is Admin in Organization A
User is Developer in Organization B
```

Do not model authorization as global user roles unless the roadmap explicitly changes.

## Current Architecture

This repository is intentionally still a single ASP.NET Core API project. Do not split it into Clean Architecture projects, MediatR handlers, generic repositories, or microservices without an explicit request.

```text
src/
  TaskOps.Api/
    Features/
      Auth/
      System/
    Infrastructure/
    Persistence/
      Configurations/
      Entities/
      Migrations/
    Shared/
      Api/
      Security/
tests/
  TaskOps.Api.Tests/
docs/
TaskOps_Project_Roadmap.md
```

Feature code belongs in `src/TaskOps.Api/Features/<FeatureName>`. Startup and cross-cutting wiring belongs in extension methods under `Infrastructure` or the relevant slice.

## Design Rules

- Keep `Program.cs` small. Prefer focused registration/middleware extension methods.
- Use EF Core directly through `TaskOpsDbContext`; do not add repository abstractions by default.
- Do not return EF entities from endpoints. Project into response DTOs.
- Keep lazy loading off. Use explicit `Include` only when entity graphs are genuinely needed.
- Put entity configuration in `Persistence/Configurations`.
- Treat PostgreSQL behavior as real behavior. Do not use EF Core in-memory provider for persistence tests.
- Organization and project access checks must be scoped by membership.
- Access tokens contain user identity only. Do not put organization roles in JWTs.
- Refresh tokens are stored hashed, rotated on refresh, and revoked on logout.

## C# Conventions

- Nullable reference types are enabled. Treat warnings as real design feedback.
- Prefer `record` types for request/response DTOs and immutable message-like data.
- Keep services and infrastructure classes `sealed` unless inheritance is intentional.
- Use async all the way for I/O. Do not block on `.Result` or `.Wait()`.
- Pass `CancellationToken` through database and network calls.
- Use explicit mapping code between entities and DTOs. Do not add reflection-based mappers by default.
- Represent expected business failures with result-style return values or stable API responses, not thrown exceptions.

## Data Access Rules

- Use `AsNoTracking()` for read-only queries.
- Use projections for list/detail endpoints instead of loading full entities.
- Apply `Take`/pagination to list endpoints. Do not add unbounded collection endpoints.
- Avoid query-in-loop patterns. Batch in SQL or compose one query.
- Do joins in the database, not in application memory.
- Use `ExecuteUpdateAsync` / `ExecuteDeleteAsync` for set-based updates when appropriate.
- Configure maximum string lengths and indexes in EF configurations, then validate API input before it reaches the database.
- When adding user-visible natural keys or uniqueness rules, enforce them with database constraints and translate constraint violations into stable API errors.

## Stack

- .NET 10
- ASP.NET Core minimal APIs
- EF Core with Npgsql
- PostgreSQL 17 via Docker Compose
- JWT bearer authentication
- OpenAPI / Swagger UI in Development
- xUnit, FluentAssertions, Testcontainers PostgreSQL

## Common Commands

Restore tools:

```bash
dotnet tool restore
```

Start local PostgreSQL:

```bash
docker compose up -d postgres
```

Run the API:

```bash
dotnet run --project src/TaskOps.Api/TaskOps.Api.csproj
```

Build:

```bash
dotnet build TaskOps.slnx -m:1
```

Run tests:

```bash
dotnet test tests/TaskOps.Api.Tests/TaskOps.Api.Tests.csproj
```

Integration tests require Docker because they use Testcontainers.

## EF Core Migrations

Use the local tool manifest from the repo root:

```bash
dotnet tool run dotnet-ef migrations add <MigrationName> \
  --project src/TaskOps.Api/TaskOps.Api.csproj \
  --startup-project src/TaskOps.Api/TaskOps.Api.csproj \
  --output-dir Persistence/Migrations
```

Manual database update:

```bash
dotnet tool run dotnet-ef database update \
  --project src/TaskOps.Api/TaskOps.Api.csproj \
  --startup-project src/TaskOps.Api/TaskOps.Api.csproj
```

When changing entities, include the migration and update tests if behavior changes.

Do not manually edit generated migrations unless adding deliberate custom SQL. Do not delete or rename migration files by hand; use `dotnet-ef migrations remove` for unapplied migrations.

## Testing Expectations

Add or update tests when changing:

- Auth flows
- Refresh-token behavior
- Organization membership or authorization checks
- EF mappings, indexes, migrations, or seed data
- API response contracts

Prefer integration tests against PostgreSQL for database behavior. For pure validation or mapping logic, small unit tests are fine if they stay cheap and focused.

## API Conventions

- Successful responses use the shared API envelope from `Shared/Api`.
- Error responses should be consistent ProblemDetails-style responses.
- Validation failures should return `400`.
- Invalid credentials and invalid refresh tokens should not leak user existence.
- Duplicate natural keys should be enforced by database constraints and translated into stable API failures.
- Treat endpoint paths, JSON field names, and status codes as API contracts. Avoid breaking changes unless the user explicitly asks for them.
- Prefer adding new endpoints or response fields over changing existing behavior silently.

## Build and Project Structure

- Keep `TaskOps.slnx` as the solution file. Do not add a parallel `.sln`.
- Respect `global.json`; do not casually bump the SDK version.
- Use the local tool manifest for EF and hook tooling instead of assuming global installs.
- Keep package choices boring and justified. Do not add libraries for patterns the framework already handles well.

## Security Notes

- Never commit production secrets.
- `Jwt:SigningKey` must be configured outside source-controlled production config.
- Do not bypass organization-scoped access because an endpoint is "temporary."
- Treat refresh-token rotation as single-use and concurrency-sensitive.

## Local Documentation

Use these files as source material before changing architecture:

- `TaskOps_Project_Roadmap.md`
- `docs/architecture-notes.md`
- `docs/phase-2-database.md`
- `README.md`

If those docs disagree with code, prefer the code for current behavior and update the docs when the change is intentional.

## External Inspiration

This guide intentionally borrows compatible ideas from Aaron Stannard's `dotnet-skills` repository, especially the skills around modern C#, EF Core patterns, database performance, Testcontainers, API compatibility, and .NET project structure. Apply those ideas pragmatically to TaskOps' current single-project architecture rather than importing enterprise structure prematurely.
