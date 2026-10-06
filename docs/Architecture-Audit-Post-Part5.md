# Architecture audit after Part 5

This audit describes the repository as it existed before the controller standardization in the same change. The source of truth is the code, not the original part prompts.

## A. Current architecture

LifeFit AI is a modular monolith. HTTP enters the API host, MediatR dispatches a command or query, a handler applies domain rules, and Infrastructure talks to SQL Server through EF Core. Liquibase owns the schema. There is one deployable API. There is no message broker, Kubernetes cluster, or service split.

## B. Actual project structure

| Path | Role |
| --- | --- |
| `backend/src/Api` | HTTP host, versioning, Swagger, middleware |
| `backend/src/Application` | Commands, queries, handlers, validators, ports |
| `backend/src/Domain` | Identity, gym, and fitness entities |
| `backend/src/Infrastructure` | EF Core, SQL Server, AI provider, cache, jobs |
| `backend/src/SharedKernel` | `Result`, `Error`, entity contracts |
| `backend/src/Modules` | Not present. Features were added under Application instead |
| `backend/tests/LifeFitAI.Application.Tests` | Unit, host, architecture, and tenant-isolation tests |
| `database/` | Liquibase changelogs. No .NET project |

## C. Current API approach

Before this standardization, every product route was a minimal API in `backend/src/Api/Endpoints`. There were no controllers. The endpoint methods were thin: they mapped a request DTO to a MediatR request and returned `ToHttpResult()`. Routes use `/api/v1/...`.

## D. Current CQRS approach

MediatR 12.5.0 is the dispatcher. FluentValidation runs in the pipeline. Handlers live next to their commands in area folders such as `Application/Gym/Members` and `Application/Fitness/Ai`. Several files hold more than one use case (`GymLists.cs`, `MembershipWrites.cs`, `AiCommands.cs`). That is organization, not a second write model.

## E. Current module boundaries

Boundaries are namespaces and folders, not separate assemblies:

- Identity, Tenancy, Authorization, Licensing
- Gym: business, branches, members, staff, trainers, memberships, attendance
- Fitness: profile, exercises, workouts, nutrition, challenges, notifications, AI

`TenantMember` and `GymMembership` remain different types. System exercises use a null tenant id for the shared catalog. Personal users still have a personal tenant.

## F. Current dependency direction

Api references Application, Infrastructure, and SharedKernel. Application references Domain and SharedKernel. Infrastructure references Application, Domain, and SharedKernel. Domain references only SharedKernel. `database/` references no .NET project. The architecture test checks this.

## G. Current database architecture

SQL Server is the database. EF Core maps tables and does not call `Database.Migrate()`. No `Migrations` folder exists. Liquibase master changelog includes changesets through `LF-0024`. Queries use projections and paging. No `Include()` calls were found. Row versions and soft-delete filters are already on the entities that use them.

## H. Current authentication and authorization

JWT validates issuer, audience, lifetime, and signing key, with 30 seconds of clock skew. Refresh tokens are stored as hashes. Passwords use PBKDF2. `RequiresPermission` checks the authenticated user and either the route tenant, for workspace administration, or the token tenant. Handlers then call `IOperationGuard` or `IPermissionService`, which requires an active membership. Gym and fitness reads also pass the token tenant into repositories.

## I. Current multi-tenant architecture

A user can belong to a personal tenant and to business tenants. Switching tenants is a command that checks membership. Handlers do not treat a null tenant id as "personal." Tests cover tenant A against tenant B for members, attendance, workouts, nutrition, AI conversations, cache keys, and AI context.

## J. Current licensing architecture

`ILicenseService` and `IFeatureFlagService` are the feature gate. `OperationGuard` calls them. Plan rules live in `PlanCatalog`. Business handlers do not compare `plan == "Pro"`.

## K. Current AI architecture

`IAiProvider` is implemented by `OpenAiCompatibleAiProvider`. The API key comes from configuration and is empty in source. Quota, rate limit, safety screening, context DTOs, and output checks stay in the application. The provider allows one connection retry and does not retry timeouts.

## L. Current production infrastructure

Configuration fails closed in production. CORS uses an explicit origin list. Rate limits, security headers, correlation ids, maintenance mode, suspended-tenant blocking, optional Redis, OpenTelemetry, health checks, and an in-process token cleanup job are present.

## M. Testing architecture

One xUnit project covers domain rules, authorization, tenant isolation, AI quota and safety, architecture references, health, correlation, maintenance, and authentication rate limits. Host tests use `WebApplicationFactory` with a placeholder database password. They do not load production data.

## N. Problems found

| Issue | Risk | Notes |
| --- | --- | --- |
| HTTP adapters were minimal APIs while the requested standard is controllers | Medium | The adapters were already thin. The mismatch was structural, not a behavior bug. |
| `docs/API-Standards.md` and `docs/Coding-Standards.md` were absent | Low | Charter and AGENTS.md carried the rules instead. |
| Charter and `docs/architecture.md` still described Part 1 as if later parts did not exist | Low | Documentation drift. |
| Application folders are not one folder per command | Low | Cosmetic. Moving them would not change behavior. |
| `Modules` projects were never created | Low | Slices live in Application. Splitting assemblies is not required. |
| Permission cache could be refilled before commit | Low | Invalidation now runs after commit. The 45-second TTL remains the fallback. |
| EF Core Design packages are referenced for tooling | Low | No migration is generated or applied. |

## O. Architecture inconsistencies

The charter said a future endpoint would be minimal, while this audit's target is a controller. Both styles dispatched MediatR. The empty Modules path in the charter does not match the Application folders that actually hold features.

## P. Naming inconsistencies

Endpoint files were named `IdentityEndpoints.cs` and similar, which hid the action. Some application files group several commands. Public routes, database names, and MediatR type names were already explicit and were left unchanged.

## Q. Technical debt

See `docs/Technical-Debt.md`. This audit adds the deferred slice-folder move and the fact that host tests do not boot two real SQL tenants.

## R. Risk levels

Critical: none found in this pass. High: none that were still open after Part 5. Medium: the HTTP style mismatch, now addressed by controllers. Low: documentation drift, folder layout, cache TTL, and the Design package.

## S. Recommended remediation

Move the HTTP adapters to controllers without moving handlers. Keep `ToHttpResult` status mapping. Do not bind commands directly from the body, because the existing request DTOs avoid mass assignment. Do not restructure handler files in this pass.

## T. Issues that should not be changed

License plan matrix, tenant semantics, system-catalog null tenant ids, membership and attendance rules, AI product behavior, Liquibase history, and the Result status codes. A controller that returns `Ok(result)` or `BadRequest(result)` would wrap the Result and drop 401, 403, 404, 409, 429, and 503. That shape was not adopted.
