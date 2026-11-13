# GrowFit mobile

Expo client for the Growventur / GrowFit API. Architecture and visual tokens are documented in `docs/Mobile-Architecture.md` and `docs/UI-Design-System.md`.

Google Play release prep: see [`GOOGLE_PLAY_RELEASE.md`](./GOOGLE_PLAY_RELEASE.md).

```text
npm install
npm start
npm test
npm run typecheck
```

## API URL

Copy `.env.example` to `.env` for local development.

- Default in development: `http://localhost:8080` (rewritten to the machine serving Expo Go).
- Android emulator to host API: `http://10.0.2.2:8080`.
- Production / EAS preview & production profiles: `https://api.growventur.com`.

## Android package

`com.growventur.growfit`

## EAS builds

```text
npm run eas:preview      # internal APK against production API
npm run eas:production   # AAB for Play Console (does not upload by itself)
npm run eas:credentials  # inspect signing credentials — do not regenerate casually
```
