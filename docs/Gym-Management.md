# Gym management

Part 3 adds business operations on top of the Part 2 identity model. The same tables serve a gym, yoga studio, CrossFit box, physiotherapy clinic, or corporate wellness workspace. The tenant type names the business. The operations do not fork by type.

## Three identities

| Concept | Table | Meaning |
| --- | --- | --- |
| User | `Users` | Login identity. One account per email. |
| Workspace membership | `TenantMembers` | The user may act in this tenant. Roles and permissions hang off this relationship. |
| Gym membership | `GymMemberships` | A paid plan for a member profile, with dates, status, and history. |

A trainer is a user, a tenant membership, the Trainer role, a staff profile, and a trainer profile. A member is a user, a tenant membership, the Member role, a member profile, and one or more gym memberships. Part 3 does not create a second user table.

## Business profile and branches

`Tenants.Name` is the workspace display name. `BusinessProfiles` is one optional row per business tenant and stores legal name, contact, address, logo, time zone, and currency. Creating a business tenant also creates the profile with time zone `UTC` and currency `USD`. Personal tenants do not get a profile.

A branch belongs to one tenant. `Code` is unique per tenant among live rows, stored uppercase, and is not changed by update. Delete sets the branch inactive. It does not free the code and does not remove history.

`StaffProfiles.BranchId` and `TrainerProfiles.BranchId` are the primary branch. They are not a multi-branch assignment. A later table can assign one person to many branches. A member is tied to a branch through `GymMemberships.BranchId`.

## People

Onboarding an existing email adds that user to the current tenant. It does not create another account and does not change the password. A new email creates the personal tenant, user, PersonalOwner role, and Free license, then adds the business membership. Public APIs do not answer "does this email exist?" as their own operation.

Staff types are Trainer, Receptionist, Manager, and Other. Onboarding accepts the first three and assigns Trainer, Receptionist, or GymManager. Trainer onboarding also creates a trainer profile. Removing staff soft-deletes the staff and trainer profiles and removes tenant roles. It does not delete the user. The workspace owner cannot be removed this way.

Member profiles store contact and demographic fields. They do not store medical information.

## Plans and gym memberships

`MembershipPlans` hold duration, type, optional visit cap, price, and currency. Price is informational. Part 3 does not collect payment.

A gym membership is Pending when the start date is in the future and Active otherwise. Later transitions:

- Renew from Active, Expired, or Frozen. Cancelled, Suspended, and Pending do not renew.
- Freeze an Active membership that has not ended.
- Unfreeze returns Active, or Expired when the end date has passed.
- Cancel from any status except Cancelled or Expired.
- Expire when the end date has passed, including a pending membership past its end.

Each change appends `MembershipHistory`. `AuditLogs` still record the security event. History is not a soft-deleted table and is not removed with the membership.

## Attendance and QR

Check-in requires an active member, an active branch in the current tenant, and a membership that is Active and covers today. Frozen, expired, pending, cancelled, and suspended memberships cannot check in. A plan visit cap counts existing attendance rows. One open visit per member is enforced by a filtered unique index. Another visit the same day is allowed after checkout.

The caller may check in another member only when they also hold `MEMBER_READ`. A member can check in and check out their own profile. A trainer does not receive check-in permission.

QR check-in stores only a hash of a random token. The token lives for `Gym:QrTokenMinutes` (default 10, clamped to 1–60), is single use, and can be revoked. The payload is not a password, profile, or membership. A token from another tenant, an expired token, a used token, or an unknown hash fails as an invalid token.

## Authorization and licensing

Handlers call `IOperationGuard`, which checks the access token, the permission, and the feature flag. Endpoints also require the permission. New feature codes are `BUSINESS_PROFILE`, `BRANCH_MANAGEMENT`, `MEMBER_MANAGEMENT`, `STAFF_MANAGEMENT`, `MEMBERSHIP_MANAGEMENT`, `ATTENDANCE`, and `BUSINESS_REPORTS`. `QR_CHECKIN` already existed. Free leaves every flag disabled. Basic enables profile, members, memberships, and attendance. Pro adds branches, staff, reports, and QR check-in.

Permissions are seeded in changeset `010` and granted through `RolePermissions`. Handlers do not compare role names.

## Dashboard and search

`GET /api/v1/dashboard` counts active members, new members this month, active and expired memberships, memberships expiring within 14 days, today's check-ins, month check-ins, active trainers, and active branches. Counts run in SQL. An optional branch id limits members, memberships, visits, trainers, and branches to that branch. The business profile time zone defines "today."

Member search matches a prefix of member code, name, email, phone, or membership number. Collection endpoints use page and page size. Page size outside 1–100 falls back to 20. Sort fields are an allow-list.

## Tenant isolation

Every read and write loads the row with the token tenant id and the id from the route or body. A branch, member, plan, membership, or attendance id from another tenant is not found. QR validation checks the token tenant before it creates attendance.
