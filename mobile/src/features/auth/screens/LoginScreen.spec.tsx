import { describe, expect, it, jest } from '@jest/globals';
import { act, fireEvent, waitFor } from '@testing-library/react-native';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import type { AuthStackParamList } from '../../../app/navigation/types';
import { renderWithProviders } from '../../../test/render';
import { signIn } from '../../../services/auth/session';
import { LoginScreen } from './LoginScreen';

jest.mock('../../../services/auth/session', () => ({
  signIn: jest.fn(),
}));

const navigation = { navigate: jest.fn() } as unknown as NativeStackScreenProps<AuthStackParamList, 'Login'>['navigation'];
const route = { key: 'Login', name: 'Login' } as NativeStackScreenProps<AuthStackParamList, 'Login'>['route'];

describe('LoginScreen', () => {
  it('shows a validation error and does not submit an empty form', async () => {
    const view = await renderWithProviders(<LoginScreen navigation={navigation} route={route} />);

    await act(async () => {
      fireEvent.press(view.getByRole('button', { name: 'Sign in' }));
    });

    expect(view.getByText('Invalid email address')).toBeTruthy();
    expect(signIn).not.toHaveBeenCalled();

    fireEvent.press(view.getByTestId('create-account'));
    expect(navigation.navigate).toHaveBeenCalledWith('Register');
  });

  it('submits valid credentials', async () => {
    jest.mocked(signIn).mockResolvedValue(undefined);
    const view = await renderWithProviders(<LoginScreen navigation={navigation} route={route} />);

    fireEvent.changeText(view.getByLabelText('Email'), 'ada@example.com');
    fireEvent.changeText(view.getByLabelText('Password'), 'secret');
    fireEvent.press(view.getByRole('button', { name: 'Sign in' }));

    await waitFor(() => expect(signIn).toHaveBeenCalledWith({ email: 'ada@example.com', password: 'secret' }));
  });

});
