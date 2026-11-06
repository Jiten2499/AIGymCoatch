import { describe, expect, it, jest } from '@jest/globals';
import { focusManager, onlineManager } from '@tanstack/react-query';
import { AppState, type AppStateStatus, type NativeEventSubscription } from 'react-native';
import { ApiError } from '../../services/api/problem';
import { bindQueryLifecycle, markQueryOnline, reportQueryConnectivity } from './queryLifecycle';

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
});
