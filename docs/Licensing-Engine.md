# Licensing engine

Licensing is tenant-scoped. A personal user has a real personal tenant and a license on that tenant. `TenantId` is never null.

## Plans

Plans live in `Plans`. Codes are stable database values. The seeded codes are `PERSONAL_FREE`, `BUSINESS_FREE`, `BASIC`, `PRO`, and `ENTERPRISE`. SuperAdmin can add more through `POST /api/v1/platform/plans`. Status is `Draft`, `Active`, `Inactive`, or `Retired`. A referenced plan is retired or deactivated. It is not deleted.

`AppliesTo` is `Personal`, `Business`, or `Any`. Personal Free and Business Free are different rows, so a feature can be on for personal workspaces and off for gym workspaces without a tenant-type branch in registration.

`IPlanCatalog` remains only as a fallback when a database plan is not present, which keeps older tests and an unseeded database from failing closed to the previous Free snapshot.

## Features

`FeatureDefinitions` is the catalog. Existing product codes stay tenant-scoped and do not consume a seat. A later feature can be marked `User` scope. That scope requires an active `UserLicenseAssignments` row in addition to the plan or override.

`PlanFeatures` stores whether a plan enables a code. Changing a plan feature does not rewrite every tenant flag row. Live checks read the plan when `TenantLicenses.PlanDefinitionId` is set.

`PlanLimits` stores numeric limits such as `AIRequestsPerPeriod`. Feature enablement and usage limits are separate. An enabled AI feature still uses the existing AI quota.

## Tenant licenses

`TenantLicenses` remains the tenant license. Part 6 adds `PlanDefinitionId`, `BaseCapacity`, `Notes`, `RevokedAt`, and `RevokedBy`. Status adds `Scheduled` and `Revoked`. `Cancelled` stays.

A license is usable only when the tenant is `Active`, the license status is `Active`, the row is not deleted, and `ExpiresAt` is null or still in the future. Authorization checks those dates. A background job is not required for correctness.

## Capacity

Effective capacity is `BaseCapacity` plus extra grants that are `Active`, started, and not expired. Available seats are that total minus active assignments. Grants are separate rows, so SuperAdmin can add or revoke extra seats without rewriting the plan.

Business administrators with `LICENSE_MANAGE` assign and revoke seats in the current tenant. They cannot create capacity, change the plan, extend expiry, or grant an override. Personal owners do not receive that permission.

## Overrides

`LicenseOverrides` grants or removes one feature for one tenant. An effective override wins over the plan feature and the stored flag. It does not change the plan.

## Effective feature resolution

`IFeatureFlagService.IsEnabledAsync` resolves a feature in this order:

1. Tenant status and license usability, including expiry.
2. An effective SuperAdmin override, when one exists.
3. The active plan feature, when the license points at a plan.
4. The stored `FeatureFlags` snapshot, when the license has no plan.
5. An active user assignment, only when the feature definition scope is `User`.

AI quota is enforced after the feature is allowed.

## Cache

Feature results use `tenant:{tenantId}:features:{code}` for 60 seconds. Plan, license, assignment, and override changes schedule those keys and delete them only after the unit of work commits. A failed save discards the pending delete. A cache delete failure is logged and does not undo the database commit. The TTL is the fallback.

## Concurrency

User assignment runs inside the unit-of-work transaction. It takes `UPDLOCK, HOLDLOCK` on that tenant license row, reloads capacity, counts active assignments, and inserts only when a seat remains. The lock is held until commit, so two API processes cannot both take the last seat. The filtered unique index still stops a second active seat for the same user. Cache invalidation stays after commit.

## Audit

License mutations write the existing `AuditLogs` row with the operator, tenant, action, entity, and old and new values. Actions include plan creation, plan feature changes, license extension and revocation, extra capacity, user assignment and revocation, and overrides.

## Authorization

Platform plan, expiry, capacity, and override routes require the SuperAdmin role and `PLATFORM_ADMIN`. Tenant assignment routes use the authenticated tenant and `LICENSE_MANAGE`. A tenant id in the body is not used. The target user must be an active member of that tenant. Another tenant's license returns not found.

## Personal and business Free

Registration assigns `PERSONAL_FREE`. The seeded plan enables `WORKOUT_PLANS`, `AI_COACH`, `AI_WORKOUT_GENERATION`, and `AI_PROGRESS_INSIGHTS`. Business workspace creation assigns `BUSINESS_FREE`, which enables none of those features. Registration does not choose the feature list.

## Administration

SuperAdmin manages plans, plan features, stored plan limits, license expiry, revocation, extra capacity, user seats, and feature overrides from the web dashboard. The same handler rules apply. There is no route that creates a second tenant license or switches an existing license to another plan. `GET /api/v1/platform/licenses` is paged. `GET /api/v1/platform/licenses/{id}` returns one license with capacity totals. Platform seat assignment checks membership of the target workspace and then calls `ReserveSeatAsync`. A full pool returns HTTP 409 with `license.capacity_conflict`.

An LLM connection and a feature license are separate. The connection answers whether the platform can call a provider. The license answers whether that tenant or user may use the AI feature. `AI_COACH` and the related AI feature codes still come from `FeatureDefinitions`. Plan limit codes are stored numbers. Seat capacity is the limit the assignment path enforces.

## LLM connections

`LlmConnections` and `LlmModels` are Liquibase changeset `018-llm-connections.xml`. The protected key never leaves the server. One non-deleted default is allowed. Chat selects that default through `IAiProvider`. If unprotect fails or no default exists, `AiOptions` is the fallback. The executable provider is OpenAI-compatible chat. Model discovery does not insert rows.
