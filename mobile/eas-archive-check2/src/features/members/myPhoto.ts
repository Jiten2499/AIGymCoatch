import { useQuery } from '@tanstack/react-query';
import { imageDataUri } from '../../services/api/image';
import { useSessionStore } from '../../store/sessionStore';

export function useMyMemberPhoto() {
  const signedIn = useSessionStore((state) => state.user !== null);
  const tenantId = useSessionStore((state) => state.currentTenant?.id ?? '');
  const photo = useQuery({
    queryKey: ['my-member-photo', tenantId],
    enabled: signedIn,
    retry: false,
    queryFn: ({ signal }) => imageDataUri('/members/me/profile-photo', signal),
  });
  return photo.data ?? null;
}
