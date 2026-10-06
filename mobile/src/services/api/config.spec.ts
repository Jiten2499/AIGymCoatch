import { describe, expect, it } from '@jest/globals';
import { hostFromScriptUrl, resolveApiBaseUrl, scriptUrlFromSourceCode } from './config';

describe('resolveApiBaseUrl', () => {
  it('points a localhost API at the computer serving Expo Go', () => {
    const scriptUrl = 'http://192.168.1.4:8081/index.bundle?platform=android&dev=true';

    expect(hostFromScriptUrl(scriptUrl)).toBe('192.168.1.4');
    expect(resolveApiBaseUrl('http://localhost:8080', '192.168.1.4')).toBe('http://192.168.1.4:8080');
  });

  it('keeps an explicit API address', () => {
    expect(resolveApiBaseUrl('http://10.0.2.2:8080/', '192.168.1.4')).toBe('http://10.0.2.2:8080');
    expect(resolveApiBaseUrl(undefined, null)).toBe('http://localhost:8080');
  });

  it('reads the Expo bundle address from the source-code constants', () => {
    const scriptUrl = 'http://192.168.1.4:8081/index.bundle?platform=android';

    expect(scriptUrlFromSourceCode({ getConstants: () => ({ scriptURL: scriptUrl }) })).toBe(scriptUrl);
    expect(scriptUrlFromSourceCode({ scriptURL: scriptUrl })).toBe(scriptUrl);
    expect(scriptUrlFromSourceCode(null)).toBeNull();
  });

  it('ignores a loopback packager host', () => {
    expect(hostFromScriptUrl('http://127.0.0.1:8081/index.bundle')).toBeNull();
    expect(resolveApiBaseUrl('http://localhost:8080', null)).toBe('http://localhost:8080');
  });
});
