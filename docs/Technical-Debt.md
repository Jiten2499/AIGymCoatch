# Technical debt

## Security

- The in-memory permission cache is process-local. Another API instance does not see the delete unless Redis is enabled.
- Development responses can include a non-database exception message.
- SuperAdmin has no step-up authentication beyond the role and `PLATFORM_ADMIN` permission.

## Performance

- Dashboard and history queries were reviewed for tenant filters and paging. They are not covered by a measured load test.
- The tenant AI usage index is new. Confirm its plan on a production-sized table before changing quotas.

## Architecture

- Redis, S3, Azure Blob, and a cloud secret manager are extension points. Only memory cache, local path checks, and environment secrets are implemented.
- LLM API keys use ASP.NET Core Data Protection. Docker stores the ring on the `dataprotection` volume. The ring is not a cloud KMS. A lost volume cannot unprotect existing connections.
- The unique default-connection index is the backstop. Two simultaneous default writes can still surface a database conflict that is not mapped to a friendly error.
- Plan limit rows are stored. The runtime does not enforce those codes as quotas. Seat capacity is the enforced limit.
- There is no API to issue a new tenant license or to change an existing license to another plan.
- Email options exist. A sender is not implemented.
- Background work is an in-process hosted service. There is no broker.

## Maintainability

- The platform directories omit `@example.com` accounts created by local API checks. A platform administrator sees every remaining account and workspace. A workspace administrator sees that workspace and its members. The shared admin grid sorts, filters, and pages the rows it loaded.
- Idempotency is applied to check-in only. Workout set logging disables the button while the request is in flight, but the API does not deduplicate a repeated set.
- Workout session detail does not include a previous-performance payload, so the mobile screen does not invent a comparison.
- There is no server pause, resume, or rest-timer state. Rest seconds on a plan exercise are displayed as a target only.
- `GET /workout-sessions` returns each session with an empty exercise list. The history screen shows the date, status, and duration from that payload. Sets are loaded from `GET /workout-sessions/{id}`. Neither payload includes the plan name or a previous-set comparison.
- The measurement form records weight only. Other returned measurement fields are displayed, and they are sent as null on create.
- Nutrition logs have no get-by-id, update, or delete route. Meal history renders the paged list payload. `GET /nutrition/summary` is one date, so the app does not draw a multi-day chart. Creating a log has no idempotency key; the save button is disabled only while that request is in flight.
- `PERSONAL_FREE` does not enable `NUTRITION` or `AI_NUTRITION`. The mobile app does not turn those flags on.
- Challenge list has no get-by-id, leave, or leaderboard route. Detail reads the same paged list. Progress is the server `currentValue` and `targetValue`. Join and progress have no idempotency key; the button is disabled only while that request is in flight.
- `GET /ai/conversations/{id}` returns messages oldest-first and drops the page total. The thread screen requests 100 messages and does not invent later pages.
- Notifications have no unread-count, mark-all, delete, or navigation payload. The app does not show an unread badge or a deep link.
- Challenges and notifications are authorized with `WORKOUT_PLANS`, not a separate feature code.
- Appearance, accent, and language have no user-preference API. Notification delivery toggles are not available.

## Licensing

- Seat assignment is serialized by a SQL Server update lock on the tenant license row. It was proven with two independent database contexts, not with two API containers.
- Expiring-license lookup loads plan codes one license at a time.
- The web admin session lives in module memory. A reload requires sign-in again. Tokens are not stored in `localStorage`.
- The mobile app shows the tenant license pool to GymOwner and SuperAdmin. Platform administration is the web dashboard.
- `LICENSE_MANAGE` reaches an existing GymOwner after the permission cache TTL or the next role change.

## Testing

- Tenant isolation tests use handler doubles plus the memory cache. They do not boot two real tenants in SQL Server.
- The GitHub Actions workflow is in the repository and was not executed from this session.

## Operations

- RPO and RTO are not measured.
- Restore testing is an operator procedure, not an automated job.
