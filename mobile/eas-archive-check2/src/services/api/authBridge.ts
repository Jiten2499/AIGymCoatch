type AuthBridge = {
  getAccessToken: () => string | null;
  refresh: () => Promise<string | null>;
  onSessionExpired: () => void;
};

const idle: AuthBridge = {
  getAccessToken: () => null,
  refresh: async () => null,
  onSessionExpired: () => undefined,
};

let bridge: AuthBridge = idle;

export function setAuthBridge(next: AuthBridge): void {
  bridge = next;
}

export function getAuthBridge(): AuthBridge {
  return bridge;
}
