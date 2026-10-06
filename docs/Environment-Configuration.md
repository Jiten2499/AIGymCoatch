# Environment configuration

LifeFit AI reads configuration from `appsettings.json`, environment-specific appsettings, environment variables, and the host secret store. The committed `backend/src/Api/appsettings.json` contains empty secret fields. `backend/src/Api/appsettings.example.json` shows the shape with placeholders. Do not commit real values.

| Environment | Behavior |
| --- | --- |
| Development | Local CORS origins `http://localhost:3000` and `http://localhost:8081`. Swagger is enabled. Docker Compose sets this environment. |
| Testing | Used by automated host tests. Production origin checks are not applied. |
| Staging | Treated as non-development. CORS uses `Security:AllowedOrigins`. |
| Production | Startup fails when the SQL connection string is missing, the JWT signing key is shorter than 32 bytes, or `Security:AllowedOrigins` is empty. |

## Required in production

- `ConnectionStrings:SqlServer`
- `Jwt:SigningKey`, `Jwt:Issuer`, `Jwt:Audience`
- `Security:AllowedOrigins`

AI, Redis, email, and storage credentials are optional. Missing AI credentials make AI calls return a controlled unavailable result. Redis stays off unless `Redis:Enabled` is true and a connection string is present.

## Options

| Section | Purpose |
| --- | --- |
| `Jwt` | Issuer, audience, lifetime, clock skew of 30 seconds, signing key |
| `ConnectionStrings:SqlServer` | Runtime SQL access. Liquibase uses its own connection. |
| `Redis` | Optional cache. Default `Enabled` is false. |
| `Ai` | Provider URL, model, timeout, user quota, tenant quota |
| `Storage` | Local root path. Files stay private and tenant-scoped. |
| `Email` | SMTP host, port, username, password, from address. No mail sender is wired yet. |
| `RateLimit` | Global, authentication, AI, and check-in windows |
| `Security` | CORS origins, maintenance mode, request size, SQL command timeout |
| `Observability` | OpenTelemetry switch and optional OTLP endpoint |

`ISecretProvider` (`EnvironmentSecretProvider`) reads configuration and returns null for blank values. It does not log the value. A cloud provider can replace it later without changing callers.

Sensitive values must come from environment variables, container secrets, or a cloud secret manager: database passwords, JWT signing keys, refresh tokens, AI keys, Redis credentials, SMTP credentials, storage credentials, encryption keys, and webhook secrets.

## API version

Routes use `/api/v1`. A breaking change needs a new version. The current version stays until callers are moved. Deprecation is announced before a version is removed. There is no second version in this release.

## Cache

| Key | TTL | Scope | Invalidation | Failure |
| --- | --- | --- | --- | --- |
| `tenant:{tenantId}:permissions:{userId}` | 45 seconds | One user in one tenant | After the role change commits | Memory cache is process-local. Redis, when enabled, is shared by the processes that use it. |
| `tenant:{tenantId}:features:{code}` | 60 seconds | One feature in one tenant | Feature enable or disable | Same fallback |
| `tenant:{tenantId}:status` | 30 seconds | Tenant status | After tenant suspension commits | Memory cache is process-local. Redis, when enabled, deletes the same key for every process that uses it. |

Permission-cache invalidation runs only after `SaveChanges` commits, or after the unit-of-work transaction commits when the change is inside a transaction. A failed save leaves the existing cache in place. If the commit succeeds and the cache delete fails, the failure is logged and the 45-second TTL is the fallback. `RolePermissions` are seeded by Liquibase and are not changed by the application, so there is no runtime path that edits a role's permission list. The in-memory cache is local to one process. With Redis enabled, a delete reaches every process using that Redis database. The TTL remains 45 seconds.
