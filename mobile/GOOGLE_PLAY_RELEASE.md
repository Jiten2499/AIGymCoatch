# GrowFit — Google Play first release guide

Parent brand: Growventur. Mobile client: Expo + React Native under `mobile/`.  
This document prepares an **internal testing** release. Nothing is uploaded or published automatically.

## 1. Current project configuration

| Item | Value |
| --- | --- |
| App display name | GrowFit |
| Expo slug | `growfit` |
| Deep-link scheme | `growfit` |
| Expo SDK | 58 |
| React Native | 0.88.0 |
| React | 19.3.0 |
| App version (`expo.version`) | `1.0.0` |
| Android `versionCode` (local) | `1` |
| EAS `appVersionSource` | `remote` + production `autoIncrement` |
| EAS project id | `922a0d36-9711-4e43-875a-82b4d4a6c9c3` |
| Orientation | Portrait |
| Auth token storage | `expo-secure-store` |
| Push / FCM | Not implemented (in-app notification polling only) |

## 2. Android package identifier

**`com.growventur.growfit`**

- Already present in `app.json` before this release prep.
- Matches the suggested production id `com.growventur.growfit`.
- **Do not change** this id after the first Play Console upload.

**You must confirm:** whether this package is already registered in Google Play Console or used by any prior uploaded AAB/APK. If a different id was used historically, stop and decide before building.

## 3. Expo SDK and dependency status

- Expo `^58.0.0` with matching Expo modules (`expo-secure-store`, `expo-image-picker`, `expo-localization`, `expo-status-bar`).
- No Firebase / `expo-notifications` package is installed.
- No unnecessary dependency upgrades were made for this release prep.
- Lockfile: `package-lock.json` (npm).

## 4. Production API configuration

| Profile | `EXPO_PUBLIC_API_URL` |
| --- | --- |
| Local / Expo Go (`.env.example`) | `http://localhost:8080` (rewritten to packager host in `__DEV__`) |
| EAS `development` | `http://localhost:8080` |
| EAS `preview` | `https://api.growventur.com` |
| EAS `production` | `https://api.growventur.com` |

Runtime safeguards in `src/services/api/config.ts`:

- Non-dev builds default to `https://api.growventur.com`.
- Non-dev builds refuse loopback hosts (`localhost` / `127.0.0.1`) and fall back to production HTTPS.
- API root remains `{base}/api/v1`.

Verified from the existing web client default (`NEXT_PUBLIC_API_URL` fallback).  
**Staging API URL:** not configured in-repo — supply later if needed.

Never put JWT secrets, DB passwords, or private keys in the mobile app or this file.

## 5. EAS setup and build commands

Prerequisites:

```bash
cd mobile
npm install
npm install -g eas-cli   # or use npx eas-cli
eas login
eas whoami
```

Recommended first path: **internal testing APK/AAB**, not production store rollout.

```bash
# Internal / stakeholder testing (APK)
npm run eas:preview
# equivalent: eas build --platform android --profile preview

# Play Store upload candidate (AAB)
npm run eas:production
# equivalent: eas build --platform android --profile production
```

Inspect / manage Android keystore (do **not** regenerate unless intentional):

```bash
npm run eas:credentials
# or: eas credentials
```

## 6. Signing credentials setup

- Prefer **EAS-managed** Android credentials.
- Do **not** delete or regenerate an existing keystore once Play has accepted a build under this package.
- Local keystore files (`*.jks`, `*.keystore`) must stay out of git (already ignored).

## 7. Internal testing instructions

1. Build with `preview` or upload a `production` AAB as a Play **internal testing** draft.
2. Add testers in Play Console → Testing → Internal testing.
3. Confirm login against `https://api.growventur.com`.
4. Smoke-test member, trainer, and staff/business roles with dedicated test accounts.
5. Verify logout, session expiry (forced refresh failure), and offline messaging.

## 8. Production build instructions

1. Confirm package id, privacy policy URL, and reviewer access are ready.
2. `eas build --platform android --profile production`
3. Download the `.aab` from the EAS build page.
4. Upload to Play Console → internal or closed testing track first.
5. Promote to production only after your approval — not automated here.

`eas.json` submit profile targets Android track `internal` with `releaseStatus: draft` so accidental submit stays draft/internal.

## 9. Play Console upload instructions

1. Create app **GrowFit** if it does not exist.
2. Set package name **`com.growventur.growfit`** exactly (immutable after first upload).
3. Complete Store listing, Data safety, Content rating, Target audience, Ads declaration.
4. Upload AAB under Testing → Internal testing.
5. Provide App access / reviewer instructions if login is required.
6. Roll out to internal testers; gather feedback; then promote.

## 10. Store listing checklist

### App name

GrowFit

### Short description (≤ 80 characters) — draft

```text
Gym and fitness workspace for members, coaches, and staff.
```

(Character count: 58)

### Full description — draft

```text
GrowFit by Growventur helps gyms and fitness businesses run day-to-day work from a phone.

Members can follow fitness activity in their workspace. Coaches and staff can manage members, memberships, leads, expenses, leave, communications, and more — according to the permissions granted by their business.

Sign in with your GrowFit / Growventur account, choose your workspace, and use the features enabled for your role and plan.

GrowFit connects securely over HTTPS to your Growventur business backend. Push delivery is not part of this first release; in-app notifications and circulars sync while you use the app.
```

### Category recommendation

Health & Fitness (primary). Business as optional secondary if offered.

### Contact / URLs

| Field | Status |
| --- | --- |
| Website | **MISSING** — confirm official Growventur / GrowFit URL |
| Contact email | **MISSING** — confirm Play Console support email |
| Privacy policy URL | **MISSING** — required before production publishing |
| Support / delete-account web URL | **MISSING** — see compliance |

### Graphics requirements

| Asset | Spec | Local status |
| --- | --- | --- |
| High-res icon | 512 × 512 PNG | Source `assets/icon.png` is 1024×1024 — export a 512×512 for Play Console |
| Feature graphic | 1024 × 500 | **MISSING** — design required |
| Phone screenshots | min 2; commonly 1080 × 1920 or similar | **MISSING** — capture from build |
| 7-inch / 10-inch tablets | Optional | Optional |
| Adaptive icons | Present under `assets/android-icon-*.png` | Present |
| Splash | `assets/splash-icon.png` + `#07110d` | Wired in `app.json` |

### App access / reviewer instructions template

```text
GrowFit requires a signed-in account.

1. Open the app.
2. Sign in with the reviewer credentials provided in Play Console → App access.
3. If prompted, select the provided business workspace.
4. Do not change password or delete the reviewer account.

Reviewer email: [PROVIDE]
Temporary password: [PROVIDE SECURELY OUT OF BAND]
Workspace / tenant name: [PROVIDE]
Roles covered: [Member / Trainer / Staff — PROVIDE]
```

Never commit real passwords to git or this markdown file.

### Data safety declaration checklist (manual)

Confirm in Play Console based on actual behavior:

- [ ] Personal info: name, email, phone (account / profile)
- [ ] Photos: profile photos, chat images, progress photos (user-generated)
- [ ] Health & fitness: workouts, measurements, goals, assessments (member fitness features)
- [ ] App activity: in-app actions / notifications
- [ ] Data encrypted in transit: **Yes** (HTTPS production API)
- [ ] Data deleted on request: **only if** you offer account deletion or a documented process
- [ ] Data shared with third parties: declare any analytics / crash / ads SDKs (none bundled for push/ads in this first prep)
- [ ] Collected for app functionality / account management

### Content rating checklist (manual)

Complete IARC questionnaire honestly. Expect questions about:

- [ ] User-generated content (chat, photos, notes)
- [ ] Account required
- [ ] Location sharing (confirm whether the app requests location — currently not a declared permission)
- [ ] Age target (business/workforce + adult fitness users — confirm policy)

## 11. Compliance items requiring manual action

| Item | Classification | Notes |
| --- | --- | --- |
| Privacy policy public URL | **NEEDS MY INPUT** | Required for store listing |
| Website + support email | **NEEDS MY INPUT** | Not found verified in-repo |
| Account deletion (in-app or web) | **BLOCKED** for production publish | Registration exists; no `DELETE users/me` account API / UI found |
| Play Console package registration | **NEEDS MY INPUT** | Confirm `com.growventur.growfit` |
| Reviewer test accounts | **NEEDS MY INPUT** | Create safe non-production credentials |
| Feature graphic + screenshots | **NEEDS MY INPUT** | Create assets |
| Ads declaration | **READY** to declare “No ads” unless you add an SDK later |
| Target API / Play policy | **WARNING** | Confirm current Play target SDK with EAS Expo 58 image at build time |
| Data safety health declarations | **NEEDS MY INPUT** | Fitness measurements / photos are collected by features |
| Push notifications | **READY** (N/A) | Placeholder only; no FCM SDK |

## 12. Known blockers and recommendations

### BLOCKED

1. **Account deletion** — Google Play requires a path to delete accounts created in-app. Add backend + mobile/web flow, or a verified web deletion URL before production publish. Internal testing may proceed while this is built, depending on your Console enforcement timing.

### WARNING

1. Camera / photo library permissions are required for profile and progress photos.
2. `RECORD_AUDIO` is blocked in Android config; ensure image-only picking stays sufficient.
3. Google Drive OAuth return path now uses `growfit://…` (was `lifefit://`). Confirm backend allows that mobile return path.
4. Sensitive `console` logging is gated behind `__DEV__` for API failures and session refresh; avoid adding token logging.

### Functional audit (code review — not a device farm run)

| Flow | Status |
| --- | --- |
| Launch / providers / navigation | Present |
| Login / register / forgot password / logout | Present |
| Secure token storage + refresh | Present |
| Tenant / workspace switching | Present |
| Member / trainer / staff feature areas | Present (permission/feature gated) |
| Image upload (profile, members, chat, progress) | Present |
| Push notifications | Not implemented |
| Offline / API errors | Handled via shared error helpers / screen states |
| Account deletion | Missing |

Do not remove features for the first release solely because they are unused.

## 13. Future version update procedure

1. Decide user-facing version (`1.0.1`, `1.1.0`, …) in `app.json` `expo.version` when appropriate.
2. With `appVersionSource: remote` and production `autoIncrement: true`, EAS increments Android `versionCode` for production builds — do not reuse a prior Play versionCode.
3. Build: `eas build --platform android --profile production`
4. Upload AAB → internal/closed testing → production.
5. Keep production `EXPO_PUBLIC_API_URL` on HTTPS only.
6. Never rotate the upload keystore casually.

## Exact commands for the first AAB

This repo is a monorepo. Prefer a mobile-only upload so EAS does not archive `backend/`, `web/`, or `.local/`:

```bash
cd mobile
npm install
eas login
# Windows PowerShell
$env:EAS_NO_VCS='1'
$env:EAS_PROJECT_ROOT=(Get-Location).Path
$env:EAS_BUILD_NO_EXPO_GO_WARNING='true'
eas build --platform android --profile production --non-interactive
```

Then download the AAB from the Expo build page and upload it manually to Google Play Console (internal testing recommended first).

**Do not run production upload/submit until you explicitly approve.**

### Successful production AAB (2026-11-13)

- Build id: `c95a5de4-b58e-4494-a506-566b3c70c3e2`
- `versionCode`: **8** (remote auto-increment)
- Artifact: see Expo build page / AAB URL from the EAS CLI output
- Working upload mode: `EAS_NO_VCS=1` from `mobile/` (~740 KB archive)
- Also required: aligned Expo SDK packages, `expo-splash-screen` plugin, and `legacy-peer-deps` (`.npmrc` + EAS env)

Earlier attempts (`versionCode` 2–7) failed during **Install dependencies** when the monorepo archive was huge or peer resolution failed.
