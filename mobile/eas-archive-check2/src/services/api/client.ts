import axios, { type AxiosError, type InternalAxiosRequestConfig } from 'axios';
import { createCorrelationId } from './correlation';
import { getAuthBridge } from './authBridge';
import { apiRoot } from './config';
import { toApiError } from './problem';

type RetriableConfig = InternalAxiosRequestConfig & { retried?: boolean };

export const apiClient = axios.create({
  baseURL: apiRoot,
  timeout: 15_000,
  headers: { Accept: 'application/json' },
});

export const refreshClient = axios.create({
  baseURL: apiRoot,
  timeout: 15_000,
  headers: { Accept: 'application/json' },
});

apiClient.interceptors.request.use((config) => {
  config.headers.set('X-Correlation-ID', createCorrelationId());
  const token = getAuthBridge().getAccessToken();
  if (token) {
    config.headers.set('Authorization', `Bearer ${token}`);
  }
  return config;
});

apiClient.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const config = error.config as RetriableConfig | undefined;
    const status = error.response?.status;
    const path = config?.url ?? '';
    const skipRefresh = path.includes('/auth/');
    if (status === 401 && config && !config.retried && !skipRefresh) {
      config.retried = true;
      const next = await getAuthBridge().refresh();
      if (next) {
        config.headers.set('Authorization', `Bearer ${next}`);
        return apiClient.request(config);
      }
      getAuthBridge().onSessionExpired();
    }
    const apiError = toApiError(error);
    const missingPhoto = status === 404 && /\/(photo|profile-photo)$/.test(path);
    if (typeof __DEV__ !== 'undefined' && __DEV__ && process.env.NODE_ENV !== 'test' && !missingPhoto) {
      console.warn(`API ${config?.method ?? 'request'} ${config?.baseURL ?? ''}${path} failed: ${apiError.message}`);
    }
    return Promise.reject(apiError);
  },
);
