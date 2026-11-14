import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { fakeHubs, fakeSignalRModule, settle } from './testing/fakeHub';
import { fakeFetch, signIn } from './testing/fakeApi';

vi.mock('@microsoft/signalr', () => fakeSignalRModule());

const { watchRoom, watchRooms } = await import('./rooms');
const { watchMessages } = await import('./chat');

function room(id: string, overrides: Record<string, unknown> = {}) {
  return {
    id,
    name: `Room ${id}`,
    createdAt: 1_000,
    lastActiveAt: 1_000,
    isActive: true,
    playback: { isPlaying: false, currentTime: 0, rate: 1, updatedAt: null, updatedBy: 'host' },
    ...overrides,
  };
}

describe('live queries over the realtime hub', () => {
  beforeEach(async () => {
    await signIn();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('subscribes to the topic, loads current state, then applies pushes', async () => {
    fakeFetch(() => ({ status: 200, body: room('a') }));
    const received: unknown[] = [];

    const stop = watchRoom('a', (value) => received.push(value));
    await settle();

    const hub = fakeHubs.at(-1)!;
    expect(hub.subscribedTopics()).toContain('room:a');
    expect(received).toEqual([room('a')]);

    hub.push('room', room('a', { isActive: false }));
    expect(received.at(-1)).toMatchObject({ id: 'a', isActive: false });

    stop();
  });

  it('ignores pushes about other rooms', async () => {
    fakeFetch(() => ({ status: 200, body: room('a') }));
    const received: unknown[] = [];

    const stop = watchRoom('a', (value) => received.push(value));
    await settle();
    fakeHubs.at(-1)!.push('room', room('b'));

    expect(received).toHaveLength(1);
    stop();
  });

  it('reloads from scratch after a reconnect instead of trusting missed pushes', async () => {
    let version = 1;
    const requests = fakeFetch(() => ({ status: 200, body: room('a', { name: `v${version}` }) }));
    const received: Array<{ name: string }> = [];

    const stop = watchRoom('a', (value) => received.push(value as { name: string }));
    await settle();

    version = 2;
    fakeHubs.at(-1)!.simulateReconnect();
    await settle();

    expect(requests.filter((request) => request.url.endsWith('/rooms/a'))).toHaveLength(2);
    expect(received.at(-1)!.name).toBe('v2');
    stop();
  });

  it('merges a pushed room into the list, newest first', async () => {
    fakeFetch(() => ({ status: 200, body: [room('old', { createdAt: 1_000 })] }));
    let latest: Array<{ id: string }> = [];

    const stop = watchRooms((rooms) => (latest = rooms));
    await settle();
    fakeHubs.at(-1)!.push('room', room('new', { createdAt: 2_000 }));
    fakeHubs.at(-1)!.push('room', room('old', { createdAt: 1_000, isActive: false }));

    expect(latest.map((item) => item.id)).toEqual(['new', 'old']);
    expect(latest).toHaveLength(2);
    stop();
  });

  it('does not deliver after the subscriber has stopped', async () => {
    fakeFetch(() => ({ status: 200, body: room('a') }));
    const received: unknown[] = [];

    const stop = watchRoom('a', (value) => received.push(value));
    await settle();
    stop();
    fakeHubs.at(-1)!.push('room', room('a', { name: 'late' }));

    expect(received).toHaveLength(1);
  });

  it('appends pushed chat messages once each, in time order', async () => {
    const encrypted = (id: string, createdAt: number) => ({
      id,
      roomId: 'a',
      senderUid: 'host',
      senderName: 'Host',
      iv: 'AAAAAAAAAAAAAAAA',
      ct: 'AAAA',
      createdAt,
    });
    fakeFetch(() => ({ status: 200, body: [encrypted('m1', 1)] }));
    const key = await crypto.subtle.generateKey({ name: 'AES-GCM', length: 256 }, false, ['encrypt', 'decrypt']);
    let latest: Array<{ id: string; text: string }> = [];

    const stop = watchMessages('a', key, (messages) => (latest = messages));
    await settle();
    fakeHubs.at(-1)!.push('message', encrypted('m3', 3));
    fakeHubs.at(-1)!.push('message', encrypted('m2', 2));
    fakeHubs.at(-1)!.push('message', encrypted('m2', 2));
    await settle();

    expect(latest.map((message) => message.id)).toEqual(['m1', 'm2', 'm3']);
    // Not real ciphertext: shown as a placeholder, never dropped.
    expect(latest[0].text).toContain('could not decrypt');
    stop();
  });
});
