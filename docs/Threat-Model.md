# Threat model

| Threat | Attack vector | Impact | Mitigation | Remaining risk |
| --- | --- | --- | --- | --- |
| Authentication bypass | Stolen or forged JWT, weak password, replayed refresh token | Account takeover | PBKDF2, password policy, issuer, audience, lifetime, hashed rotating refresh tokens | A stolen active access token works until it expires |
| Authorization bypass | Missing permission or role check | Privilege escalation | Permission service, operation guard, `PLATFORM_ADMIN` only on SuperAdmin | Frontend checks are not a control |
| Tenant isolation failure | Route id, body, or query tenant from another workspace | Cross-tenant read or write | Handlers load by the token tenant and user | A new query that forgets the tenant filter |
| IDOR | Guessed member, session, or conversation id | Data exposure | Lookup includes tenant and, for fitness, user | Identifiers are still GUIDs, not a control by themselves |
| API abuse | Login or AI floods | Denial of service and AI cost | Configured rate limits and quotas | Limits are per user or IP, not a global edge network |
| Prompt injection | User text that asks the model to ignore instructions or leak data | Unsafe advice or data leakage | Safety policy, context DTO, no email or phone in the prompt | The model can still answer poorly inside the allowed topic |
| Data leakage | Logs, errors, prompts, cache | Secret or personal data exposure | Structured logs without bodies, generic database errors, tenant cache keys | Development can still include a non-database exception message |
| File path abuse | `../`, absolute paths, remote URLs | Local file access | `IFileStorage` and body-scan reference checks | Storage is local validation, not a cloud ACL yet |
| Secret theft | Keys in source, images, or logs | Full compromise | Environment configuration, empty committed secrets, non-root image | Operators can still inject secrets into the wrong log line |
| SQL Server exposure | Stolen connection string | Database compromise | Secret store, no connection string in errors | Network access to SQL must still be restricted by the host |
| Redis exposure | Shared cache key | Cross-tenant cache read | Keys start with `tenant:{tenantId}` | Redis is optional and should not be exposed publicly |
| Background job replay | Duplicate cleanup or check-in | Extra writes | Token cleanup is idempotent. Check-in accepts an idempotency key | Other commands do not require an idempotency key |
| QR replay | Reused check-in token | False attendance | Expiring hashed QR tokens, tenant and branch checks, check-in rate limit | A token is valid until it expires |
| SuperAdmin abuse | Stolen platform admin token | Tenant suspension or license changes | SuperAdmin role plus `PLATFORM_ADMIN`, and an audit row | There is no separate admin UI or step-up authentication |
