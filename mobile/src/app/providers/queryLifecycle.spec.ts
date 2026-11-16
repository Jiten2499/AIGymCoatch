import { describe, expect, it, jest } from '@jest/globals';
import { focusManager, onlineManager } from '@tanstack/react-query';
import { AppState, type AppStateStatus, type NativeEventSubscription } from 'react-native';
import { ApiError } from '../../services/api/problem';
import { bindQueryLifecycle, markQueryOnline, reportQueryConnectivity, shouldRetryQuery } from './queryLifecycle';

describe('queryLifecycle', () => {
  it('binds AppState to focusManager and restores online on foreground', () => {
    const listeners: Array<(state: AppStateStatus) => void> = [];
    const remove = jest.fn();
    jest.spyOn(AppState, 'addEventListener').mockImplementation((_type, listener) => {
      listeners.push(listener as (state: AppStateStatus) => void);
      return { remove } as NativeEventSubscription;
    });
    const setFocused = jest.spyOn(focusManager, 'setFocused');
    const setOnline = jest.spyOn(onlineManager, 'setOnline');

    const unbind = bindQueryLifecycle();
    expect(listeners.length).toBeGreaterThan(0);
    listeners[0]?.('background');
    expect(setFocused).toHaveBeenCalledWith(false);
    listeners[0]?.('active');
    expect(setFocused).toHaveBeenCalledWith(true);
    expect(setOnline).toHaveBeenCalledWith(true);

    unbind();
    expect(remove).toHaveBeenCalled();
  });

  it('marks connectivity from ApiError.offline', () => {
    const setOnline = jest.spyOn(onlineManager, 'setOnline');
    reportQueryConnectivity(new ApiError('Network request failed', 0, null, true));
    expect(setOnline).toHaveBeenCalledWith(false);
    markQueryOnline();
    expect(setOnline).toHaveBeenCalledWith(true);
  });

  it('reconnects after a cold network failure without leaving the app', () => {
    jest.useFakeTimers();
    const setOnline = jest.spyOn(onlineManager, 'setOnline');
    reportQueryConnectivity(new ApiError('Network request failed', 0, null, true));
    expect(setOnline).toHaveBeenCalledWith(false);
    jest.advanceTimersByTime(700);
    expect(setOnline).toHaveBeenLastCalledWith(true);
    markQueryOnline();
    setOnline.mockClear();
    for (let attempt = 0; attempt < 4; attempt += 1) {
      reportQueryConnectivity(new ApiError('Network request failed', 0, null, true));
      jest.advanceTimersByTime(700);
    }
    expect(setOnline.mock.calls.filter((call) => call[0] === true)).toHaveLength(3);
    jest.useRealTimers();
  });

  it('retries a dropped connection and stops on auth or missing records', () => {
    const offline = new ApiError('Network request failed', 0, null, true);
    expect(shouldRetryQuery(0, offline)).toBe(true);
    expect(shouldRetryQuery(3, offline)).toBe(false);
    expect(shouldRetryQuery(0, new ApiError('missing', 404, null))).toBe(false);
    expect(shouldRetryQuery(0, new ApiError('denied', 403, null))).toBe(false);
  });
});
