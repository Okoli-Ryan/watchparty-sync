import { apiRequest } from './http';
import { liveQuery, type Unsubscribe } from './realtime';
import { STALE_MS } from '../shared/constants';
import type { Member, MemberRole } from '../shared/types';

interface MembersSnapshot {
  roomId: string;
  members: Member[];
  /** The server's clock when the list was read. */
  serverTime: number;
}

/**
 * Join (or rejoin) a room as the signed-in user. Returns the role the SERVER
 * decided: a returning primary owner gets the room back here, on the server, so
 * the client no longer writes that handoff itself.
 */
export async function joinRoom(roomId: string): Promise<MemberRole> {
  const joined = await apiRequest<{ role: MemberRole }>('PUT', `/rooms/${roomId}/members/me`);
  return joined.role;
}

/**
 * One heartbeat. Call on an interval while attached. The server also uses it to
 * prune members who have gone quiet and to hand a hostless room to its heir, which
 * every client used to work out for itself.
 */
export function heartbeat(roomId: string): Promise<void> {
  return apiRequest<void>('POST', `/rooms/${roomId}/members/me/heartbeat`).catch(() => undefined);
}

/** Leave a room. If you were the last one there, the server ends it. */
export function leaveRoom(roomId: string): Promise<void> {
  return apiRequest<void>('DELETE', `/rooms/${roomId}/members/me`).catch(() => undefined);
}

/**
 * Estimated offset between server time and this machine's clock, in ms.
 * `serverNow() ≈ Date.now() + clockOffset`.
 *
 * Heartbeats are stamped with the server's clock, and comparing them with the
 * local clock is what caused the Firebase build's false-stale bugs
 * (DECISIONS.md #6). Every members snapshot now carries the server's own time,
 * so the offset is measured directly instead of being inferred from heartbeats.
 */
let clockOffset = 0;

function calibrateClock(serverTime: number): void {
  clockOffset = serverTime - Date.now();
}

/** Best estimate of the server's current time. */
export function serverNow(): number {
  return Date.now() + clockOffset;
}

/** Reference "now" for freshness checks: the estimated server clock. */
export function freshnessRef(_members?: Member[]): number {
  return serverNow();
}

/**
 * A member counts as present if its heartbeat isn't far behind `ref`, the
 * estimated server clock, so a room that goes quiet all at once still ages out.
 */
export function isFresh(member: Member, ref: number): boolean {
  if (member.lastSeen === null) return true;
  return ref - member.lastSeen <= STALE_MS;
}

/**
 * Subscribe to a room's members.
 *
 * The second callback argument is kept for the Firebase build's signature, where it
 * meant "this snapshot came from the local cache". The server's list is always
 * complete, so it is always false.
 */
export function watchMembers(
  roomId: string,
  callback: (members: Member[], fromCache: boolean) => void,
): Unsubscribe {
  return liveQuery<MembersSnapshot, MembersSnapshot>({
    topic: `members:${roomId}`,
    event: 'members',
    load: () => apiRequest<MembersSnapshot>('GET', `/rooms/${roomId}/members`),
    applyPush: (_current, pushed) => (pushed.roomId === roomId ? pushed : undefined),
    emit: (snapshot) => {
      calibrateClock(snapshot.serverTime);
      callback(snapshot.members, false);
    },
  });
}
