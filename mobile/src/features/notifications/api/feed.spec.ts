import { describe, expect, it } from '@jest/globals';
import { mergeNotificationFeed } from './feed';

describe('mergeNotificationFeed', () => {
  it('merges system notifications and circulars without duplicating ids across kinds', () => {
    const feed = mergeNotificationFeed(
      [{ id: 'n1', type: 'ChallengeReminder', title: 'Joined', message: 'Steps', isRead: false, createdAt: '2026-10-08T10:00:00Z' }],
      [{
        id: 'c1',
        type: 'Announcement',
        title: 'Holiday hours',
        message: 'Closing early',
        priority: 'Normal',
        publishAt: '2026-10-08T11:00:00Z',
        expiresAt: null,
        isRead: false,
        imageStorageKey: 'img',
        createdBy: 'Owner',
      }],
    );

    expect(feed.map((item) => `${item.kind}:${item.id}`)).toEqual(['circular:c1', 'notification:n1']);
    expect(feed[0]).toMatchObject({ kind: 'circular', hasAttachment: true, createdBy: 'Owner' });
    expect(feed[1]).toMatchObject({ kind: 'notification', notificationType: 'ChallengeReminder' });
  });
});
