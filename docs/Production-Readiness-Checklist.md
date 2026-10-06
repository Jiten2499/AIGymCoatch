# Production readiness checklist

## Application

- Release build succeeds.
- Unit, integration, and architecture tests succeed.
- Production configuration is present and startup fails when it is not.
- Serilog writes structured logs with correlation id, and without request bodies.
- Unexpected errors return ProblemDetails. Database failures do not return SQL text.

## Security

- Passwords are hashed with PBKDF2 and a per-password salt. The policy requires length 12 and mixed character classes.
- JWT checks issuer, audience, lifetime, and signing key. Clock skew is 30 seconds.
- Refresh tokens are stored hashed, rotated, and revocable.
- Authorization uses the authenticated user, the current tenant, permission, resource lookup by that tenant, license, and feature flag.
- Tenant isolation tests cover member, attendance, workout, nutrition, AI conversation, audit writes, cache keys, and AI context.
- Secrets are not committed. Production CORS uses an explicit origin list.
- Rate limits cover the API, authentication, AI, and check-in.
- Request body, header, and line size limits are set from configuration.

## Database

- Liquibase owns schema changes. There are no EF Core migrations.
- `LF-0024` adds audit correlation, a tenant AI usage index, idempotency records, and `PLATFORM_ADMIN`.
- Row version columns remain on the records that already use them. Conflicts return 409.
- Soft-deleted rows stay out of normal queries.
- Backup and restore are documented in `docs/Disaster-Recovery.md`.

## Infrastructure

- The API image is multi-stage and runs as a non-root user.
- Compose starts SQL Server, Liquibase, optional Redis, and the API. The API does not wait on Redis.
- `/health/live` checks the process. `/health/ready` checks SQL Server.
- Background cleanup of expired and old revoked refresh tokens runs in-process.

## Observability

- Logs include correlation id and, when authenticated, user and tenant.
- The `LifeFitAI` meter records AI requests, rate-limit rejections, job executions, and authentication failures. Labels stay low-cardinality.
- Traces cover ASP.NET Core, outbound HTTP, and EF Core when observability is enabled.
- OTLP export happens only when `Observability:OtlpEndpoint` is set.

## Operations

- Use `docs/Operations-Runbook.md`, `docs/Incident-Response.md`, and `docs/Disaster-Recovery.md`.
- Maintenance mode returns 503 for normal APIs and leaves `/health` available.
- A suspended tenant is blocked with 403 except health, authentication, tenant switching, platform administration, and Swagger.
