# Database standards

Liquibase, under `/database`, is the source of truth for the SQL Server schema. EF Core maps tables after a changeset creates them. Do not add an EF Core migration for a schema change.

The target database is SQL Server 2022. Objects live in the `app` schema.

## Types

| Concern | SQL Server type |
| --- | --- |
| Primary key | `UNIQUEIDENTIFIER`, column name `Id` |
| Tenant foreign key | `UNIQUEIDENTIFIER`, column name `TenantId` |
| User foreign key | `UNIQUEIDENTIFIER`, column name `UserId` |
| Instants | `DATETIME2` stored in UTC |
| Concurrency | `ROWVERSION` |
| Soft delete | `DeletedAt DATETIME2 NULL` |
| Audit | `CreatedAt`, `UpdatedAt`, `CreatedBy`, `UpdatedBy` |

Every business table inherits that shape from `BaseEntity`.

## Names

| Object | Pattern | Example |
| --- | --- | --- |
| Table | PascalCase | `Memberships` |
| Column | PascalCase | `TenantId` |
| Primary key | `PK_TableName` | `PK_Memberships` |
| Foreign key | `FK_Table_ReferencedTable` | `FK_Memberships_Tenants` |
| Index | `IX_Table_Column` | `IX_Users_TenantId` |
| Unique index | `UX_Table_Columns` | `UX_Users_TenantId_Email` |
| Procedure | `usp_Name` | `usp_CloseAttendance` |
| View | `vw_Name` | `vw_ActiveMembers` |
| Function | `fn_Name` | `fn_MemberAge` |

Foreign keys use `ON DELETE NO ACTION`.

## Changesets

Changelogs live in `database/changelog`. The master file is `database/changelog/db.changelog-master.yaml`. Individual changes live in `database/changelog/changes/`.

Every changeset includes:

- Ticket id in the Liquibase `id` and in the comment
- Author
- Rollback
- Index review
- Performance note

A changeset that creates no table says so in the index review. Do not add an index "for later." Do not edit a changeset that has already been applied. Add a new changeset.

## Index governance

Do not create an index on every column. Add one only when it serves a `WHERE` clause, a `JOIN`, an `ORDER BY`, a foreign key, a uniqueness rule, or a composite query the application will run.

Review indexes in this order whenever a changeset creates or changes a table:

1. Primary key. One clustered key on `Id` unless a measured workload proves a different clustered key.
2. Foreign-key indexes. SQL Server does not create these automatically. Add one index per foreign key so deletes and joins on the parent do not scan the child.
3. Unique indexes. Enforce natural uniqueness, and filter `DeletedAt IS NULL` when soft-deleted rows may repeat the same key.
4. Composite indexes. Add one only for a query whose leading columns match the filter and sort. Column order follows equality predicates, then range, then sort.
5. Filtered indexes. Use them when a predicate such as `DeletedAt IS NULL` or `IsActive = 1` selects the rows the workload actually reads.
6. Explain why each index exists in the changeset comment. Name the query or constraint it serves. If an index does not serve a known query or constraint, do not create it.

Avoid duplicate and overlapping indexes. If a composite index already serves a left-prefix lookup, do not add a second index on that prefix alone.

Part 1 creates the `app` schema and no tables, so it creates no indexes.

Part 3 changesets `006` through `010` add business profiles, branches, staff, trainer, and member profiles, membership plans, gym memberships, membership history, attendance, QR check-in tokens, and the gym permission seed. Attendance history is queried by tenant, branch, and member plus check-in time. There is no separate attendance index on `GymMembershipId`: member history starts from `MemberProfileId`, and the filtered open-visit unique index covers the active row. `StaffProfiles.BranchId` and `TrainerProfiles.BranchId` are a single primary branch. Part 4 changesets `011` through `015` add exercises, fitness profiles, goals, measurements, body-scan metadata, workout programs and sessions, nutrition, AI conversations and usage, challenges, notifications, and the fitness permission seed. History tables such as measurements, sets, nutrition logs, AI messages, and usage records are append-only. A null `Exercises.TenantId` or `Challenges.TenantId` is shared reference data. User fitness rows always store both `UserId` and `TenantId`.

## Ownership

| Concern | Owner |
| --- | --- |
| Tables, constraints, indexes, views, procedures, functions, seed data | `/database` through Liquibase |
| `DbContext`, mappings, queries, commands, transactions | .NET Infrastructure |
| Runtime connection string | API configuration |
| Migration connection | Liquibase configuration |

The backend build does not require Liquibase. Liquibase does not reference a `.csproj` or a .NET assembly.
