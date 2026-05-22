# TaskOps

**TaskOps** is a project-management and issue-tracking backend built to learn the .NET ecosystem. It is intentionally pragmatic: start as a single ASP.NET Core API and evolve toward a modular monolith with real-world concerns (auth, tenancy, validation, tests, observability, and deployment).

## Tech stack

| Layer | Choice |
|--------|--------|
| Runtime | [.NET 10](https://dotnet.microsoft.com/) (SDK pinned via `global.json`) |
| API | ASP.NET Core (minimal APIs) |
| Validation | FluentValidation |
| Data | Entity Framework Core, Npgsql |
| Database | PostgreSQL 17 (local via Docker Compose) |
| API docs | OpenAPI + Swagger UI (Development) |

## Repository layout

```text
src/
  TaskOps.Api/          # Web API, feature slices, persistence, infrastructure
    Features/           # Auth, Organizations, System, and future product slices
    Shared/             # Shared API envelopes/results and security helpers
tests/
  TaskOps.Api.Tests/    # PostgreSQL-backed integration tests
docs/                   # Setup and phase notes (e.g. phase-0-setup.md)
docker-compose.yml      # PostgreSQL for local development
TaskOps_Project_Roadmap.md   # Goals, architecture evolution, backlog
```

## Prerequisites

- **.NET SDK** matching `global.json` (currently **10.0.300**)
- **Docker** (optional but recommended for PostgreSQL)
- **Local .NET tools** — restore after clone:

  ```bash
  dotnet tool restore
  ```

  This restores EF Core CLI and Husky.Net.

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
| `GET /api/organizations` | List organizations for the authenticated user |
| `POST /api/organizations` | Create an organization |
| `GET /api/organizations/{organizationId}` | Get an organization scoped by membership |
| `PUT /api/organizations/{organizationId}` | Update organization settings as owner |
| `GET /api/organizations/{organizationId}/members` | List organization members |
| `POST /api/organizations/{organizationId}/members` | Add an organization member as owner |
| `PUT /api/organizations/{organizationId}/members/{memberId}/role` | Change a member role as owner |
| `DELETE /api/organizations/{organizationId}/members/{memberId}` | Remove a member as owner |
| `GET /api/organizations/{organizationId}/projects` | List non-archived projects for an organization |
| `POST /api/organizations/{organizationId}/projects` | Create a project as owner, admin, or project manager |
| `GET /api/organizations/{organizationId}/projects/{projectId}` | Get project details scoped by organization membership |
| `PUT /api/organizations/{organizationId}/projects/{projectId}` | Update a project as owner, admin, or project manager |
| `POST /api/organizations/{organizationId}/projects/{projectId}/archive` | Archive a project |

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

## Local validation hooks

The repository uses Husky.Net for Git hook validation.

After cloning and restoring tools, install the hooks once:

```bash
dotnet husky install
```

The hooks run the same commands developers should run manually:

| Hook | Validation |
|------|------------|
| `pre-commit` | `dotnet format TaskOps.slnx --verify-no-changes --no-restore` |
| `pre-push` | `dotnet restore TaskOps.slnx`, `dotnet build TaskOps.slnx -m:1 --no-restore`, `dotnet test tests/TaskOps.Api.Tests/TaskOps.Api.Tests.csproj --no-build` |

`pre-push` requires Docker because the integration tests use Testcontainers PostgreSQL. Local hooks may be bypassed with `--no-verify` for exceptional cases, but CI should remain the merge authority.

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
