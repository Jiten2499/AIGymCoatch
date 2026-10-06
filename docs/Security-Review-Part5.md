# Security review — Part 5

| Finding | Severity | Affected area | Current behavior | Recommended fix | Status |
| --- | --- | --- | --- | --- | --- |
| Production could start without CORS origins | High | API | Startup now fails in production when `Security:AllowedOrigins` is empty. Credentials are not combined with any-origin. | Keep the fail-fast check. | Fixed |
| Client-supplied tenant id | High | Handlers | Member, attendance, workout, nutrition, and AI reads use the token tenant. Tests cover tenant A against tenant B ids. | Review every new query for the same filter. | Fixed for current handlers |
| Cache cross-tenant read | High | Permissions and features | Keys include the tenant id. A status cached for tenant B is not returned for tenant A. | Keep the key helper. Invalidate on role and feature changes. | Fixed |
| AI context leakage | High | Coach prompt | The prompt uses the fitness context DTO. It does not include email, phone, or address. | Keep explicit context records. | Fixed |
| SQL errors in responses | Medium | Exceptions | Unique and other database update failures return a generic message. The log records the exception type. | Keep database exception text out of production responses. | Fixed |
| Refresh token growth | Medium | Authentication | A hosted job deletes expired tokens and revoked tokens older than 7 days. | Monitor the job counter. | Fixed |
| QR replay | Medium | Check-in | Tokens expire and are stored hashed. Check-in is rate limited and can take an idempotency key. | Keep the expiry short. | Mitigated |
| Permission cache race | Low | Authorization | Invalidation now runs after commit. A failed cache delete still falls back to the 45-second TTL. In-memory cache is not shared across processes. | Use Redis when more than one API process serves traffic. | Mitigated |
| Tenant status cache race | Low | Suspension | Status invalidation now runs after commit. A failed cache delete falls back to the 30-second TTL. In-memory cache is not shared across processes. | Use Redis when more than one API process serves traffic. | Mitigated |
| Development exception messages | Low | Errors | Non-database exceptions can include `exception.Message` in Development. Production uses a generic detail. | Leave development detail for local debugging. | Accepted |
| No cloud secret provider | Low | Configuration | `EnvironmentSecretProvider` reads configuration. It does not call a cloud API. | Add a provider when a secret manager is chosen. | Accepted |
| Email password is configuration only | Low | Email | The value is bound and not logged. No SMTP client sends it. | Do not log the options object. | Accepted |
| Audit read API | Low | Audit | Tenants have no audit list route. Writes store the acting tenant and correlation id, not secrets. | Add a tenant-scoped read later if operators need it. | Accepted |
| Member hard delete | Info | Members | There is no member delete route. Updates load the member by the current tenant, so a foreign id cannot be changed. | Keep updates tenant-scoped. | Accepted |
| Pagination | Info | Lists | Page size outside 1–100 becomes 20. The API does not return 400 for that case, so existing clients stay compatible. | Document the clamp. | Accepted |
