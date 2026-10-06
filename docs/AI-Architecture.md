# AI architecture

The application does not call a vendor SDK. Handlers depend on `IAiProvider`. Infrastructure registers one OpenAI-compatible HTTP provider. Groq, OpenAI, and Azure OpenAI are selected by `Ai:BaseUrl`, `Ai:Model`, and `Ai:Provider`. The API key is `Ai:ApiKey`, normally supplied as `Ai__ApiKey`. `appsettings.json` keeps the key empty. The key is not logged and is not returned by the API.

## Request path

1. Authenticate and read the current tenant from the access token.
2. `IOperationGuard` checks the permission and the feature flag.
3. `IAiQuotaService` counts `AiUsageRecords` for the user and tenant since the UTC day and month. The default limit is 20 requests per day and 200 per month. Zero means blocked.
4. `IAiSafetyService` screens the user text.
5. `IAiContextBuilder` builds a small context.
6. A versioned prompt is added as a system message. That text is not stored and is not returned.
7. The provider is called with a timeout, a token cap, and a prompt-size cap.
8. Structured output is deserialized, checked against the exercise library, and screened again.
9. A usage row is written after success. Provider, model, request type, token counts, and estimated cost are stored. There is no billing.

Coach chat uses `AI_COACH`. Workout generation uses `AI_WORKOUT_GENERATION`. Nutrition advice uses `AI_NUTRITION`. Progress insight uses `AI_PROGRESS_INSIGHTS`. All of them also require `AI_USE`. The three generation flags are enabled on Pro and Enterprise. Free leaves every flag disabled. A missing flag row is treated as disabled.

## Context

The context contains goal, fitness level, activity, unit system, preferred days and minutes, up to five recent workout dates, today's calorie and protein totals against targets, and the active challenge name. It does not include email, phone, address, date of birth, emergency contacts, measurements, medical notes, or full database rows.

## Prompts and validation

Prompts live in `FitnessPrompts`: `fitness-coach-v1`, `workout-generation-v1`, and `nutrition-advice-v1`. Workout generation must return JSON. Markdown fences are stripped. Exercise ids must be in the allowed catalog of at most 40 exercises. Sets are clamped to 1–10, reps to 1–100, duration to 1–7200 seconds, and rest to 0–600 seconds. Invalid output is not persisted. The API returns `ai.invalid_output`.

A successful coach or nutrition response stores the user message and the assistant message. A provider failure does not store an assistant message. Conversations are listed and read only for the current user and tenant.

## Failures and cost

Timeouts, transport errors, and an empty key return `ai.unavailable` (HTTP 503). A provider rate limit or an application quota returns `ai.quota` (HTTP 429). The process does not throw those conditions to the host. Logs record the failure category, not the prompt, the key, or the user text.

`Ai:MaxTokens`, `Ai:MaxPromptCharacters`, and `Ai:TimeoutSeconds` bound each call. The context builder sends dates and totals, not workout or nutrition history.
