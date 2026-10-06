import { describe, expect, it } from '@jest/globals';
import axios from 'axios';
import { ApiError, fieldErrors, namedFieldErrors, toApiError, unmappedValidation } from './problem';

describe('toApiError', () => {
  it('maps ProblemDetails from an HTTP response', () => {
    const error = new axios.AxiosError('fail');
    error.response = {
      data: { title: 'Bad Request', status: 400, detail: 'Email is invalid', errors: { Email: ['Email is invalid'] } },
      status: 400,
      statusText: 'Bad Request',
      headers: {},
      config: { headers: axios.AxiosHeaders.from({}) },
    };

    const apiError = toApiError(error);

    expect(apiError).toBeInstanceOf(ApiError);
    expect(apiError.status).toBe(400);
    expect(apiError.message).toBe('Email is invalid');
    expect(fieldErrors(apiError.problem).Email).toBe('Email is invalid');
  });

  it('places validation errors on the matching field and hides the generic title', () => {
    const error = new ApiError('One or more validation errors occurred.', 400, {
      title: 'One or more validation errors occurred.',
      errors: { Height: ['Height must be between 1 and 300'], '$.goalType': ['Goal type is not valid.'] },
    });

    expect(namedFieldErrors(error)).toEqual({
      height: 'Height must be between 1 and 300',
      goalType: 'Goal type is not valid.',
    });
    expect(unmappedValidation(error, ['height', 'goalType'], 'offline')).toBeNull();
  });

  it('marks a missing response as offline', () => {
    const error = new axios.AxiosError('network');

    expect(toApiError(error).offline).toBe(true);
  });
});
