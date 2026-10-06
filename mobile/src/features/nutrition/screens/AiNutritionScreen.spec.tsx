import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { fireEvent } from '@testing-library/react-native';
import { ApiError } from '../../../services/api/problem';
import { useSessionStore } from '../../../store/sessionStore';
import { renderWithProviders } from '../../../test/render';
import { nutritionAdvice } from '../../aiCoach/api/ai.api';
import { listFeatureFlags } from '../../licensing/api/licenses.api';
import { getNutritionSummary } from '../api/nutrition.api';
import { AiNutritionScreen } from './AiNutritionScreen';

jest.mock('../api/nutrition.api', () => ({ getNutritionSummary: jest.fn() }));
jest.mock('../../aiCoach/api/ai.api', () => ({ nutritionAdvice: jest.fn() }));
jest.mock('../../licensing/api/licenses.api', () => ({ listFeatureFlags: jest.fn() }));

describe('AiNutritionScreen', () => {
  beforeEach(() => {
    useSessionStore.setState({
      status: 'authenticated', accessToken: 'token', refreshToken: 'refresh', expiresAt: '2099-01-01T00:00:00Z',
      user: { id: 'user-1', email: 'ada@example.com', firstName: 'Ada', lastName: 'Lovelace' },
      currentTenant: { id: 'tenant-a', name: 'Ada Personal', type: 'Personal', status: 'Active' },
    });
    jest.mocked(nutritionAdvice).mockReset();
    jest.mocked(getNutritionSummary).mockResolvedValue({ date: '2026-10-03', calories: 500, protein: 30, carbohydrates: 40, fat: 10, calorieTarget: 2000, proteinTarget: 120, carbTarget: 180, fatTarget: 50 });
  });

  it('blocks guidance when the feature is disabled', async () => {
    jest.mocked(listFeatureFlags).mockResolvedValue([{ featureCode: 'AI_NUTRITION', enabled: false }, { featureCode: 'NUTRITION', enabled: true }]);
    const view = await renderWithProviders(<AiNutritionScreen />);
    expect(await view.findByText('This feature is not enabled for the current workspace.')).toBeTruthy();
    expect(nutritionAdvice).not.toHaveBeenCalled();
  });

  it('keeps measured intake separate from the AI message', async () => {
    jest.mocked(listFeatureFlags).mockResolvedValue([{ featureCode: 'AI_NUTRITION', enabled: true }, { featureCode: 'NUTRITION', enabled: true }]);
    jest.mocked(nutritionAdvice).mockResolvedValue({ conversationId: 'c1', message: 'Keep the same lunch tomorrow.', inputTokens: 1, outputTokens: 1 });
    const view = await renderWithProviders(<AiNutritionScreen />);
    expect(await view.findByText('Calories: 500')).toBeTruthy();
    expect(view.getByText('AI-generated fitness guidance is for general wellness and does not replace professional medical advice.')).toBeTruthy();
    expect(view.getByText('LifeFit AI is not a medical diagnosis system.')).toBeTruthy();
    await fireEvent.press(view.getByRole('button', { name: 'Ask' }));
    expect(await view.findByText('Keep the same lunch tomorrow.')).toBeTruthy();
    expect(view.getByText('AI insight')).toBeTruthy();
  });

  it('shows an error when guidance fails', async () => {
    jest.mocked(listFeatureFlags).mockResolvedValue([{ featureCode: 'AI_NUTRITION', enabled: true }, { featureCode: 'NUTRITION', enabled: true }]);
    jest.mocked(nutritionAdvice).mockRejectedValue(new ApiError('Network request failed', 0, null, true));
    const view = await renderWithProviders(<AiNutritionScreen />);
    await fireEvent.press(await view.findByRole('button', { name: 'Ask' }));
    expect(await view.findByText('You appear to be offline.')).toBeTruthy();
  });
});
