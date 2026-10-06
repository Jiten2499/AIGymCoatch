# API standards

LifeFit AI exposes version 1 at `/api/v1`. A breaking change needs a new version. The current version stays until callers move.

## Request flow

```text
Controller
    -> Command or query
    -> Handler
    -> Domain and application services
    -> Infrastructure
    -> EF Core
    -> SQL Server
```

Controllers live in `backend/src/Api/Controllers`. They send one MediatR request and return the existing result mapping. They do not contain business rules, queries, or permission decisions beyond the `RequiresPermission` filter.

Request bodies bind to request DTOs in `LifeFitAI.Api.Contracts`. Those DTOs are mapped to commands in the action. Domain entities are not bound from the client, and the Result object is not returned as the success body. Success returns the value or no content. Failures use ProblemDetails with the status already defined for that error code.

## Cross-cutting behavior

Authentication is JWT. Authorization is the permission filter plus the handler's tenant, license, and feature checks. `X-Correlation-ID` is accepted or generated for every request. Authentication, AI, and check-in use named rate limits. Check-in accepts `Idempotency-Key`.

Anonymous platform info stays at `GET /api/v1/platform`. Health stays at `/health/live` and `/health/ready`.
