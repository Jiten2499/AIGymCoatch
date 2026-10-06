import { NativeModules, TurboModuleRegistry } from 'react-native';

const defaultApiUrl = 'http://localhost:8080';

export function hostFromScriptUrl(scriptUrl: string | null | undefined): string | null {
  if (!scriptUrl) {
    return null;
  }

  const match = /^[a-zA-Z][\w+.-]*:\/\/([^/:?#]+)/.exec(scriptUrl);
  const host = match?.[1];
  if (!host || isLoopbackHost(host)) {
    return null;
  }

  return host;
}

export function resolveApiBaseUrl(configured: string | undefined, packagerHost: string | null): string {
  const base = (configured?.trim() || defaultApiUrl).replace(/\/$/, '');
  if (!packagerHost || !isLoopbackUrl(base)) {
    return base;
  }

  const url = new URL(base);
  url.hostname = packagerHost;
  return url.toString().replace(/\/$/, '');
}

function isLoopbackHost(host: string): boolean {
  return host === 'localhost' || host === '127.0.0.1' || host === '::1';
}

function isLoopbackUrl(value: string): boolean {
  try {
    return isLoopbackHost(new URL(value).hostname);
  } catch {
    return false;
  }
}

type SourceCodeModule = {
  getConstants?: () => { scriptURL?: string | null };
  scriptURL?: string;
};

export function scriptUrlFromSourceCode(source: SourceCodeModule | null | undefined): string | null {
  const fromConstants = source?.getConstants?.()?.scriptURL;
  if (typeof fromConstants === 'string' && fromConstants.length > 0) {
    return fromConstants;
  }

  return typeof source?.scriptURL === 'string' && source.scriptURL.length > 0 ? source.scriptURL : null;
}

function readPackagerHost(): string | null {
  try {
    const turbo = TurboModuleRegistry.get<SourceCodeModule>('SourceCode');
    const legacy = NativeModules.SourceCode as SourceCodeModule | undefined;
    return hostFromScriptUrl(scriptUrlFromSourceCode(turbo) ?? scriptUrlFromSourceCode(legacy));
  } catch {
    return null;
  }
}

export const apiBaseUrl = resolveApiBaseUrl(process.env.EXPO_PUBLIC_API_URL, readPackagerHost());

export const apiRoot = `${apiBaseUrl}/api/v1`;
