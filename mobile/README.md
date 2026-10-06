# LifeFit AI mobile

Expo client for the LifeFit AI API. Architecture and visual tokens are documented in `docs/Mobile-Architecture.md` and `docs/UI-Design-System.md`.

```text
npm install
npm start
npm test
npm run test:coverage
npm run typecheck
```

Set `EXPO_PUBLIC_API_URL` in `.env` when the API is not on this computer. The default `http://localhost:8080` is rewritten to the computer serving Expo Go, on port 8080, so a phone on the same Wi-Fi can reach it. An Android emulator that cannot use that address should set `http://10.0.2.2:8080`.
