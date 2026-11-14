import { apiRequest } from './http';
import { liveQuery, type Unsubscribe } from './realtime';
import { ROOM_PAGE_SIZE, ROOM_STALE_MS } from '../shared/constants';
import {
  generateRoomKey,
  generateSalt,
  deriveKeyFromPassphrase,
  makeVerifier,
} from '../shared/crypto';
import type { Room, UserProfile, RoomVisibility } from '../shared/types';

export type { Unsubscribe };

export interface CreateRoomInput {
  name: string;
  pageUrl: string;
  selector: string;
  frameOrigin: string;
  currentTime: number;
  owner: UserProfile;
  visibility: RoomVisibility;
  /** Required for private rooms; never sent anywhere, only used to derive the key. */
  passphrase?: string;
}

/**
 * Build the chat key material for a room, in the browser.
 *  • public  — random key, stored with the room.
 *  • private — key derived from the passphrase; only the salt and a verifier
 *    are sent, so the key itself never reaches the server.
 */
async function buildChatFields(input: CreateRoomInput) {
  if (input.visibility === 'private') {
    const passphrase = input.passphrase?.trim();
    if (!passphrase) throw new Error('A passphrase is required for a private room.');

    const chatSalt = generateSalt();
    const key = await deriveKeyFromPassphrase(passphrase, chatSalt);
    return { chatKey: null, chatSalt, chatCheck: await makeVerifier(key) };
  }

  return { chatKey: generateRoomKey(), chatSalt: null, chatCheck: null };
}

/** Create a new active room owned by the signed-in user. Returns the new room id. */
export async function createRoom(input: CreateRoomInput): Promise<string> {
  const chatFields = await buildChatFields(input);
  const created = await apiRequest<{ id: string }>('POST', '/rooms', {
    body: {
      name: input.name.trim(),
      pageUrl: input.pageUrl,
      selector: input.selector,
      frameOrigin: input.frameOrigin,
      currentTime: input.currentTime || 0,
      visibility: input.visibility,
      ...chatFields,
    },
  });
  return created.id;
}

export function getRoom(id: string): Promise<Room | null> {
  return apiRequest<Room | null>('GET', `/rooms/${id}`, { allowNotFound: true });
}

function newestFirst(left: Room, right: Room): number {
  return (right.createdAt ?? 0) - (left.createdAt ?? 0);
}

/** Puts a pushed room into the list: replace it if present, add it if new, keep the newest page. */
function mergeRoom(rooms: Room[], pushed: Room): Room[] {
  const others = rooms.filter((room) => room.id !== pushed.id);
  const merged = [pushed, ...others].sort(newestFirst);
  return merged.slice(0, ROOM_PAGE_SIZE);
}

/**
 * Live list of recent rooms, newest first: both live and ended. The UI splits
 * them with `isRoomLive`, so an ended room shows up under History instead of
 * lingering in the active list.
 *
 * `onError` matters: a silently empty list is indistinguishable from "there are
 * genuinely no rooms".
 */
export function watchRooms(callback: (rooms: Room[]) => void, onError?: (error: Error) => void): Unsubscribe {
  return liveQuery<Room[], Room>({
    topic: 'rooms',
    event: 'room',
    load: () => apiRequest<Room[]>('GET', `/rooms?limit=${ROOM_PAGE_SIZE}`),
    applyPush: mergeRoom,
    emit: callback,
    onError,
  });
}

/** Live subscription to a single room. The callback receives null if it does not exist. */
export function watchRoom(id: string, callback: (room: Room | null) => void): Unsubscribe {
  return liveQuery<Room | null, Room>({
    topic: `room:${id}`,
    event: 'room',
    load: () => getRoom(id),
    applyPush: (_current, pushed) => (pushed.id === id ? pushed : undefined),
    emit: callback,
  });
}

function activityMs(room: Room): number {
  return room.lastActiveAt ?? room.createdAt ?? 0;
}

/**
 * Reference "now" for room liveness: the freshest heartbeat any room has written,
 * which is at most one ROOM_TOUCH_MS old and so a good stand-in for the server's
 * clock. Server time is compared to server time, never to the local clock
 * (DECISIONS.md #6).
 */
export function roomsRef(rooms: Room[]): number {
  let freshest = 0;
  for (const room of rooms) freshest = Math.max(freshest, activityMs(room));
  return freshest || Date.now();
}

/** A room is live if it wasn't closed AND its heartbeat is recent. */
export function isRoomLive(room: Room, ref: number): boolean {
  if (room.isActive === false) return false;
  return ref - activityMs(room) < ROOM_STALE_MS;
}

/**
 * Host-only: point the room at a different video. The server resets playback to
 * the new position so viewers don't inherit a timestamp from the old one.
 */
export function updateRoomVideo(
  roomId: string,
  video: { pageUrl: string; selector: string; frameOrigin: string; currentTime: number },
): Promise<void> {
  return apiRequest<void>('PUT', `/rooms/${roomId}/video`, {
    body: { ...video, currentTime: video.currentTime || 0 },
  });
}

/**
 * Host-only: the room's liveness heartbeat, and where the host's playhead is now.
 *
 * `hostPosition` is kept apart from `playback` on purpose: sync is event-driven
 * (DECISIONS.md #10), and a periodic position folded into playback would yank
 * viewers around on a timer.
 */
export function touchRoom(
  roomId: string,
  position?: { currentTime: number; isPlaying: boolean } | null,
): Promise<void> {
  return apiRequest<void>('POST', `/rooms/${roomId}/touch`, { body: { position: position ?? null } });
}

/** Host-only: push the latest playback state. The server stamps the time. */
export function writePlayback(
  roomId: string,
  state: { isPlaying: boolean; currentTime: number; rate: number },
): Promise<void> {
  return apiRequest<void>('PUT', `/rooms/${roomId}/playback`, { body: state });
}

/**
 * Deliberately hand the room to someone watching. Moves the persistent claim too,
 * so the previous host won't reclaim it on their next visit.
 */
export function transferOwnership(roomId: string, targetUid: string): Promise<void> {
  return apiRequest<void>('POST', `/rooms/${roomId}/ownership-transfers`, { body: { targetUid } });
}
