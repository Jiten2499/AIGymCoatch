import { useQuery } from '@tanstack/react-query';
import { imageDataUri } from '../../services/api/image';
import { AppAvatar } from './AppAvatar';

export function MemberPhoto({ label, path, size }: { label: string; path: string | null; size?: number }) {
  const photo = useQuery({
    queryKey: ['auth-image', path],
    enabled: Boolean(path),
    retry: false,
    queryFn: ({ signal }) => imageDataUri(path ?? '', signal),
  });
  return <AppAvatar label={label} size={size} uri={photo.data} />;
}
