# Operations runbook

## Startup

1. Start SQL Server and wait until its health check passes. `depends_on` only waits for the configured condition.
2. Create the `LifeFitAI` database if it does not exist.
3. Run Liquibase validate, then update. The database image does this through `migrate.sh`.
4. Start Redis only when caching is enabled. The API starts when Redis is disabled.
5. Start the API with `ASPNETCORE_ENVIRONMENT`, the SQL connection string, and a JWT signing key of at least 32 bytes.
6. Call `/health/live` and `/health/ready`. Ready must be 200 before traffic is sent.

## Health and logs

- Live means the process is accepting requests.
- Ready means SQL Server answered the EF health check. Redis is not part of readiness.
- Logs are compact JSON on the console and under `logs/lifefit-*.json`.
- Search by `CorrelationId`, then `TenantId` and `UserId`. Do not expect passwords, tokens, or API keys in the log.

## Common failures

| Symptom | What to check |
| --- | --- |
| Process exits at startup | Production connection string, JWT key length, or empty `Security:AllowedOrigins` |
| Ready is unhealthy | SQL Server, connection string, and whether Liquibase finished |
| 503 on every API call | `Security:MaintenanceMode` |
| 403 workspace is suspended | Tenant status. Authentication and tenant switch remain available so the user can leave that workspace. |
| 429 | Rate-limit configuration for global, auth, AI, or check-in |
| AI 503 | Provider key, timeout, or provider outage. Other fitness routes stay available. |
| 409 | Concurrency row version, unique value, or a repeated idempotency key |

## Rollback

Restore the previous application image. Roll back a Liquibase changeset only when its rollback block is safe and a backup exists. Prefer a forward fix for data that was already written.

## Backup and restore

Follow `docs/Disaster-Recovery.md`. Take a backup before a schema update. After a restore, run readiness and a tenant-scoped smoke test.

## Incident investigation

1. Record the correlation id from the response header `X-Correlation-ID`.
2. Find that id in the API log.
3. Confirm the tenant id on the request came from the token, not the body.
4. For a suspected cross-tenant read, compare the token tenant with the row tenant. Handlers load by the token tenant.

## Tenant isolation incident

Disable the affected route with maintenance mode if the leak is ongoing. Preserve logs and audit rows. Identify the route id that was requested and the tenant id in the token. Do not copy row contents into a ticket if they contain personal data. Fix the query so it filters by the authenticated tenant, then add a regression test.

## AI provider outage

Confirm AI routes return the controlled unavailable result. Leave workout, nutrition, and attendance routes in service. Do not raise the retry count above the single connection retry.

## Database outage

Readiness goes unhealthy. Stop sending traffic. Restore or fail over using the backup plan, then run Liquibase status before opening the API.
