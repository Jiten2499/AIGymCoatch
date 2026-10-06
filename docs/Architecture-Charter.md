# Architecture charter

LifeFit AI Part 1 keeps the .NET host and the database project as separate boundaries in one repository.

```text
┌─────────────────────────────┐
│          Backend            │
│                             │
│ ASP.NET Core                │
│ EF Core                     │
│ MediatR                     │
│ Application logic           │
└──────────────┬──────────────┘
               │
               │ Runtime data access
               ▼
┌─────────────────────────────┐
│        SQL Server           │
└──────────────▲──────────────┘
               │
               │ Schema management
               │
┌──────────────┴──────────────┐
│          Liquibase          │
│                             │
│ /database                   │
│ changelogs                  │
│ indexes                     │
│ constraints                 │
│ procedures                  │
│ views                       │
└─────────────────────────────┘
```

## Responsibilities

Liquibase owns schema management. Changesets under `/database` create and change tables, constraints, indexes, views, procedures, functions, and seed data.

EF Core owns application data access. The `DbContext` maps entities, runs queries and commands, and participates in transactions. It does not create the production schema.

The API connects with `ConnectionStrings:SqlServer`. Liquibase connects with its own URL, username, and password. Those configurations stay separate. The API does not start Liquibase, and it does not call `Database.Migrate()`.

## Backend shape

| Layer | Project | Responsibility |
| --- | --- | --- |
| Host | `backend/src/Api` | Controllers, versioning, Swagger, Serilog, correlation ids, health, ProblemDetails |
| Application | `backend/src/Application` | Commands, queries, handlers, validators, and ports |
| Domain | `backend/src/Domain` | Identity, tenancy, licensing, and gym business entities |
| Shared kernel | `backend/src/SharedKernel` | `BaseEntity`, `Result`, `Error`, audit and soft-delete contracts |
| Infrastructure | `backend/src/Infrastructure` | EF Core `DbContext` and SQL Server data access |

Allowed references: Api → Application, Infrastructure, SharedKernel. Application → Domain, SharedKernel. Infrastructure → Application, Domain, SharedKernel. No project references the `database/` directory.

## Database change for a feature

When a feature changes the database:

1. Identify the required schema changes.
2. Create or update the Liquibase changeset under `/database`.
3. Review indexes and constraints.
4. Update EF Core mappings if the application reads or writes the new shape.
5. Add or update application tests.
6. Confirm Liquibase is still the only schema source of truth.

## LLM provider

`IAiProvider` remains the only AI boundary. `OpenAiCompatibleAiProvider` asks `ILlmRuntime` for the active default connection, then calls that OpenAI-compatible chat endpoint. Configuration in `AiOptions` is the fallback when no usable default connection exists. Credentials are protected by `ILlmSecretProtector` and are not part of the HTTP contract. Schema for `LlmConnections` and `LlmModels` is Liquibase changeset `018-llm-connections.xml`.

## Deferred

Parts 1–6 are present: identity, gym operations, the member fitness experience, production controls, dynamic licensing, and the SuperAdmin web dashboard. Payments and notification delivery stay out of scope. The business model is documented in `docs/Gym-Management.md`. The member model is documented in `docs/Fitness-Domain.md` and `docs/AI-Architecture.md`. HTTP adapters are controllers, documented in `docs/API-Standards.md`.
