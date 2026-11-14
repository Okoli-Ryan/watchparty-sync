import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { fakeHubs, fakeSignalRModule, settle } from './testing/fakeHub';
import { fakeFetch, signIn } from './testing/fakeApi';
import { STALE_MS } from '../shared/constants';
import type { Member } from '../shared/types';

vi.mock('@microsoft/signalr', () => fakeSignalRModule());

const { watchMembers, serverNow, isFresh, freshnessRef } = await import('./presence');

function member(uid: string, lastSeen: number): Member {
  return { uid, displayName: uid, role: 'viewer', joinedAt: lastSeen, lastSeen };
}

describe('presence', () => {
  beforeEach(async () => {
    await signIn();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("calibrates to the server's clock from each members snapshot", async () => {
    // The server's clock is ten minutes ahead of this machine's.
    const serverAhead = Date.now() + 10 * 60 * 1000;
    fakeFetch(() => ({ status: 200, body: { roomId: 'r1', members: [], serverTime: serverAhead } }));

    const stop = watchMembers('r1', () => undefined);
    await settle();

    expect(Math.abs(serverNow() - serverAhead)).toBeLessThan(1_000);
    stop();
  });

  it('ages members against the server clock, never the local one', async () => {
    const serverTime = Date.now() + 10 * 60 * 1000;
    const snapshot = {
      roomId: 'r1',
      members: [member('fresh', serverTime - 1_000), member('stale', serverTime - STALE_MS - 1_000)],
      serverTime,
    };
    fakeFetch(() => ({ status: 200, body: snapshot }));
    let latest: Member[] = [];

    const stop = watchMembers('r1', (members) => (latest = members));
    await settle();
    const reference = freshnessRef(latest);
    const present = latest.filter((candidate) => isFresh(candidate, reference)).map((candidate) => candidate.uid);

    // Compared with the LOCAL clock both would look ten minutes in the future: "fresh".
    expect(present).toEqual(['fresh']);
    stop();
  });

  it('applies a pushed snapshot for its own room only', async () => {
    const now = Date.now();
    fakeFetch(() => ({ status: 200, body: { roomId: 'r1', members: [], serverTime: now } }));
    let latest: Member[] = [];

    const stop = watchMembers('r1', (members) => (latest = members));
    await settle();
    const hub = fakeHubs.at(-1)!;
    hub.push('members', { roomId: 'other', members: [member('x', now)], serverTime: now });
    hub.push('members', { roomId: 'r1', members: [member('host', now)], serverTime: now });

    expect(latest.map((candidate) => candidate.uid)).toEqual(['host']);
    stop();
  });
});
