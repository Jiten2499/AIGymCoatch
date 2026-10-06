# Decision log

## LLM connections and secret protection

Chat uses the existing `IAiProvider`. `DatabaseLlmRuntime` loads the one active default row in `LlmConnections`, unprotects the key, and passes that endpoint and model into `OpenAiCompatibleAiProvider`. If no default row can be unprotected, the provider falls back to `AiOptions` so a deployment that still uses configuration keeps working. The only executable provider type is `OpenAiCompatible`. Groq and OpenAI are that type when the base URL is their OpenAI-compatible endpoint. Anthropic and Azure OpenAI are not implemented.

The API key is protected with ASP.NET Core Data Protection (`IDataProtectionProvider.CreateProtector("LifeFitAI.LlmConnection.ApiKey")`). The host calls `AddDataProtection().SetApplicationName("LifeFitAI")`. Docker persists the key ring at `/var/lifefit/dataprotection`. A missing ring still cannot unprotect ciphertext written by another ring. That is not a cloud key vault. Unprotect failure returns null and the call falls back to configuration or fails closed. GET responses expose `apiKeyConfigured` only. Audit text for a replacement is `LLM connection secret updated`.

One default is enforced by clearing other defaults in the handler and by filtered unique index `UX_LlmConnections_IsDefault`. The default cannot be disabled or deleted. Model refresh lists provider ids and does not insert them. Chat uses the connection default model. When that value is empty, `AiOptions.Model` is `openai/gpt-oss-120b`, because Groq retired `llama-3.3-70b-versatile`. Workout generation asks that model for low reasoning effort and applies a JSON object wrapped in extra text. Exercise ids that are not in the catalog are still rejected.

## SuperAdmin licensing screens

The web app at `web/` is the platform administration UI. It calls the existing plan, feature, license, capacity, assignment, override, and expiring routes. A license is still created with the workspace. The UI does not issue a second license or change the plan. Platform seat assignment reuses `ReserveSeatAsync`. `GET /api/v1/platform/licenses/{id}` reads one license so the detail screen is not limited to the first page. Plan limit rows are stored values. They are not a second quota engine. Seat capacity remains the enforced limit.

## Dynamic licensing

Plans, features, plan features, limits, extra capacity, user assignments, and overrides are database rows. Personal registration assigns `PERSONAL_FREE`. Business workspace creation assigns `BUSINESS_FREE`. The plan row decides the features. Existing licenses are backfilled onto the matching plan. Billing is not part of this change. Seat assignment locks the tenant license row with `UPDLOCK, HOLDLOCK` inside the unit-of-work transaction and recounts seats before insert. A process-local lock is not used. The unique active-assignment index still stops a second seat for the same user.

## Coach, challenges, notifications, and settings

The mobile coach home lists `GET /ai/conversations` and continues a thread with `GET /ai/conversations/{id}` plus `POST /ai/coach/chat`. The chat response is one completed message. The client appends that message locally and does not reload the thread after every send. Message pages are oldest-first and the detail response omits `totalCount`, so the thread requests the first 100 messages. Quick prompts are localized drafts. They are not sent until the member presses send. Token counts are not shown as a quota.

Challenges and notifications use `WORKOUT_PLANS` from `GET /features`. There is no separate challenge or notification flag. The app lists `GET /challenges`, joins with `POST /challenges/{id}/join`, and records `POST /challenges/{id}/progress`. There is no challenge-by-id, leave, or leaderboard route. Detail reuses the list page that opened it. Notifications are `GET /notifications` and `POST /notifications/{id}/read`. There is no unread-count, mark-all, delete, or destination payload, so the app does not show a badge or invent a deep link.

Appearance, accent, and language stay in the device preference store. Hindi and Nepali catalogs still fall back to English. Logout uses the existing session clear.

## Nutrition screens

The mobile nutrition journey uses `GET/PUT /nutrition/profile`, `GET/POST /nutrition/logs`, `GET /nutrition/summary`, and `POST /ai/nutrition/advice`. Access follows `NUTRITION` and `AI_NUTRITION` from `GET /features`. Personal Free does not enable those features. There is no nutrition entry endpoint by id, no update or delete, and no multi-day trend endpoint. The history list shows the fields returned on each log. A zero calorie target means the summary had no saved profile.

## Member fitness screens

The mobile member journey reads workout plans, days, exercises, sessions, sets, measurements, personal records, and AI progress insight from the existing `/api/v1` contracts. Feature availability comes from `GET /features` for the current workspace. The session payload does not include a previous-performance comparison, so the detail screen shows only the recorded sets. Set logging still has no server idempotency; the log button is disabled only while that request is in flight.

## Personal fitness journey

Personal registration stays on the personal Free plan. The plan enables the workout and AI features used by the fitness profile, goals, workout sessions, and AI coach. Business Free stays disabled for those features.

## Mobile foundation

The Expo app in `mobile/` is the React Native foundation. It uses TypeScript, React Navigation, TanStack Query, Zustand for client preferences and session, and a local design system. User appearance and accent choices stay on the device until a preference API exists. Tenant logos use `BusinessProfile.LogoUrl` when a later screen is allowed to read it. Hindi and Nepali catalogs fall back to reviewed English copy.

## Tenant status cache

Tenant suspension schedules `tenant:{tenantId}:status` and deletes it only after the unit of work commits. A failed save leaves the cached status in place. The 30-second TTL stays as the fallback. There is no runtime activation or deactivation command. `Tenant.Suspend` is the only status change.

## Permission cache

Role assignment and role removal schedule a permission-cache delete and run it only after the database commit. The key is `tenant:{tenantId}:permissions:{userId}`. The 45-second TTL stays as the fallback when a write bypasses the application or the cache delete fails. `RolePermissions` stay immutable after Liquibase seed. The default memory cache is process-local; Redis is the shared cache when it is enabled.

## Post-Part-5 architecture

LifeFit AI uses a modular monolith with Clean Architecture, vertical slice architecture, CQRS/MediatR, and conventional ASP.NET Core controllers.

The HTTP adapter was moved from minimal API endpoint classes to controllers. Each action still sends one command or query and returns the existing status mapping. Handlers were not moved or rewritten. Microservices are not introduced: one database, one process, and in-process jobs are enough for the current product, and a service split would add failure modes without a boundary that the code needs.

## Part 5

| Decision | Reason |
| --- | --- |
| The modular monolith stays | Production readiness does not require a service split. |
| Redis is optional and off by default | The API must run with SQL Server alone. Cache failures fall back to memory. |
| Cache keys include the tenant id | A shared cache must not return another tenant's permissions, features, or status. |
| OpenTelemetry is the instrumentation foundation | Logs stay on Serilog. Metrics and traces use the `LifeFitAI` meter and ASP.NET, HTTP, and EF instrumentation. Export is optional. |
| Liquibase remains the schema source | `LF-0024` adds operational tables and indexes. No EF migration was added. |
| AI stays behind `IAiProvider` | Workout, nutrition, and coach code do not call a vendor SDK. One connection retry is allowed. Timeouts are not retried. |
| Background work is in-process | Refresh-token cleanup needs retry, cancellation, and logging. A broker was not required. |
| Production configuration fails closed | Missing connection string, JWT key, or CORS origins stops production startup. |
| Security model is unchanged | Authentication, current tenant, permission, ownership, license, and feature flag remain the checks. `PLATFORM_ADMIN` is granted only to SuperAdmin. |
