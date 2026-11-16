import { apiRequest } from './http';
import { liveQuery, type Unsubscribe } from './realtime';
import type { RoomHistoryEntry } from '../shared/types';

// The signed-in user's own room history and favourites. Every route is under
// /me, so there is no way to address anyone else's: the server enforces what
// the old Firestore security rules enforced per document.
//
// Attendance is no longer written from here. Joining a room records it on the
// server, so it can be neither forgotten nor forged.

interface HistoryEntryDto {
  roomId: string;
  roomName: string;
  pageUrl: string;
  favourite: boolean;
  lastAttendedAt: number | null;
  roomCreatedAt: number | null;
  lastReadAt: number | null;
}

interface HistoryChange {
  roomId: string;
  /** Null when the entry was removed. */
  entry: HistoryEntryDto | null;
}

function toEntry(dto: HistoryEntryDto): RoomHistoryEntry {
  return {
    roomId: dto.roomId,
    roomName: dto.roomName || 'Untitled room',
    pageUrl: dto.pageUrl,
    favourite: dto.favourite,
    lastAttendedAt: dto.lastAttendedAt,
    roomCreatedAt: dto.roomCreatedAt,
    lastReadAt: dto.lastReadAt,
  };
}

export function setFavourite(roomId: string, favourite: boolean): Promise<void> {
  return apiRequest<void>('PUT', `/me/history/${roomId}/favourite`, { body: { favourite } }).catch(() => undefined);
}

/**
 * Record how far this user has read a room's chat, as the epoch ms of the newest
 * message they have seen. It only ever moves forward, on the server, so a client
 * that is behind cannot un-read what another client already read.
 */
export function markRead(roomId: string, at: number): Promise<void> {
  return apiRequest<void>('PUT', `/me/history/${roomId}/read-position`, { body: { at } }).catch(() => undefined);
}

export function removeFromHistory(roomId: string): Promise<void> {
  return apiRequest<void>('DELETE', `/me/history/${roomId}`).catch(() => undefined);
}

/**
 * Live view of one history entry. The extension watches this so a read that
 * happens in the web dashboard settles the widget's unread badge, and vice versa.
 */
export function watchHistoryEntry(
  roomId: string,
  callback: (entry: RoomHistoryEntry | null) => void,
): Unsubscribe {
  return liveQuery<RoomHistoryEntry | null, HistoryChange>({
    topic: 'history',
    event: 'history',
    load: async () => {
      const dto = await apiRequest<HistoryEntryDto | null>('GET', `/me/history/${roomId}`, { allowNotFound: true });
      return dto ? toEntry(dto) : null;
    },
    applyPush: (_current, change) => {
      if (change.roomId !== roomId) return undefined;
      return change.entry ? toEntry(change.entry) : null;
    },
    emit: callback,
  });
}

function mostRecentlyAttendedFirst(left: RoomHistoryEntry, right: RoomHistoryEntry): number {
  return (right.lastAttendedAt ?? 0) - (left.lastAttendedAt ?? 0);
}

/**
 * Folds a change into the list. An entry that only carries a read position (never
 * attended) stays out of the list, as it did in the Firebase build.
 */
function applyHistoryChange(entries: RoomHistoryEntry[], change: HistoryChange): RoomHistoryEntry[] {
  const others = entries.filter((entry) => entry.roomId !== change.roomId);
  const changed = change.entry ? toEntry(change.entry) : null;

  if (!changed || changed.lastAttendedAt === null) return others;
  return [changed, ...others].sort(mostRecentlyAttendedFirst);
}

/** Live history for the signed-in user, most recently attended first. */
export function watchHistory(callback: (entries: RoomHistoryEntry[]) => void, max = 200): Unsubscribe {
  return liveQuery<RoomHistoryEntry[], HistoryChange>({
    topic: 'history',
    event: 'history',
    load: async () => {
      const dtos = await apiRequest<HistoryEntryDto[]>('GET', `/me/history?limit=${max}`);
      return dtos.map(toEntry);
    },
    applyPush: applyHistoryChange,
    emit: callback,
  });
}
