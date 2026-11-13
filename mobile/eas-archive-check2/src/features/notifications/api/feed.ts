import type { MemberCommunication } from '../../communications/types';
import type { AppNotification, FeedItem } from '../types';

export function toNotificationFeedItem(item: AppNotification): FeedItem {
  return {
    kind: 'notification',
    id: item.id,
    title: item.title,
    message: item.message,
    isRead: item.isRead,
    occurredAt: item.createdAt,
    notificationType: item.type,
  };
}

export function toCircularFeedItem(item: MemberCommunication): FeedItem {
  return {
    kind: 'circular',
    id: item.id,
    title: item.title,
    message: item.message,
    isRead: item.isRead,
    occurredAt: item.publishAt,
    circularType: item.type,
    priority: item.priority,
    createdBy: item.createdBy ?? null,
    hasAttachment: Boolean(item.imageStorageKey),
  };
}

export function mergeNotificationFeed(
  notifications: readonly AppNotification[],
  circulars: readonly MemberCommunication[],
): FeedItem[] {
  return [
    ...notifications.map(toNotificationFeedItem),
    ...circulars.map(toCircularFeedItem),
  ].sort((left, right) => Date.parse(right.occurredAt) - Date.parse(left.occurredAt));
}
