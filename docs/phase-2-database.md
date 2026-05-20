# Phase 2 Database Notes

Phase 2 adds PostgreSQL and EF Core to TaskOps.

## Start PostgreSQL

From the repository root:

```bash
docker compose up -d postgres
```

Check the container:

```bash
docker compose ps
```

## Run The API

```bash
dotnet run --project src/TaskOps.Api/TaskOps.Api.csproj
```

In development, the API is configured to:

- Apply EF Core migrations on startup
- Seed one demo user
- Seed one demo organization
- Seed one demo project
- Seed one demo issue

## Useful URLs

```text
http://localhost:5000/swagger
http://localhost:5000/health
http://localhost:5000/api/status
```

The health endpoint includes a PostgreSQL check.

## Auth Endpoints Added After Phase 2

The current API also includes the Phase 3 auth slice:

```text
POST /api/auth/register
POST /api/auth/login
POST /api/auth/refresh
POST /api/auth/logout
GET  /api/auth/me
```

Refresh tokens are stored hashed in PostgreSQL.

## Connection String

Development connection string:

```text
Host=localhost;Port=5432;Database=taskops;Username=taskops;Password=taskops_dev_password
```

This is local development only.

## EF Core Commands

The repository uses a local EF tool manifest.

Install or restore local tools:

```bash
dotnet tool restore
```

Add a migration:

```bash
dotnet tool run dotnet-ef migrations add MigrationName \
  --project src/TaskOps.Api/TaskOps.Api.csproj \
  --startup-project src/TaskOps.Api/TaskOps.Api.csproj \
  --output-dir Persistence/Migrations
```

Apply migrations manually:

```bash
dotnet tool run dotnet-ef database update \
  --project src/TaskOps.Api/TaskOps.Api.csproj \
  --startup-project src/TaskOps.Api/TaskOps.Api.csproj
```

Current migrations:

- `20260520083630_InitialCreate`
- `20260520100819_AddRefreshTokens`

## Inspect Seed Data

```bash
docker compose exec -T postgres psql -U taskops -d taskops
```

Example queries:

```sql
select * from "Users";
select * from "Organizations";
select * from "Projects";
select * from "Issues";
select * from "__EFMigrationsHistory";
```

## Reset Local Database

This deletes local development data:

```bash
docker compose down -v
docker compose up -d postgres
dotnet run --project src/TaskOps.Api/TaskOps.Api.csproj
```
