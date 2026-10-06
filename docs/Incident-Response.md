# Incident response

## Flow

Detect, triage, contain, investigate, recover, verify, document, and prevent recurrence.

## Severity

| Level | Meaning |
| --- | --- |
| P1 Critical | Active cross-tenant data exposure, authentication bypass, or full platform outage |
| P2 High | A major feature is down, or a single tenant is unavailable |
| P3 Medium | Degraded performance or a failing non-critical dependency such as AI or email |
| P4 Low | Cosmetic or documented limitation with a workaround |

## Cross-tenant procedure

1. Treat the report as P1 until the scope is known.
2. Enable maintenance mode if requests are still returning another tenant's data.
3. Capture the correlation id, route, token tenant, and time. Do not attach personal data to the incident note.
4. Check audit rows for the same correlation id. Audit rows must not contain passwords, hashes, tokens, or API keys.
5. Patch the handler or query so the server tenant is the only tenant used.
6. Add or extend the tenant isolation test.
7. Verify with two tenants that the original route returns not found or an empty page for the other tenant.
8. Write the cause and the fix in the incident record.

## After the incident

Confirm health checks, a normal authenticated call, and the isolation test. Record what will stop the same failure, such as a query filter or a missing permission check.
