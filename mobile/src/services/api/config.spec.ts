import { describe, expect, it } from '@jest/globals';
import {
  defaultApiUrlForEnvironment,
  hostFromPackagerAddress,
  hostFromScriptUrl,
  productionApiUrl,
  resolveApiBaseUrl,
  scriptUrlFromSourceCode,
} from './config';

describe('resolveApiBaseUrl', () => {
  it('points a localhost API at the computer serving Expo Go', () => {
    const scriptUrl = 'http://192.168.1.4:8081/index.bundle?platform=android&dev=true';

    expect(hostFromScriptUrl(scriptUrl)).toBe('192.168.1.4');
    expect(resolveApiBaseUrl('http://localhost:8080', '192.168.1.4', { isDev: true })).toBe('http://192.168.1.4:8080');
  });

  it('keeps an explicit API address in development', () => {
    expect(resolveApiBaseUrl('http://10.0.2.2:8080/', '192.168.1.4', { isDev: true })).toBe('http://10.0.2.2:8080');
    expect(resolveApiBaseUrl(undefined, null, { isDev: true })).toBe('http://localhost:8080');
  });

  it('defaults production builds to the HTTPS Growventur API', () => {
    expect(defaultApiUrlForEnvironment(false)).toBe(productionApiUrl);
    expect(resolveApiBaseUrl(undefined, null, { isDev: false })).toBe(productionApiUrl);
    expect(resolveApiBaseUrl('https://api.growventur.com/', '192.168.1.4', { isDev: false })).toBe('https://api.growventur.com');
  });

  it('refuses localhost in production builds', () => {
    expect(resolveApiBaseUrl('http://localhost:8080', '192.168.1.4', { isDev: false })).toBe(productionApiUrl);
    expect(resolveApiBaseUrl('http://127.0.0.1:8080', null, { isDev: false })).toBe(productionApiUrl);
  });

  it('reads the Expo bundle address from the source-code constants', () => {
    const scriptUrl = 'http://192.168.1.4:8081/index.bundle?platform=android';

    expect(scriptUrlFromSourceCode({ getConstants: () => ({ scriptURL: scriptUrl }) })).toBe(scriptUrl);
    expect(scriptUrlFromSourceCode({ scriptURL: scriptUrl })).toBe(scriptUrl);
    expect(scriptUrlFromSourceCode(null)).toBeNull();
  });

  it('reads a host and port from the Expo packager address', () => {
    expect(hostFromPackagerAddress('192.168.18.183:8081')).toBe('192.168.18.183');
    expect(resolveApiBaseUrl('http://localhost:8080', '192.168.18.183', { isDev: true })).toBe('http://192.168.18.183:8080');
  });

  it('ignores a loopback packager host', () => {
    expect(hostFromScriptUrl('http://127.0.0.1:8081/index.bundle')).toBeNull();
    expect(resolveApiBaseUrl('http://localhost:8080', null, { isDev: true })).toBe('http://localhost:8080');
  });
});
