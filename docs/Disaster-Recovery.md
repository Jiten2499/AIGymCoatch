# Disaster recovery

This document describes the recovery approach. It does not claim a measured recovery point objective or recovery time objective. Those values are valid only after restore tests on the real environment.

## Backups

- Full backup of `LifeFitAI` on a schedule owned by the SQL Server operator.
- Differential backups between full backups.
- Transaction log backups often enough for the point-in-time need of that environment.
- Keep backups in a separate account or region from the database server.
- Retain full, differential, and log backups according to the operator's compliance window. This repository does not delete them.

## Restore

1. Restore the latest full backup.
2. Restore the latest differential, if one exists.
3. Restore transaction logs to the chosen point in time.
4. Run the application health checks against the restored database.
5. Record the result. A backup that has not been restored is an untested backup.

## Deployment order

Backup, then Liquibase update, then verification, then application deployment, then a smoke test of `/health/live`, `/health/ready`, and an authenticated call. Schema changes should expand first, deploy a compatible application, migrate data, switch behavior, and contract only in a later planned change. Changeset `LF-0024` is expand-only.

## Retention

Do not automatically delete audit logs, attendance, or business records. Suggested starting points, to be confirmed before any purge job is added:

| Data | Consideration |
| --- | --- |
| Audit logs | Keep for the compliance window. Do not store passwords, hashes, tokens, or API keys in them. |
| AI conversations and usage | Tenant-scoped. Retain with the membership record unless a documented privacy request says otherwise. |
| Attendance | Business record. Do not purge casually. |
| Notifications | Short operational history is enough once delivery exists. |
| Application logs | Rolling files keep 14 days in the API container. |
| Soft-deleted rows | Remain excluded from normal queries. Physical deletion needs a documented rule. |
| Refresh tokens | The cleanup job removes expired tokens and revoked tokens older than 7 days. Active sessions are left in place. |

## Dependency criticality

| Dependency | If it fails |
| --- | --- |
| SQL Server | Readiness fails. The API cannot serve tenant data. |
| Redis | Optional. The process continues with the memory cache when Redis cannot be used. |
| AI provider | Fitness features continue. AI endpoints return a controlled failure. |
| Email | Configuration only. Core requests do not depend on SMTP. |
| File storage | The affected file operation fails. Other APIs continue. |
