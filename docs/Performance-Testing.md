# Performance testing

No service level agreement is stated here. Record p50, p95, and p99 from a real workload before publishing a target.

## Suggested scenarios

| Scenario | What to measure |
| --- | --- |
| Authentication | Login and refresh latency, and the rate-limit response under a small burst |
| Member search | Page size at the maximum of 100, filtered by the current tenant |
| Attendance | A paged history for one branch |
| Dashboard | Business dashboard and member dashboard query time |
| Workout history | Paged sessions for one user and tenant |
| AI | Time to the provider, timeout behavior, and quota rejection |

## Method

Run against a staging database restored from a sanitized backup. Keep SQL command timeout at the configured 30 seconds. Watch SQL CPU, memory, connections, and slow queries while the test runs. Do not add an index from a single plan without the review in `docs/Database-Standards.md`.

## Database signals

CPU, memory, disk, database size, transaction log, connections, slow queries, deadlocks, lock waits, index fragmentation, and Liquibase migration failures. Use the host's database tooling. This application does not include a monitoring dashboard.
