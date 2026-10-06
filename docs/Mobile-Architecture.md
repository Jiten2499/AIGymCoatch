# Mobile architecture

LifeFit AI mobile is an Expo application in `mobile/`. It is the member and operator client for the existing `/api/v1` backend. This document covers the foundation. Product journeys such as workout logging and AI chat are not implemented yet.

`docs/UI-Standards.md` is not in the repository. Visual rules for the app live in `docs/UI-Design-System.md`.

## Technology

| Choice | Role | Why |
| --- | --- | --- |
| Expo SDK 57, React Native 0.86, React 19, TypeScript | App runtime | Already the repository mobile stack. |
| React Navigation 7 | Stacks and tabs | Official navigation for this Expo generation. Bottom tabs hold the daily destinations. A stack holds settings and business screens. |
| TanStack Query 5 | Server state | Cache, retry, and cancellation stay out of components. Query keys include the tenant id for tenant data. |
| Zustand | Client state | Session, workspace list, and appearance preferences. Server payloads are not copied into it. |
| Axios | HTTP | One client, correlation ids, and refresh handling. |
| React Hook Form and Zod | Forms | Field state and client checks live next to the form. The API remains authoritative. |
| i18next | Copy | Keys, fallback language, and plurals. Hindi and Nepali catalogs exist. Unreviewed product sentences fall back to English. |
| expo-secure-store | Secrets | Access and refresh tokens are not stored in plain AsyncStorage. Values are split so a long JWT fits the platform item limit. |
| expo-localization | Device locale | First launch can follow the device language when it is `en`, `hi`, or `ne`. |
| Jest and React Native Testing Library | Tests | Behavior tests live beside the unit under test as `*.spec.ts` or `*.spec.tsx`. |
| Custom components | UI | No paid component kit. The LifeFit palette is not Material's default theme, so screens use shared tokens instead of a second design system. |

Web can later use its own component library, such as Material UI, against the same API client ideas. Do not import web-only widgets into this app.

## Folder structure

```text
mobile/
├── App.tsx
├── app.json
├── src/
│   ├── app/
│   │   ├── navigation/
│   │   └── providers/
│   ├── components/
│   │   ├── access/
│   │   ├── data-display/
│   │   ├── feedback/
│   │   ├── forms/
│   │   └── ui/
│   ├── constants/
│   ├── features/
│   │   ├── aiCoach/
│   │   ├── auth/
│   │   ├── business/
│   │   ├── challenges/
│   │   ├── dashboard/
│   │   ├── fitness/
│   │   ├── notifications/
│   │   ├── nutrition/
│   │   ├── profile/
│   │   ├── progress/
│   │   ├── settings/
│   │   ├── shell/
│   │   ├── tenant/
│   │   └── workouts/
│   ├── hooks/
│   ├── locales/
│   │   ├── en/
│   │   ├── hi/
│   │   └── ne/
│   ├── services/
│   │   ├── api/
│   │   ├── auth/
│   │   ├── branding/
│   │   ├── localization/
│   │   └── storage/
│   ├── store/
│   ├── test/
│   ├── theme/
│   └── types/
└── assets/
```

Screens stay thin. They render state and call hooks or services. API paths belong in feature `api` modules or `services/api`.

## Navigation

Signed-out users see Login, Register, and Forgot password.

Signed-in users see tabs for Home, Workouts, Nutrition, Progress, and More. More opens Fitness, AI coach, Challenges, Notifications, Profile, and Settings. Business destinations are registered on the same stack and shown from More only when the current workspace type is not `Personal` and the membership roles include `GymOwner`, `GymManager`, `Trainer`, `Receptionist`, or `SuperAdmin`.

That role check is a navigation hint. The API still enforces permissions.

While the stored session is being restored, the navigator shows a loading state instead of a blank screen.

## State

- TanStack Query holds API data. Tenant-owned lists use `pageQueryKey(tenantId, resource, query)`. Personal fitness data uses `['tenant', tenantId, ...]` so a workspace switch cannot show another tenant's profile, plan, or session.
- `useSessionStore` holds the in-memory access token, refresh token, user, and current tenant from `AuthResponse`.
- `useWorkspaceStore` holds `GET /api/v1/tenants/me`, an optional logo URL, and optional feature flags. Switching tenants clears the previous tenant's logo and removes that tenant's query cache.
- `usePreferencesStore` holds appearance, accent, and locale.

## API

`EXPO_PUBLIC_API_URL` defaults to `http://localhost:8080`. In Expo Go that localhost default is rewritten to the computer that served the bundle, still on port 8080, so a phone on the same Wi-Fi reaches the API. An Android emulator that cannot use that address should set `http://10.0.2.2:8080`. A desktop browser on the API machine uses `http://127.0.0.1:8080`.

The client base path is `/api/v1`. Each request sets `X-Correlation-ID`. A 401 outside `/auth/*` refreshes once through `POST /api/v1/auth/refresh`. A failed refresh clears the session. ProblemDetails become `ApiError`, including field errors when the body contains `errors`. A response without a body is treated as offline.

Tokens are not written to logs.

## Authentication

Implemented against the existing contract:

- `POST /api/v1/auth/login`
- `POST /api/v1/auth/register`
- `POST /api/v1/auth/forgot-password`
- `POST /api/v1/auth/refresh`
- `POST /api/v1/auth/logout`
- `GET /api/v1/tenants/me`
- `POST /api/v1/tenants/switch`

Login returns `AuthResponse`: access token, refresh token, expiry, user profile, and current tenant. Switch replaces both tokens. Forgot password shows the same confirmation whether or not the email exists, matching the API.

Registration password checks mirror the current policy: 12 characters with uppercase, lowercase, a number, and a symbol. The server still decides.

## Tenant context

A user is not a tenant. `currentTenant` comes from the access token payload returned by login, refresh, or switch. Personal workspaces use LifeFit AI branding. Business workspaces use the tenant name. A logo URL is applied only for the active tenant id.

## Lists

`PageQuery` sends `page`, `pageSize`, optional `sort`, and optional `term`. Page size outside 1–100 is sent as 20 so the client matches the API clamp instead of downloading a full table. `AppList` uses `FlatList`. Search text should pass through `useDebouncedValue` before it becomes a query key.

## Forms

Auth forms use React Hook Form, Zod, and `FormField` for the label, hint, and error. Server field errors can be mapped with `fieldErrors`.

## Testing

Colocate tests:

- `LoginScreen.spec.tsx`
- `AppButton.spec.tsx`
- `tokenStorage.spec.ts`
- `branding.spec.ts`

Run from `mobile/`:

```text
npm install
npm start
npm test
npm run test:coverage
npm run typecheck
```

`npm run typecheck` is the production TypeScript build check. A store binary is produced with EAS or `npx expo export` when a release is requested.

## Accessibility

Shared controls set roles, labels, 44pt targets, and font scaling up to 1.4. Theme pairs keep accent text on a contrasting fill. Reduced motion sets animation duration to 0. Hindi and Nepali are left-to-right. `layoutDirection` is ready for a future right-to-left locale.

## Future backend integration requirements

These are not called today, because the API does not expose them:

- `GET` and `PUT /api/v1/users/me/preferences` for appearance (`system`, `light`, `dark`), accent (`lime`, `teal`, `blue`, `violet`, `coral`), and locale (`en`, `hi`, `ne`).
- `GET /api/v1/users/me/permissions` returning the current tenant's permission codes. Until then, business navigation uses workspace roles from `GET /api/v1/tenants/me`.
- A tenant branding payload with accent color. `GET /api/v1/business/profile` already returns `logoUrl`, `businessName`, `timeZone`, and `currency` for callers who hold `BUSINESS_PROFILE_READ`. The shell does not call it for every user, because members receive 403.
- Reviewed Hindi and Nepali copy. The catalogs currently fall back to English.
- `GET /api/v1/features` already exists and requires `FEATURE_READ`. Feature-gated UI should load it only after that permission is known.

Do not add schema or controller changes for these until a product task asks for them.
