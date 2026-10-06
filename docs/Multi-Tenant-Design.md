# Multi-tenant design

LifeFit AI separates a person from the workspaces that person can enter.

## User and tenant

A user account is an identity. A tenant is a workspace. They are different records.

Every user has exactly one personal tenant. `Users.TenantId` stores that personal tenant. It does not change when the user joins a gym, studio, or company. Business membership is a row in `TenantMembers` plus tenant-scoped rows in `UserRoles`.

Tenant types are `Personal`, `Gym`, `YogaStudio`, `CrossFit`, `Physio`, and `Corporate`. New business types are additional values, not a new identity model. Tenant status is `Active`, `Suspended`, or `Inactive`.

There is no personal user represented by a null tenant id.

## Personal tenant

Registration creates, in one database transaction:

1. A personal tenant named `{FirstName} Personal`
2. The user, whose `TenantId` is that tenant
3. An active membership
4. The `PersonalOwner` role in that tenant
5. An active Free license and the default feature flags

If any step fails, the transaction rolls back. Registration does not assign `SuperAdmin`.

## Business tenant

`POST /api/v1/tenants` creates a non-personal tenant for the authenticated user. The same transaction creates the membership, assigns `GymOwner` as the owner role, creates a Free license, creates a `BusinessProfiles` row, and writes an audit record. `Tenants.Name` remains the workspace display name. The profile stores legal name, contact, address, logo, time zone, and currency. Personal tenants do not get a business profile.

## Membership

`TenantMembers` is workspace membership. It authorizes a user inside a tenant. It is not a paid gym membership.

`GymMemberships` is the business membership: a member profile, a branch, a plan, a date range, and a status. One member can have many gym memberships over time. Creating a gym membership does not create a user, and creating a tenant membership does not sell a plan.

The business operations model is `docs/Gym-Management.md`.

## Current tenant

The current workspace comes from the `tenant_id` claim in the access token. `ICurrentTenant` is exposed as `ICurrentActor.TenantId`. The API does not accept a caller-supplied tenant id as the current workspace.

When a route contains `tenantId`, authorization checks that the authenticated user is an active member of that tenant and holds the required permission there. Changing the route, query, or body does not grant access.

## Switching

`GET /api/v1/tenants/me` lists workspaces where the caller has an active membership. `POST /api/v1/tenants/switch` issues a new access token and refresh token only after that membership is confirmed and the tenant is active. The new access token's `tenant_id` is the selected workspace.

## Isolation

EF Core applies soft-delete query filters. It does not apply a global tenant filter. `Users.TenantId` means the personal tenant, so a global filter would hide the wrong rows.

Tenant isolation is the default application path:

- The access token supplies the current tenant.
- Membership is checked before a workspace is used.
- `IPermissionService` resolves roles and permissions only inside the requested tenant.
- Repositories do not accept an untrusted tenant id as proof of access.
- Fitness, nutrition, AI conversations, challenges, and notifications are read with the current user id and the current tenant id. A personal workspace and a gym workspace do not share those rows. System exercises and system challenges have a null tenant id and are read-only reference data.

## Roles and permissions

Roles are a catalog. Their effect is the permissions granted to a user inside one tenant through `UserRoles`. The same person can be `GymOwner` in one tenant and `Member` in another. A role assignment never applies to every tenant.

Authorization is permission-based. Endpoint filters and handlers call `IPermissionService` rather than comparing a role name to `Admin`.

## Authorization flow

1. The caller presents a valid access token.
2. The current tenant is the token tenant, or the route tenant when the route names one.
3. The user has an active membership in that tenant.
4. The user's roles in that tenant grant the required permission.
5. When the operation is feature-gated, the tenant is active, the license is usable, and the feature flag is enabled.
