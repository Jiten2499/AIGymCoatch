import axios from 'axios';
import type { ProblemDetails } from '../../types/api';

export class ApiError extends Error {
  readonly status: number;
  readonly problem: ProblemDetails | null;
  readonly offline: boolean;

  constructor(message: string, status: number, problem: ProblemDetails | null, offline = false) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.problem = problem;
    this.offline = offline;
  }
}

export function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) {
    return error;
  }

  if (!axios.isAxiosError(error)) {
    return new ApiError('Request failed', 0, null);
  }

  if (!error.response) {
    return new ApiError('Network request failed', 0, null, true);
  }

  const data = error.response.data;
  const problem = isProblem(data) ? data : null;
  const message = problem?.detail ?? problem?.title ?? 'Request failed';
  return new ApiError(message, error.response.status, problem);
}

export function isNotFound(error: unknown): boolean {
  return toApiError(error).status === 404;
}

export function errorText(error: unknown, offline: string): string {
  const apiError = toApiError(error);
  return apiError.offline ? offline : apiError.message;
}

export function fieldErrors(problem: ProblemDetails | null): Record<string, string> {
  if (!problem?.errors) {
    return {};
  }

  return Object.fromEntries(
    Object.entries(problem.errors).map(([key, messages]) => [key, messages[0] ?? 'Invalid value']),
  );
}

export function namedFieldErrors(error: unknown): Record<string, string> {
  const result: Record<string, string> = {};
  for (const [key, message] of Object.entries(fieldErrors(toApiError(error).problem))) {
    const segment = key.replace(/^\$\./, '').split('.').filter(Boolean).at(-1) ?? 'request';
    const name = segment.charAt(0).toLowerCase() + segment.slice(1);
    result[name] = message;
  }
  return result;
}

export function unmappedValidation(error: unknown, attached: readonly string[], offline: string): string | null {
  const apiError = toApiError(error);
  if (apiError.offline) {
    return offline;
  }

  const fields = namedFieldErrors(error);
  const leftover = Object.entries(fields).filter(([key]) => !attached.includes(key));
  if (leftover.length > 0) {
    return leftover.map(([, message]) => message).join('\n');
  }

  if (Object.keys(fields).length > 0) {
    return null;
  }

  return apiError.message === 'One or more validation errors occurred.' ? null : apiError.message;
}

function isProblem(value: unknown): value is ProblemDetails {
  return typeof value === 'object' && value !== null && ('detail' in value || 'title' in value || 'errors' in value);
}
