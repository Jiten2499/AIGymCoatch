# Roadmap

| Part | Scope | Status |
| --- | --- | --- |
| 1 | Foundation and architecture | Complete |
| 2 | Identity, multi-tenant, and licensing | Complete |
| 3 | Gym management core | Complete |
| 4 | AI and member experience | Complete |
| 5 | Production readiness, security, scaling, and operations | Complete |
| 6 | Dynamic licensing and SuperAdmin license management | Complete |
| 6 UI | SuperAdmin web dashboard, LLM connections, and licensing administration | Complete |

Part 7 has not been started. Payment and subscription billing are not part of Part 6.

The mobile member experience now includes workout day and exercise detail, active set logging, workout summary, server-paged history, session detail, measurements, personal records, and an AI progress insight. Nutrition adds the daily summary, profile, meal log, paged history, and AI nutrition guidance behind the existing feature flags. Coach conversations, challenge join and progress, notification read state, and device-local settings use the existing member APIs. SuperAdmin configures LLM connections, plans, features, licenses, seats, and overrides in the web dashboard. The member app still reads `GET /features` and the existing AI routes.

Out of scope until a later request: payments, social features, live classes, video, wearables, computer vision, medical diagnosis, medication recommendations, marketplace, e-commerce, microservices, Kubernetes, Kafka, RabbitMQ, event sourcing, and a data warehouse.
