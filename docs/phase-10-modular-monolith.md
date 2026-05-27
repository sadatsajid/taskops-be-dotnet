# Phase 10 Modular Monolith

TaskOps is now shaped as a modular monolith inside one ASP.NET Core API project.

This is intentional. The system keeps one deployable application, one PostgreSQL database, one EF Core `DbContext`, and one migration stream. The modular-monolith boundary is expressed first through product modules, not separate services or separate databases.

## Current Module Layout

```text
src/
  TaskOps.Api/
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

## Module Ownership

Each product module owns its request and response DTOs, endpoint mapping, validators, module services, authorization checks relevant to that module, and internal business behavior.

The API startup remains thin. `Program.cs` composes platform services, persistence, OpenAPI, middleware, and module endpoint registration.

## Boundary Rules

- Keep organization-scoped access explicit and membership-based.
- Keep shared code small and stable.
- Do not add generic repositories over EF Core.
- Do not split module projects until the single-project boundary becomes difficult to maintain.
- Do not introduce separate databases.
- Prefer deliberate module-to-module contracts over casual cross-module data access.

## Completed Since Phase 10

- Organization access behavior now lives in `Modules/Organizations/Access` and is exposed through named ASP.NET authorization policies (`Organization.Member`, `Organization.Owner`, `Organization.ProjectManagement`). Endpoints declare the policy; services no longer carry per-method access boilerplate. See `architecture-notes.md` for the runtime contract.

## Next Evolution

The next useful improvements are not more folders. They are stronger boundaries:

- Introduce a domain-event / outbox seam before Notifications is built, so Comments and activity logs share one mechanism.
- Introduce Notifications, Files, and Dashboard as modules from birth.
- Consider PostgreSQL schemas later only after ownership is stable.
- Split into module projects only if compile-time boundaries become worth the extra project overhead.
