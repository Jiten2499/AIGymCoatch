import { getSecretDriver } from '../storage/secretDriver';

const chunkSize = 1800;

export type StoredSession = {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
};

export async function saveSession(session: StoredSession): Promise<void> {
  await writeChunked('lifefit.access', session.accessToken);
  await writeChunked('lifefit.refresh', session.refreshToken);
  await getSecretDriver().set('lifefit.expires', session.expiresAt);
}

export async function readSession(): Promise<StoredSession | null> {
  const accessToken = await readChunked('lifefit.access');
  const refreshToken = await readChunked('lifefit.refresh');
  const expiresAt = await getSecretDriver().get('lifefit.expires');
  if (!accessToken || !refreshToken || !expiresAt) {
    return null;
  }
  return { accessToken, refreshToken, expiresAt };
}

export async function clearSession(): Promise<void> {
  await removeChunked('lifefit.access');
  await removeChunked('lifefit.refresh');
  await getSecretDriver().remove('lifefit.expires');
}

async function writeChunked(key: string, value: string): Promise<void> {
  const driver = getSecretDriver();
  const count = Math.max(1, Math.ceil(value.length / chunkSize));
  await driver.set(`${key}.n`, String(count));
  for (let index = 0; index < count; index += 1) {
    await driver.set(`${key}.${index}`, value.slice(index * chunkSize, (index + 1) * chunkSize));
  }
}

async function readChunked(key: string): Promise<string | null> {
  const driver = getSecretDriver();
  const countText = await driver.get(`${key}.n`);
  const count = Number(countText);
  if (!countText || Number.isNaN(count) || count < 1) {
    return null;
  }
  const parts: string[] = [];
  for (let index = 0; index < count; index += 1) {
    const part = await driver.get(`${key}.${index}`);
    if (part === null) {
      return null;
    }
    parts.push(part);
  }
  return parts.join('');
}

async function removeChunked(key: string): Promise<void> {
  const driver = getSecretDriver();
  const countText = await driver.get(`${key}.n`);
  const count = Number(countText);
  if (countText && !Number.isNaN(count)) {
    for (let index = 0; index < count; index += 1) {
      await driver.remove(`${key}.${index}`);
    }
  }
  await driver.remove(`${key}.n`);
}
