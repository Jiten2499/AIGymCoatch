# LifeFit AI agent guide

LifeFit AI is a multi-tenant fitness platform. Part 1 is the backend foundation. Part 2 adds identity, personal and business tenants, RBAC, licensing, and feature flags. Part 3 adds gym and business operations. Part 4 adds the member fitness experience: profiles, goals, exercises, workouts, measurements, nutrition, challenges, a member dashboard, and an AI coach behind a provider abstraction. Part 5 adds production operations: configuration, tenant-safe caching, rate limits, optional Redis, health checks, and in-process jobs. Payments, delivery notifications, and Part 6 stay out of scope.

## Tech stack

- .NET 9, ASP.NET Core controllers, API versioning, Swagger, ProblemDetails
- MediatR 12.5.0 and FluentValidation. MediatR stays on 12.x because later major versions are commercially licensed. FluentAssertions stays on 7.x for the same reason.
- SQL Server 2022, EF Core as the runtime mapper, Liquibase as the schema source of truth
- Serilog with console and rolling compact JSON files
- Docker Compose and GitHub Actions

## Architecture

Clean Architecture, vertical slices, and CQRS.

| Layer | Project | Responsibility |
| --- | --- | --- |
| Host | `backend/src/Api` | HTTP, versioning, Swagger, Serilog, correlation ids, health, ProblemDetails |
| Slices | `backend/src/Modules/*` | Empty until a feature is requested |
| Application | `backend/src/Application` | MediatR pipeline and FluentValidation |
| Domain | `backend/src/Domain` | Identity, tenant, licensing, gym business, and fitness entities |
| Shared kernel | `backend/src/SharedKernel` | `BaseEntity`, `Result`, `Error`, audit and soft-delete contracts |
| Infrastructure | `backend/src/Infrastructure` | EF Core mappings, SQL Server, password hashing, and token issuance |

Allowed references: Api → Application, Infrastructure, SharedKernel. Application → Domain, SharedKernel. Infrastructure → Application, Domain, SharedKernel. No cycles.

## Coding standards

- Controllers contain no business logic. An action sends one MediatR request.
- A future feature owns its command or query, handler, validator, endpoint, and mapping.
- Use dependency injection. Use `async`/`await` for I/O and pass `CancellationToken`.
- Return `ProblemDetails` for failures. Use `Result` and `Error` inside the application.
- Keep files under 300 lines. Do not leave TODOs.
- Log with Serilog templates. Do not log secrets or request bodies.
- Connection strings and the SQL Server password come from the environment. Do not hardcode them.

## Database separation

Liquibase is the sole source of truth for database schema changes.

Never create EF Core migrations.

Never modify the database schema from application startup.

Database changes belong exclusively under `/database`.

.NET projects may define EF Core entities and mappings but must not own schema migrations.

The backend connection string is for runtime data access. Liquibase has its own connection settings for migrations. Liquibase does not reference a .NET project, and the backend build does not require Liquibase.

When implementing a feature that changes the database:

1. Identify required schema changes.
2. Create or update the appropriate Liquibase changeset under `/database`.
3. Review indexes and constraints.
4. Update EF Core mappings if required.
5. Add or update application tests.
6. Validate that Liquibase remains the database source of truth.

## Database standards

- Liquibase changelogs under `database/changelog` own the schema. The master file is `database/changelog/db.changelog-master.yaml`.
- `uniqueidentifier` keys, `datetime2` UTC values, `rowversion`, soft delete, and audit columns.
- PascalCase tables and columns. Primary keys use `PK_TableName`. Foreign keys use `FK_Table_ReferencedTable`. Indexes use `IX_Table_Column`. Procedures use `usp_Name`. Views use `vw_Name`. Functions use `fn_Name`.
- Every changeset includes a ticket id, author, rollback, index review, and performance note.
- Review indexes in this order: primary key, foreign keys, unique indexes, composite indexes, filtered indexes, then the reason each index exists.
- Do not create business tables or speculative indexes in Part 1. The full standard is `docs/Database-Standards.md`. Identity and licensing tables belong to Part 2 changesets `003` through `005`. Gym operations tables belong to Part 3 changesets `006` through `010`.

## Identity and tenant rules

- Never create EF Core migrations.
- Database schema changes belong under `/database`.
- Liquibase is the schema source of truth.
- Never bypass tenant authorization.
- Never trust a client-provided tenant id without membership validation.
- Never store raw refresh tokens.
- Never log passwords, password hashes, salts, refresh tokens, reset tokens, or signing keys.
- Never assign SuperAdmin during normal registration or through the workspace role API.
- Do not expose EF entities directly from APIs.
- Do not duplicate authorization or licensing decisions across endpoints. Call `IPermissionService`, `ILicenseService`/`IFeatureFlagService`, and `IPlanCatalog`.

## Testing standards

- xUnit and FluentAssertions. Mock ports with NSubstitute when a test needs a double.
- Run `dotnet test backend/LifeFitAI.sln` before committing backend changes.
- Identity, tenancy, permission, and licensing tests belong with Part 2. Gym operations tests belong with Part 3. Fitness, nutrition, challenge, and AI tests belong with Part 4. Production, tenant-isolation, and architecture tests belong with Part 5. Do not add payment tests until payments are requested.

## Gym operations rules

- Liquibase owns schema changes. Never create EF migrations.
- Never bypass tenant isolation. Resolve every business record with the current tenant and the entity id.
- Never trust a client-supplied tenant id.
- Never create a second user account for a gym membership. Reuse the existing user when the email already exists.
- `TenantMember` is workspace authorization. `GymMembership` is the paid business membership. Do not replace one with the other.
- Authorize with permissions. Do not hardcode role checks in handlers.
- Gate business operations with the license and feature flag through `IOperationGuard`.
- Do not expose EF entities from APIs.
- Keep membership, attendance, and onboarding rules in the domain and application handlers.
- Do not implement payments or push delivery. Part 5 operational controls are already in place.
- `StaffProfiles.BranchId` and `TrainerProfiles.BranchId` are the primary branch only. A later assignment table can add multi-branch staff.

## Git standards

- Conventional Commits.
- Build and test the touched area before committing.
- Do not commit `.env`, logs, or real passwords.
- Commit only when the user asks.

## Member experience rules

- Liquibase owns schema changes. Never create EF migrations.
- Never bypass tenant isolation. Fitness rows are loaded with the current user and the current tenant.
- Never expose sensitive user data to an AI provider beyond the controlled context.
- Never hardcode AI provider secrets. The API key comes from configuration.
- Never trust raw AI output as domain data. Validate structured output before persistence.
- AI providers stay behind `IAiProvider`. Do not expose system prompts.
- Respect license and feature gating through `IOperationGuard`.
- Part 5 operational controls are in place. Do not start Part 6.
- Do not duplicate Users, Tenants, Members, or GymMemberships for fitness data.
