import { describe, expect, it, jest } from '@jest/globals';
import { fireEvent } from '@testing-library/react-native';
import { renderWithProviders } from '../../test/render';
import { AppButton } from './AppButton';

describe('AppButton', () => {
  it('calls onPress when the control is enabled', async () => {
    const onPress = jest.fn();
    const view = await renderWithProviders(<AppButton label="Save" onPress={onPress} />);

    fireEvent.press(view.getByRole('button', { name: 'Save' }));

    expect(onPress).toHaveBeenCalledTimes(1);
  });

  it('does not call onPress while loading', async () => {
    const onPress = jest.fn();
    const view = await renderWithProviders(<AppButton label="Save" loading onPress={onPress} />);

    fireEvent.press(view.getByRole('button', { name: 'Save' }));

    expect(onPress).not.toHaveBeenCalled();
  });
});
