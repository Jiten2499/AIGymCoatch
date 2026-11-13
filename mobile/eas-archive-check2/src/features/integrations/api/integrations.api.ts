import { apiClient } from '../../../services/api/client';
import { tenantKey } from '../../fitness/api/keys';

export type StorageConnection = {
  provider: string;
  status: string;
  accountEmail: string | null;
  rootFolderId: string | null;
  connectedAt: string | null;
  lastValidatedAt: string | null;
  lastError: string | null;
  canConnect: boolean;
};

export const integrationsQueryKey = (tenantId: string) => tenantKey(tenantId, 'integrations', 'google-drive');

export async function getGoogleDriveConnection(signal?: AbortSignal) {
  const response = await apiClient.get<StorageConnection>('/integrations/google-drive', { signal });
  return response.data;
}

export async function beginGoogleDriveConnection() {
  const response = await apiClient.post<{ authorizationUrl: string }>('/integrations/google-drive/connect', {
    clientKind: 'Mobile',
    returnPath: 'growfit://integrations/google-drive',
  });
  return response.data;
}

export async function testGoogleDriveConnection() {
  const response = await apiClient.post<StorageConnection>('/integrations/google-drive/test');
  return response.data;
}

export async function disconnectGoogleDrive() {
  const response = await apiClient.post<StorageConnection>('/integrations/google-drive/disconnect');
  return response.data;
}
