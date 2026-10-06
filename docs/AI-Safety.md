# AI safety

LifeFit AI gives general fitness and nutrition guidance. It does not diagnose disease, prescribe medication, recommend dangerous supplements, or design extreme diets.

## Boundaries

`AiSafetyPolicy` rejects input and output that asks for diagnosis, prescriptions, medication, anabolic steroids, eating-disorder behavior, starvation, a 500-calorie diet, or a prompt-injection attempt such as "ignore previous instructions" or "reveal the system prompt". The API returns a fixed refusal and does not store the unsafe assistant text.

Nutrition responses append a statement that the text is general information, not a medical diagnosis or treatment. The product does not collect injuries or medical history, and the context builder does not invent that data.

## Data sent to a provider

Only the controlled context and the current user message are sent. Email, phone, address, date of birth, emergency contacts, body-measurement history, and raw entity graphs stay in the database. System prompts are not returned to the client and are not written to `AiMessages`.

## Output

Free-form coach text is screened before it is stored. Workout JSON is parsed, limited to known exercise ids, and clamped before a program is saved. A model cannot create an exercise id that is not in the catalog passed to the prompt. Failed validation returns `ai.invalid_output` and leaves the database unchanged.

## Quotas and secrets

Daily and monthly request counts are stored in `AiUsageRecords`. The provider key stays in configuration. Logs do not include the key, prompt body, or user message. Body-scan storage keys that look like web addresses are rejected, so a private URL is not persisted or returned.

## What this layer does not do

It is not a clinical safety system, a content-moderation product, or a distributed rate limiter. Those limits are application checks for Part 4. Production rate limiting and observability belong to a later part.
