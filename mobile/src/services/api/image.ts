import { apiClient } from './client';

export async function imageDataUri(path: string, signal?: AbortSignal): Promise<string | null> {
  try {
    const response = await apiClient.get<ArrayBuffer>(path, { responseType: 'arraybuffer', signal });
    const type = String(response.headers['content-type'] ?? 'image/jpeg').split(';')[0];
    const bytes = toBytes(response.data);
    if (bytes.length === 0 || !type.startsWith('image/')) {
      return null;
    }
    return `data:${type};base64,${encodeBase64(bytes)}`;
  } catch {
    return null;
  }
}

function toBytes(data: ArrayBuffer | Uint8Array | string): Uint8Array {
  if (typeof data === 'string') {
    const bytes = new Uint8Array(data.length);
    for (let index = 0; index < data.length; index += 1) {
      bytes[index] = data.charCodeAt(index) & 0xff;
    }
    return bytes;
  }
  return data instanceof Uint8Array ? data : new Uint8Array(data);
}

function encodeBase64(bytes: Uint8Array): string {
  let binary = '';
  const size = 0x8000;
  for (let index = 0; index < bytes.length; index += size) {
    binary += String.fromCharCode(...bytes.subarray(index, index + size));
  }
  return globalThis.btoa(binary);
}
