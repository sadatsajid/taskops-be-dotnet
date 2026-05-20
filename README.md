# TaskOps

**TaskOps** is a project-management and issue-tracking backend built to learn the .NET ecosystem. It is intentionally pragmatic: start as a single ASP.NET Core API and evolve toward a modular monolith with real-world concerns (auth, tenancy, validation, tests, observability, and deployment).

## Tech stack

| Layer | Choice |
|--------|--------|
| Runtime | [.NET 10](https://dotnet.microsoft.com/) (SDK pinned via `global.json`) |
| API | ASP.NET Core (minimal APIs) |
| Data | Entity Framework Core, Npgsql |
| Database | PostgreSQL 17 (local via Docker Compose) |
| API docs | OpenAPI + Swagger UI (Development) |

## Repository layout

```text
src/
  TaskOps.Api/          # Web API, features, persistence, infrastructure
tests/
  TaskOps.Api.Tests/    # PostgreSQL-backed integration tests
docs/                   # Setup and phase notes (e.g. phase-0-setup.md)
docker-compose.yml      # PostgreSQL for local development
TaskOps_Project_Roadmap.md   # Goals, architecture evolution, backlog
```

## Prerequisites

- **.NET SDK** matching `global.json` (currently **10.0.300**)
- **Docker** (optional but recommended for PostgreSQL)
- **EF Core CLI** — restore local tools after clone:

  ```bash
  dotnet tool restore
  ```

## Quick start

### 1. Start PostgreSQL

From the repository root:

```bash
docker compose up -d
```

Default compose credentials match `appsettings.Development.json` (`taskops` / `taskops_dev_password`, database `taskops`).

### 2. Run the API

```bash
dotnet run --project src/TaskOps.Api/TaskOps.Api.csproj
```

The app uses `Properties/launchSettings.json` (e.g. **http://localhost:5000** for the `http` profile).

### 3. Verify

| Endpoint | Description |
|----------|-------------|
| `GET /` | Service welcome payload |
| `GET /api/status` | Environment and time (JSON envelope) |
| `GET /health` | Health checks (includes PostgreSQL when configured) |
| `POST /api/auth/register` | Register a user and issue access/refresh tokens |
| `POST /api/auth/login` | Login with email/password |
| `POST /api/auth/refresh` | Rotate a refresh token |
| `POST /api/auth/logout` | Revoke a refresh token |
| `GET /api/auth/me` | Return the authenticated user |

In Development, OpenAPI is available under `/openapi/v1.json` and Swagger UI is mapped by the project’s OpenAPI UI wiring.

### Database and migrations

Development settings enable applying EF Core migrations and optional seeding on startup (`appsettings.Development.json` → `Database` section). To add migrations from the repo root:

```bash
dotnet tool run dotnet-ef migrations add <Name> \
  --project src/TaskOps.Api/TaskOps.Api.csproj \
  --startup-project src/TaskOps.Api/TaskOps.Api.csproj \
  --output-dir Persistence/Migrations
```

## Tests

Integration tests use Testcontainers with real PostgreSQL:

```bash
dotnet test tests/TaskOps.Api.Tests/TaskOps.Api.Tests.csproj
```

Docker Desktop must be running.

Build the whole solution with single-node MSBuild execution:

```bash
dotnet build TaskOps.slnx -m:1
```

## Configuration

- **Base:** `src/TaskOps.Api/appsettings.json` — connection string placeholder and default flags.
- **Development:** `src/TaskOps.Api/appsettings.Development.json` — local PostgreSQL connection string and startup behavior.
- **JWT:** configure `Jwt:SigningKey` with at least 32 characters. Development uses a local-only key.

Override secrets and environment-specific values with environment variables or user secrets in real deployments; do not commit production credentials.

## Documentation

- [Phase 0 setup](docs/phase-0-setup.md) — local toolchain notes.
- [Architecture notes](docs/architecture-notes.md) — current project structure and rules.
- [Project roadmap](TaskOps_Project_Roadmap.md) — vision, stack, and evolution plan.

## Contributing

This repository is a personal learning project. If you fork it, keep the same pragmatic rule set: small steps, clear boundaries, and tests where they buy confidence.
