# Security standards

## Passwords

Passwords are hashed with PBKDF2-HMAC-SHA256 and a random salt. The hash and salt are stored separately. The iteration count, salt size, and hash size come from the `Password` configuration section. The default iteration count is 210000.

The API does not store plaintext passwords and does not use MD5, SHA-1, or a single unsalted SHA-256 hash. Verification uses a fixed-time comparison. `IPasswordPolicy` enforces minimum length and uppercase, lowercase, digit, and special-character rules. Those rules are configuration, not scattered constants.

## Access tokens

Access tokens are JWTs signed with HMAC-SHA256. The signing key is `Jwt:SigningKey`, supplied by the environment as `Jwt__SigningKey`. Startup rejects a missing key or a key shorter than 32 bytes. The key is not committed.

Claims are `sub` (user id), `tenant_id`, and `jti`, plus the standard issuer, audience, and expiry claims. Inbound claim mapping is disabled so `sub` stays `sub`. Access tokens do not carry passwords, roles for every tenant, or business payloads.

## Refresh tokens

A refresh token is 32 random bytes, encoded for transport. The database stores only the SHA-256 hex hash. Rotation works as follows:

1. Hash the presented token and load that row.
2. Reject an unknown token.
3. If the row is already revoked, revoke every active refresh token for that user and reject the request.
4. If the row is expired but not revoked, reject it and leave the rest of the family active.
5. Otherwise revoke the presented token, set `ReplacedByTokenId` to the new token id, and issue a new access token and refresh token.

Logout revokes the presented refresh token. Password change and password reset revoke active refresh tokens for that user.

## Password reset

`POST /api/v1/auth/forgot-password` always returns success. When the account is active, the API stores a hashed reset token and calls `IPasswordResetNotifier`. The notifier does not send email in Part 2 and does not log the token. `POST /api/v1/auth/reset-password` accepts the raw token once, changes the password, and does not echo the token.

## Secret management

Connection strings, the SQL password, and the JWT signing key come from the environment or user secrets. `appsettings.json` keeps an empty signing key. `.env` is gitignored. Seed data contains roles and permissions only. There is no seeded administrator password.

LLM API keys are not configuration files and are not returned by the API. `DataProtectionLlmSecretProtector` encrypts the key with ASP.NET Core Data Protection before `LlmConnections.ProtectedApiKey` is stored. The purpose string is `LifeFitAI.LlmConnection.ApiKey`. The application name is `LifeFitAI`. Docker persists the key ring on the `dataprotection` volume. Production must back up that ring, or share it across instances, before an operator relies on stored connections. Data Protection here is not a cloud KMS. A failed unprotect does not log the ciphertext or the key. Logs, audit payloads, ProblemDetails, and Swagger examples omit the secret. The browser sends a key only on create or when the operator checks Replace API key. The web session keeps the access token in module memory, not `localStorage`.

## LLM connection operations

Create, update, disable, delete, secret replacement, connection test, default change, and model refresh write `AuditLogs` with the action name and connection name. The secret is not a column in that row. Deleting or disabling the current default is rejected until another default is chosen. The test calls the provider `GET /models` with the stored key and returns success, a safe failure message, latency, and model ids. Provider bodies and authorization headers are not returned.

## Logging

Request logs record the operation name, status, and correlation id. They do not record passwords, password hashes, salts, refresh tokens, reset tokens, or the signing key. Validation and server errors use ProblemDetails. Production error details do not include exception messages.

## Authentication and authorization

Anonymous operations are register, login, refresh, logout, forgot-password, and reset-password. Other identity and tenant routes require a bearer token. Permission checks use `IPermissionService` with the user, the tenant, and a permission code. `SuperAdmin` is never assigned by registration or by the workspace role API.

## Tenant isolation

The current tenant is the access-token claim. A route or body tenant id is accepted only after membership and permission checks succeed. Soft-delete filters hide deleted rows. There is no global tenant query filter, because the user row's tenant id is the personal workspace rather than the selected workspace.
