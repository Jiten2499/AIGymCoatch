# Architecture refactor plan

## 1. Must fix now

- Replace minimal API endpoint registrations with thin controllers.
- Preserve every route, verb, request DTO, response body, status code, permission, rate limit, idempotency check, version, and ProblemDetails path.
- Keep handlers, validators, and domain types where they are.
- Remove the old endpoint maps so each route has one implementation.
- Record the controller decision and correct the stale charter sentences.

## 2. Should fix before the next feature

- Split large command files into one folder per use case when that feature is next edited. Do not move them only to match a diagram.
- Add a SQL-backed two-tenant test if a future change touches repository filters.
- Remove the EF Core Design package reference if no design-time factory is required.

## 3. Can defer

- Separate module assemblies.
- Cloud secret provider, blob storage, and an email sender.
- Measured performance targets.
- A tenant-facing audit read API.

## 4. Do not change

- Liquibase changesets `LF-0001` through `LF-0024`.
- Plan definitions and feature-flag codes.
- Personal tenant versus business tenant rules.
- AI provider abstraction, quotas, and safety policy.
- Membership, attendance, and fitness business rules.
- Microservices, Kubernetes, brokers, and event sourcing.

The must-fix items are the only code changes in this pass.
