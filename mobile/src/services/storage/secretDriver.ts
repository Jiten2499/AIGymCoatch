import * as SecureStore from 'expo-secure-store';

export type SecretDriver = {
  get(key: string): Promise<string | null>;
  set(key: string, value: string): Promise<void>;
  remove(key: string): Promise<void>;
};

const expoDriver: SecretDriver = {
  get: (key) => SecureStore.getItemAsync(key),
  set: (key, value) => SecureStore.setItemAsync(key, value),
  remove: (key) => SecureStore.deleteItemAsync(key),
};

let driver: SecretDriver = expoDriver;

export function setSecretDriver(next: SecretDriver): void {
  driver = next;
}

export function getSecretDriver(): SecretDriver {
  return driver;
}

export function memorySecretDriver(): SecretDriver {
  const values = new Map<string, string>();
  return {
    async get(key) {
      return values.get(key) ?? null;
    },
    async set(key, value) {
      values.set(key, value);
    },
    async remove(key) {
      values.delete(key);
    },
  };
}
