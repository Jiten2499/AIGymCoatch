import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { fireEvent } from '@testing-library/react-native';
import { endSession } from '../../../services/auth/session';
import { memorySecretDriver, setSecretDriver } from '../../../services/storage/secretDriver';
import { usePreferencesStore } from '../../../store/preferencesStore';
import { renderWithProviders } from '../../../test/render';
import { SettingsScreen } from './SettingsScreen';

jest.mock('../../../services/auth/session', () => ({ endSession: jest.fn(async () => undefined) }));

describe('SettingsScreen', () => {
  beforeEach(() => {
    setSecretDriver(memorySecretDriver());
    usePreferencesStore.setState({ appearance: 'system', accent: 'lime', locale: 'en' });
    jest.mocked(endSession).mockClear();
  });

  it('stores a light appearance choice', async () => {
    const view = await renderWithProviders(<SettingsScreen />);

    fireEvent.press(view.getByRole('button', { name: 'Light' }));

    expect(usePreferencesStore.getState().appearance).toBe('light');
  });

  it('stores a supported accent', async () => {
    const view = await renderWithProviders(<SettingsScreen />);

    fireEvent.press(view.getByRole('button', { name: 'teal' }));

    expect(usePreferencesStore.getState().accent).toBe('teal');
  });

  it('stores the selected language on the device', async () => {
    const view = await renderWithProviders(<SettingsScreen />);
    fireEvent.press(view.getByRole('button', { name: 'हिन्दी' }));
    expect(usePreferencesStore.getState().locale).toBe('hi');
  });

  it('logs out through the existing session flow', async () => {
    const view = await renderWithProviders(<SettingsScreen />);
    fireEvent.press(view.getByRole('button', { name: 'Log out' }));
    expect(endSession).toHaveBeenCalledTimes(1);
  });
});
