# LifeFit AI database

This directory is the database project. It is a separate boundary from the .NET backend. Liquibase applies everything here. The API never runs schema migrations.

## Purpose

The database project owns the SQL Server schema and the migration history:

- Tables, columns, primary keys, foreign keys, unique constraints, and check constraints
- Indexes
- Views, stored procedures, and functions
- Seed and reference data
- Database-specific configuration

The backend owns runtime data access through EF Core. It does not own schema creation.

## Why Liquibase

Liquibase is the source of truth for database changes. A changeset is reviewed, applied in order, and recorded in `DATABASECHANGELOG`. EF Core maps tables after a changeset has created them. Do not create EF Core migrations, and do not call `Database.Migrate()` or any equivalent at application startup.

## Layout

```text
database/
├── changelog/
│   ├── db.changelog-master.yaml
│   └── changes/
├── procedures/
├── views/
├── functions/
├── seed/
├── tests/
├── liquibase.properties.example
├── Dockerfile
└── README.md
```

`db.changelog-master.yaml` includes the files under `changelog/changes/`. Changeset ids `LF-0001` and `LF-0002` are the Part 1 foundation. They create the `app` schema and no business tables.

## Install Liquibase

Use Liquibase 4.31, the same version as `database/Dockerfile`.

- Docker: `docker build -t lifefit-liquibase ./database`
- Local CLI: install Liquibase 4.31 and the SQL Server JDBC driver, then run commands from the `database/` directory

The .NET SDK is not required to run Liquibase. Liquibase is not required to build the backend.

## Local SQL Server connection

Connection settings are for migrations. The API uses `ConnectionStrings__SqlServer` separately, for runtime data access.

Copy the example and fill it in locally. Do not commit that file.

```powershell
Copy-Item database\liquibase.properties.example database\liquibase.properties
```

Set `username` and `password` in `database/liquibase.properties`, or export them instead of writing a properties file:

```powershell
$env:LIQUIBASE_COMMAND_URL = "jdbc:sqlserver://localhost:1433;databaseName=LifeFitAI;encrypt=false;trustServerCertificate=true"
$env:LIQUIBASE_COMMAND_USERNAME = "sa"
$env:LIQUIBASE_COMMAND_PASSWORD = "<local-password>"
$env:LIQUIBASE_COMMAND_CHANGELOG_FILE = "changelog/db.changelog-master.yaml"
```

The `LifeFitAI` database must already exist. Docker Compose creates it before Liquibase runs. For a manual SQL Server install, create the database first.

From the repository root, with Docker:

```powershell
Copy-Item .env.example .env
docker compose up --build sqlserver database-init liquibase
```

Set `MSSQL_SA_PASSWORD` in `.env` before that command. `.env` is gitignored.

## Commands

Run these from `database/` when using a local Liquibase CLI and `liquibase.properties`:

```powershell
liquibase validate
liquibase status
liquibase update
```

| Command | What it does |
| --- | --- |
| `validate` | Checks the changelog for structural errors |
| `status` | Lists changesets that are not yet in `DATABASECHANGELOG` |
| `update` | Applies pending changesets |

The Docker image runs `validate`, then `update`, then exits. The API container starts only after that exit succeeds.

## Pending changes

`liquibase status` prints changesets that have not been applied. `liquibase status --verbose` includes the changeset path and author. Review that list before `update`.

## Rollback

Every changeset has a `<rollback>` block. Roll back one changeset only when that rollback was reviewed:

```powershell
liquibase rollback-count 1
```

Do not edit a changeset that has already been applied. Add a new changeset that changes the schema forward. Use rollback for a local mistake or an explicit, tested undo.

## Changeset conventions

- One logical change per changeset.
- File name: `changelog/changes/NNN-short-name.xml` or `.yaml`, numbered after the latest file.
- `id`: `LF-####`, the same ticket id as the comment.
- `author`: the person or role that owns the change.
- Comment: ticket id, what changed, index review, and performance note.
- Include the new file from `db.changelog-master.yaml`.
- Do not add an empty changeset to fill a number.

## Index governance

Do not index every column. Add an index only when a changeset creates or changes a table and the index serves a known need:

- A `WHERE` clause, `JOIN`, or `ORDER BY` the application will run
- A foreign key, so parent deletes and joins do not scan the child
- A uniqueness rule
- A composite pattern whose leading columns match the filter and sort

Review in this order: primary key, foreign keys, unique indexes, composite indexes, filtered indexes. State why each index exists in the changeset comment. Skip the index when no query or constraint needs it. The full standard is [Database standards](../docs/Database-Standards.md).

## Foreign keys

Name a foreign key `FK_Table_ReferencedTable`. Use `ON DELETE NO ACTION`. Index the foreign-key column. Name that index `IX_Table_Column`.

## Naming

| Object | Pattern | Example |
| --- | --- | --- |
| Schema | application objects in `app` | `app` |
| Table and column | PascalCase | `TenantId` |
| Primary key | `Id` `UNIQUEIDENTIFIER`, constraint `PK_TableName` | `PK_Members` |
| Foreign key | `FK_Table_ReferencedTable` | `FK_Members_Tenants` |
| Index | `IX_Table_Column` | `IX_Members_TenantId` |
| Unique index | `UX_Table_Columns` | `UX_Members_TenantId_Email` |
| Procedure | `usp_Name` | `usp_CloseAttendance` |
| View | `vw_Name` | `vw_ActiveMembers` |
| Function | `fn_Name` | `fn_MemberAge` |

## Add a database change

1. Add a changeset under `database/changelog/changes/`.
2. Include the index review and a rollback.
3. Include the file from `database/changelog/db.changelog-master.yaml`.
4. Put procedures, views, functions, and seed data in their directories and reference them from the changeset.
5. Run `liquibase validate`, then `liquibase status`, then `liquibase update` against a local database.
6. If the application reads or writes the new shape, update the EF Core mapping in Infrastructure. Do not add an EF Core migration.

## Review

A database change is reviewed like application code:

- The changeset is forward-only if it may already be applied.
- Rollback exists and matches the change.
- Names follow the standard.
- The index review names the query or constraint for every new index.
- No secret, password, or environment-specific connection string is committed.
- EF Core migrations were not added.

## EF Core migrations

Do not run `dotnet ef migrations add`. Do not commit a `Migrations` folder. Schema changes belong only in this directory.
