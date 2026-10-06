# Fitness domain

Part 4 stores the member experience separately from identity and gym membership. The same user can keep a fitness profile in a personal tenant and another profile in each gym tenant. `TenantMember` remains workspace access. `GymMembership` remains the paid gym membership. Neither is copied into a second user account.

## Ownership

| Entity | Owner | Isolation |
| --- | --- | --- |
| Exercise | System when `TenantId` is null; otherwise the tenant | System rows are immutable. Custom rows are visible only in that tenant. |
| Fitness profile, goals, measurements, body scans | User inside one tenant | `UserId` + `TenantId` |
| Workout program | System, tenant, or user | Null tenant is shared. A tenant program has no user. A user program stores both ids. |
| Program day and program exercise | The program | Reached through the program. They do not repeat the user id. |
| Workout session, nutrition profile, nutrition log, AI conversation, notification | User inside one tenant | `UserId` + `TenantId` |
| Session exercise and set | The session | Reached through the session owned by the current user and tenant. |
| Challenge | System or tenant | A null tenant is a shared challenge. |
| Challenge participant | User inside the workspace they joined | Unique per challenge and user. |
| AI message and AI usage | The conversation or the calling user | Messages follow the conversation. Usage is counted per user and tenant. |

## Fitness profile and goals

A profile stores unit system, height, weight, fitness level, primary goal, activity level, training experience, preferred days, preferred duration, and preferred location. It does not store medical history. `Metric` and `Imperial` are stored on the profile and again on each measurement so older rows keep the unit they were recorded in.

A user can have many goals. Status is `Active`, `Completed`, or `Cancelled`. Completing or cancelling a goal keeps the row.

## Exercises

The catalog is shared. Twelve system exercises are seeded. A tenant can add a custom exercise with `EXERCISE_MANAGE`. Names are unique among live system rows and unique per tenant among custom rows.

## Workouts

A program is the plan: ordered days, then ordered exercises that point at the library. A session is the work performed. Sessions may omit the program for an ad-hoc workout. Status moves `Planned` to `InProgress` to `Completed`, or to `Cancelled` before completion. A completed session cannot be completed again.

Sets leave weight, reps, duration, distance, and RPE optional. Strength uses weight and reps. Cardio uses duration and distance. Bodyweight uses reps. Mobility uses duration. Weight is `DECIMAL(8,2)`, distance is `DECIMAL(10,2)`, and RPE is `DECIMAL(3,1)` from 0 to 10.

Personal records are calculated, not stored. The query aggregates maximum weight, reps, duration, and distance from completed sets. Estimated one-rep max uses Epley: `weight * (1 + reps / 30)` when weight and reps are greater than zero. One rep returns the weight. The result is rounded to two decimals. A record is not claimed without that definition.

History and counts are paged or aggregated in SQL. Weekly and monthly counts filter completed sessions. The streak uses distinct completed dates from the last 90 days.

## Measurements and scans

Measurements are inserted. They are not overwritten. Body-scan rows store a date, source, status, and an opaque storage key. A value containing `://` or starting with `http` is rejected. The API does not return a private object URL.

## Nutrition

The nutrition profile stores calorie and macro targets, an extensible diet preference (`Vegetarian`, `Vegan`, `NonVegetarian`, `Custom`), and meal frequency. Logs store a date, meal type, food name, quantity, and macros. There is no full food database. The daily summary sums one user, one tenant, and one date, then compares those totals with the profile targets.

## Challenges and notifications

Challenge types are `WorkoutCount`, `ConsistencyDays`, and `Custom`. Completing a workout increments joined workout-count challenges and, on the first completion that day, consistency challenges. Custom progress is recorded explicitly. Reaching the target sets `CompletedAt`. There is no social leaderboard.

Notifications are stored rows for challenge joins and completed workouts. Push, email, and SMS delivery are not implemented.

## Member dashboard

`GET /api/v1/member/dashboard` returns the active goal, latest measurement, streak, weekly and monthly workout counts, five recent sessions, today's nutrition summary, the active challenge, and gym membership status when a member profile exists in the current tenant. A personal tenant returns a null membership status.

## Indexes

Important indexes are `UX_Exercises_SystemName`, `UX_Exercises_TenantId_Name`, `UX_FitnessProfiles_User_Tenant`, `IX_FitnessGoals_User_Status`, `IX_BodyMeasurements_User_Date`, `IX_WorkoutSessions_User_Started`, `UX_WorkoutSessionExercises_Session_Sequence`, `UX_WorkoutSets_Exercise_Set`, `IX_NutritionLogs_User_Date`, `IX_AiConversations_User_Updated`, `IX_AiMessages_Conversation_Created`, `IX_AiUsageRecords_User_Created`, `IX_Challenges_Tenant_Status`, `UX_ChallengeParticipants_Challenge_User`, and `IX_Notifications_User_Unread`. Child workout rows are reached from the parent key, so they do not repeat `UserId`.
