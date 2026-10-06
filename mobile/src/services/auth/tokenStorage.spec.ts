import { beforeEach, describe, expect, it } from '@jest/globals';
import { memorySecretDriver, setSecretDriver } from '../storage/secretDriver';
import { clearSession, readSession, saveSession } from './tokenStorage';

describe('tokenStorage', () => {
  beforeEach(() => {
    setSecretDriver(memorySecretDriver());
  });

  it('round-trips tokens and clears them on sign-out', async () => {
    const accessToken = `header.${'a'.repeat(4000)}.signature`;
    await saveSession({ accessToken, refreshToken: 'refresh-token', expiresAt: '2026-10-02T00:00:00Z' });

    await expect(readSession()).resolves.toEqual({
      accessToken,
      refreshToken: 'refresh-token',
      expiresAt: '2026-10-02T00:00:00Z',
    });

    await clearSession();
    await expect(readSession()).resolves.toBeNull();
  });
});
