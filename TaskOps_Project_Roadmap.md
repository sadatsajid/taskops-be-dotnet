# TaskOps Project Roadmap

## Goal

Build **TaskOps**, a project management and issue tracking system, as a serious first .NET application.

The goal is not to clone Jira. The goal is to learn the .NET ecosystem by building a backend that contains the same architectural pressures real systems have:

- Authentication
- Organization-level access control
- Project and issue management
- Database modeling
- Validation
- API design
- Testing
- Background work
- Real-time updates
- Observability
- Deployment readiness

The project should start pragmatic and evolve toward a modular monolith. Do not begin with a heavy enterprise template.

## Recommended Stack

- .NET 10 LTS
- ASP.NET Core Web API
- EF Core
- PostgreSQL
- Docker / Docker Compose
- JWT authentication
- FluentValidation
- Swagger / OpenAPI
- xUnit
- FluentAssertions
- Testcontainers or Docker-backed integration tests
- Serilog
- SignalR
- Hangfire or Quartz.NET
- Redis later, not at the beginning
- React + TypeScript frontend later

## Target Architecture Evolution

Start with:

```text
TaskOps.Api
```

Feature folders inside one API project:

```text
Features/
  Auth/
  Organizations/
  Projects/
  Issues/
  Comments/
  Dashboard/
Infrastructure/
Persistence/
Shared/
```

Then evolve to:

```text
TaskOps.Api
TaskOps.Application
TaskOps.Domain
TaskOps.Infrastructure
TaskOps.Tests
```

Eventually organize the application as a modular monolith:

```text
Modules/
  Identity/
  Organizations/
  Projects/
  Issues/
  Notifications/
  Files/
SharedKernel/
Infrastructure/
```

Do not split into microservices. That would be premature and mostly educationally harmful for this project.

## Core Principle

The hardest part of TaskOps is not CRUD.

The hardest part is this rule:

> A user has permissions inside an organization, and later possibly inside a project.

This must influence the data model, authorization logic, queries, tests, and API design from the beginning.

Avoid global roles like:

```text
User is Admin
```

Prefer scoped roles:

```text
User is Admin in Organization A
User is Developer in Organization B
```

## Phase 0: Local Environment Setup

### Objective

Prepare your machine for .NET backend development.

### Install

- .NET SDK 10 LTS
- Docker Desktop
- PostgreSQL client tooling
- Rider, Visual Studio, or VS Code with C# Dev Kit
- Git
- Postman or Insomnia
- TablePlus, DBeaver, or pgAdmin

### Verify

Run:

```bash
dotnet --version
docker --version
git --version
```

### Learn

Understand:

- What the .NET SDK is
- What ASP.NET Core is
- What a solution file is
- What a project file is
- How dependency injection works in ASP.NET Core
- How configuration works through `appsettings.json`, environment variables, and user secrets

### Exit Criteria

- .NET SDK works locally
- Docker works locally
- You can create and run a blank ASP.NET Core Web API
- You understand the difference between a solution and a project

## Phase 1: Skeleton API

### Objective

Create the first runnable backend.

### Build

- ASP.NET Core Web API
- Health check endpoint
- Swagger enabled in development
- Basic API response conventions
- Global exception handling
- Basic request logging

### Keep It Simple

Use one project:

```text
TaskOps.Api
```

Do not add Clean Architecture yet.
Do not add MediatR yet.
Do not add repositories yet.

### Learn

- Program startup model
- Middleware pipeline
- Controllers vs minimal APIs
- Built-in dependency injection
- Configuration binding
- Environment-specific settings

### Exit Criteria

- API runs locally
- Swagger opens
- Health endpoint works
- Errors return consistent JSON

## Phase 2: Database Foundation

### Objective

Add PostgreSQL and EF Core.

### Build

- Docker Compose with PostgreSQL
- EF Core `DbContext`
- Initial migration
- Database seeding for development
- Basic audit fields:
  - `CreatedAt`
  - `UpdatedAt`

### First Entities

- User
- Organization
- OrganizationMember
- Project
- Issue

### Important Modeling Choices

Use `Issue`, not `Task`, as the main work item name.

Reason: `Task` conflicts mentally and technically with `System.Threading.Tasks.Task` in .NET.

Suggested issue statuses:

- Todo
- InProgress
- InReview
- Done

Suggested issue priorities:

- Low
- Medium
- High
- Critical

### EF Core Rules

- Keep lazy loading off
- Use explicit includes only when needed
- Prefer projection DTOs for list endpoints
- Avoid returning EF entities directly from controllers
- Keep `DbContext` scoped per request
- Do not inject `DbContext` into singletons

### Exit Criteria

- PostgreSQL runs in Docker
- EF Core migrations work
- Database can be created from scratch
- Initial entities are persisted correctly

## Phase 3: Authentication

### Objective

Implement basic identity without overbuilding authorization yet.

### Build

- User registration
- Login
- Password hashing
- JWT access token
- Refresh token table
- Logout / revoke refresh token
- Current user endpoint

### Avoid Initially

- External OAuth
- Magic links
- Multi-factor authentication
- Complex permission systems

### Important Security Rules

- Never store raw passwords
- Store refresh tokens hashed if possible
- Keep access tokens short-lived
- Keep refresh tokens revocable
- Do not put organization permissions directly into long-lived tokens unless you understand the invalidation problem

### Exit Criteria

- User can register
- User can log in
- API can identify authenticated user
- Refresh token rotation works
- Protected endpoints reject anonymous requests

## Phase 4: Organization Membership And Scoped Roles

### Objective

Model the core authorization concept correctly.

### Build

- Create organization
- Update organization
- Invite or add organization member
- List organization members
- Change member role
- Remove member

### Roles

Start with:

- Owner
- Admin
- ProjectManager
- Developer
- Viewer

### Role Rules

- Organization creator becomes Owner
- Owner can manage organization settings and members
- Admin can manage most organization resources
- ProjectManager can manage projects and issues
- Developer can work on assigned issues
- Viewer can read only

### Blunt Warning

This is where many beginner APIs rot.

If every endpoint only checks `User.Identity.IsAuthenticated`, the app is fake-secure. Every organization and project query must be scoped to the current user's membership.

Use an explicit organization-access helper that distinguishes unauthenticated users, non-members, and members without enough role. Do not let `null` membership checks spread across endpoints or services.

### Exit Criteria

- User cannot access an organization they do not belong to
- User cannot modify organization data without sufficient role
- Membership checks are tested
- Role behavior is explicit and not scattered randomly through controllers

## Phase 5: Project Management

### Objective

Build project CRUD under organizations.

### Build

- Create project
- Update project
- Archive project
- List projects in organization
- Get project details
- Project membership or project visibility rules

### Design Choice

At first, use organization membership for project access.

Do not add separate project-level roles until the organization-level model is stable.

### Exit Criteria

- Projects belong to organizations
- Project queries are organization-scoped
- Archived projects do not appear in default lists
- Unauthorized users cannot access projects

## Phase 6: Issue Management

### Objective

Build the central TaskOps workflow.

### Build

- Create issue
- Update title and description
- Assign issue to organization member
- Change status
- Change priority
- Set due date
- List issues
- Filter issues
- Search issues
- Sort issues
- Pagination

### Filters

Support:

- Status
- Priority
- Assignee
- Project
- Created date
- Due date
- Search text

### Important Rules

- Assignee must be a member of the issue's organization
- Issue must belong to a project
- Project must belong to the organization in the route or request
- Status transitions should start simple

### Avoid Initially

- Custom workflows
- Sprint planning
- Epics
- Story points
- Dependency graphs

### Exit Criteria

- Issue CRUD works
- Filtering and pagination work
- Assignment is validated
- Authorization applies to every issue endpoint

## Phase 7: Validation And API Contracts

### Objective

Make the API feel professional.

### Build

- FluentValidation for request validation
- Consistent validation error response
- DTOs for requests and responses
- Pagination response shape
- Problem details for errors

### Rules

- Do not expose EF entities as API contracts
- Do not let validation live only in controllers
- Separate request validation from business rules

Example distinction:

- Request validation: `Title` is required
- Business rule: assignee must belong to organization

### Exit Criteria

- Invalid requests return clear errors
- API contracts are stable DTOs
- Business rule failures return meaningful responses

## Phase 8: Testing Foundation

### Objective

Learn how .NET APIs are tested properly.

### Build

- Unit tests for pure business rules
- Integration tests for API endpoints
- Database-backed tests using Testcontainers or a test PostgreSQL database
- Authentication test helpers

### Test First

Prioritize tests for:

- Organization access control
- Role permissions
- Issue assignment rules
- Refresh token rotation
- Pagination and filtering

### Blunt Testing Advice

Do not mock EF Core for serious query behavior. It gives false confidence.

Use integration tests against a real PostgreSQL database for persistence-sensitive behavior.

### Exit Criteria

- Tests run locally
- Access control tests exist
- Main issue workflow is covered
- Database state resets between tests

## Phase 9: Refactor To Clean Boundaries

### Objective

Move from a single pragmatic API project toward better separation.

### When To Do This

Only after these are working:

- Auth
- Organizations
- Projects
- Issues
- Validation
- Tests

### Target Structure

```text
TaskOps.Api
TaskOps.Application
TaskOps.Domain
TaskOps.Infrastructure
TaskOps.Tests
```

### Move

- Entities and domain rules to `Domain`
- Use cases/application services to `Application`
- EF Core and external services to `Infrastructure`
- Controllers, middleware, auth setup, and Swagger to `Api`
- Tests to `Tests`

### Be Careful

Do not create interfaces for everything.

Good abstractions:

- Email sender
- File storage
- Clock/time provider
- Current user context
- Token service

Usually bad abstractions:

- Generic repository over EF Core
- Interface per service by default
- Mapping layer before mapping pain exists

### Exit Criteria

- Dependencies point inward
- API depends on Application
- Application does not depend on API
- Domain does not depend on EF Core
- Tests still pass

## Phase 10: Evolve To Modular Monolith

### Objective

Adopt Solution 3: a modular monolith with clear product boundaries.

### Modules

Create module boundaries around:

- Identity
- Organizations
- Projects
- Issues
- Notifications
- Files
- Dashboard

### Each Module Owns

- Commands/use cases
- Queries
- DTOs
- Validators
- Authorization checks relevant to the module
- Internal domain behavior

### Shared Kernel

Keep shared kernel tiny.

Acceptable shared concepts:

- Entity base type
- Domain event base type
- Result type
- Pagination types
- Clock abstraction
- Current user abstraction

Dangerous shared concepts:

- Giant helper classes
- Shared validation utilities for everything
- Cross-module service soup
- Base repository classes

### Database Strategy

Start with one database and one `DbContext`.

Later, consider module-specific schemas:

```text
identity.Users
org.Organizations
org.OrganizationMembers
project.Projects
issue.Issues
notification.Notifications
```

Do not use separate databases.

### Exit Criteria

- Modules are visible in folder structure
- Cross-module access is deliberate
- Shared code is small
- Feature work mostly stays inside one module

## Phase 11: Comments And Activity Logs

### Objective

Add collaboration history.

### Build

- Add issue comments
- Edit comment
- Delete comment
- List comments
- Activity log for issue changes

### Activity Examples

- Issue created
- Status changed
- Assignee changed
- Priority changed
- Comment added
- Attachment uploaded

### Design Advice

Activity logs are append-only.

Do not treat activity logs as editable business records.

### Exit Criteria

- Issue history is visible
- Important changes create activity records
- Activity records are scoped to organization/project access

## Phase 12: Notifications

### Objective

Add user-facing notifications without sending real email yet.

### Build

- Notification table
- Notification creation on important events
- Mark notification as read
- List unread notifications
- Mock email sender

### Events That Create Notifications

- User assigned to issue
- Issue status changed
- User mentioned in comment
- Comment added to watched issue

### Avoid Initially

- Real email provider
- Push notifications
- Complex preferences

### Exit Criteria

- Notifications are created from domain/application events
- User can read and mark notifications
- Notification logic is not hardcoded inside every controller

## Phase 13: Background Jobs

### Objective

Learn scheduled and asynchronous backend work.

### Add

- Hangfire or Quartz.NET
- Job dashboard protected in development/admin mode
- Due date reminder job
- Notification dispatch job
- Cleanup expired refresh tokens

### DI Lifetime Warning

Background jobs do not run inside a normal HTTP request.

They need their own dependency injection scope. Never casually reuse scoped services from singleton job objects.

### Exit Criteria

- Background jobs run locally
- Jobs can safely access scoped services
- Expired refresh tokens are cleaned up
- Reminder notifications work

## Phase 14: File Attachments

### Objective

Add attachment metadata and storage abstraction.

### Build

- Upload attachment
- Store file metadata
- Download attachment
- Delete attachment
- Local filesystem storage for development
- Storage abstraction for future S3/Azure Blob

### Security Rules

- Validate file size
- Validate file type
- Do not trust uploaded filenames
- Generate server-side storage names
- Enforce organization/project/issue access on download

### Exit Criteria

- Files can be uploaded and downloaded
- Metadata is stored in PostgreSQL
- Storage can later move to cloud without rewriting business logic

## Phase 15: SignalR Real-Time Updates

### Objective

Add real-time collaboration updates.

### Build

- SignalR hub
- Organization/project/issue groups
- Issue updated event
- Comment added event
- Notification created event

### Rules

- Do not trust the client to join any group
- Validate access before adding a connection to a group
- Keep SignalR messages small
- Treat SignalR as a notification channel, not the source of truth

### Exit Criteria

- Connected clients receive issue updates
- Unauthorized users cannot subscribe to private project updates
- API still works without SignalR

## Phase 16: Dashboard And Read Models

### Objective

Build useful analytics without wrecking the write model.

### Build

- Issue counts by status
- Issue counts by priority
- Issues assigned to current user
- Overdue issues
- Recently active issues
- Project activity summary

### Query Advice

Use projection queries.

Do not load entire entity graphs into memory for dashboard cards.

### Later Optimization

If dashboard queries become expensive, introduce read models or cached summaries.

Do not start there.

### Exit Criteria

- Dashboard endpoints are fast enough locally
- Queries are scoped by organization access
- Response DTOs are purpose-built

## Phase 17: Observability And Production Readiness

### Objective

Make the app understandable when something breaks.

### Add

- Serilog structured logging
- Correlation IDs
- Health checks
- Database health check
- Request timing logs
- Rate limiting
- CORS policy
- OpenAPI polish

### Avoid

- Logging secrets
- Logging raw tokens
- Logging huge request bodies by default

### Exit Criteria

- Logs are structured
- Health endpoints work
- API has rate limiting
- Swagger accurately reflects auth and endpoints

## Phase 18: Caching

### Objective

Learn caching where it actually makes sense.

### Add Redis For

- Frequently read organization/project metadata
- Dashboard summaries if needed
- Rate limiting backing store if needed

### Do Not Cache

- Permission checks prematurely
- Highly volatile issue lists
- Anything you cannot invalidate correctly

### Blunt Caching Advice

Bad caching creates security bugs and stale data. Add Redis only after you can explain what is cached, why, and how it is invalidated.

### Exit Criteria

- Redis runs in Docker
- At least one low-risk cache exists
- Cache invalidation is explicit

## Phase 19: Frontend

### Objective

Add a React frontend once the backend is stable.

### Stack

- React
- TypeScript
- TanStack Query
- React Router
- MUI, shadcn/ui, or another component library

### Build Screens

- Login/register
- Organization switcher
- Project list
- Issue list
- Issue detail
- Comments
- Notifications
- Dashboard
- Members/settings

### Frontend Rules

- Use TanStack Query for server state
- Keep auth token handling boring and explicit
- Treat backend authorization as authoritative
- Do not duplicate permission logic deeply in the UI
- Use UI permission checks for visibility only, not security

### Exit Criteria

- User can complete core workflows from browser
- API errors display clearly
- Real-time updates work if SignalR has been added

## Phase 20: CI/CD And Deployment

### Objective

Make the project shippable.

### Add

- GitHub Actions
- Build workflow
- Test workflow
- Docker image build
- Migration strategy
- Environment-specific configuration
- Deployment target

### Deployment Options

Good first choices:

- Azure App Service + Azure Database for PostgreSQL
- Render
- Railway
- Fly.io
- Docker on a VPS

### Exit Criteria

- CI runs on pull requests
- Tests run in CI
- App can be deployed from a clean environment
- Production configuration does not depend on local secrets

## Phase 21: Hardening And Review

### Objective

Review the system like a professional engineer.

### Review Areas

- Authorization coverage
- Query performance
- Migration history
- Error handling
- Logging quality
- Test coverage
- API consistency
- Module boundaries
- Secret handling
- Token expiration behavior
- File upload safety

### Questions To Ask

- Can a user access another organization's data?
- Can a deleted or removed member still use a refresh token?
- Can role changes take effect quickly?
- Are dashboard queries efficient?
- Are background jobs idempotent?
- Are file downloads authorization-protected?
- Can the system be restored from a fresh database and migrations?

### Exit Criteria

- Known security holes are fixed
- Main workflows are tested
- Deployment path is documented
- Architecture matches the actual complexity of the product

## Suggested Milestones

### Milestone 1: Working API

Includes:

- Skeleton API
- PostgreSQL
- EF Core
- Users
- Organizations
- Projects
- Issues

Outcome:

You have a real backend.

### Milestone 2: Secure API

Includes:

- JWT auth
- Refresh tokens
- Organization membership
- Scoped roles
- Access control tests

Outcome:

You have a backend that is not fake-secure.

### Milestone 3: Professional API

Includes:

- Validation
- Error handling
- Pagination
- Filtering
- Integration tests
- Logging

Outcome:

You have something that resembles production API work.

### Milestone 4: Modular Monolith

Includes:

- Clean boundaries
- Application/domain/infrastructure split
- Modules
- Reduced shared-code mess

Outcome:

You evolve toward Solution 3 without starting there prematurely.

### Milestone 5: Advanced Backend

Includes:

- Comments
- Activity logs
- Notifications
- Background jobs
- Attachments
- SignalR

Outcome:

You learn the broader .NET backend ecosystem.

### Milestone 6: Product

Includes:

- React frontend
- Dashboard
- Real-time updates
- Deployment
- CI/CD

Outcome:

TaskOps becomes a complete portfolio-grade application.

## What Not To Do

Do not:

- Start with microservices
- Add MediatR before you understand the request flow
- Add generic repositories over EF Core by default
- Hide every EF query behind an abstraction
- Build a permission system before you have real permissions
- Add Redis before you have a caching problem
- Add SignalR before the normal API works
- Add Hangfire before you have real background work
- Use Clean Architecture as a folder naming exercise
- Return EF entities directly from API endpoints
- Treat frontend permission checks as security

## Recommended Build Order

1. Local setup
2. Skeleton API
3. PostgreSQL + EF Core
4. Users
5. Organizations
6. Organization membership and roles
7. Projects
8. Issues
9. Validation
10. Auth hardening
11. Integration tests
12. Refactor into cleaner boundaries
13. Modular monolith structure
14. Comments
15. Activity logs
16. Notifications
17. Background jobs
18. Attachments
19. SignalR
20. Dashboard
21. Observability
22. Redis where justified
23. React frontend
24. CI/CD
25. Deployment
26. Final hardening

## Final Recommendation

Build TaskOps in three architectural movements:

1. **Pragmatic vertical slice API**
2. **Clean boundary refactor**
3. **Modular monolith**

The most important reason:

> You need working product behavior before architecture has anything honest to organize.

Start simple, but not sloppy. Add structure when the pain becomes real, not when a template tells you to.
