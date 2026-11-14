import { afterAll, beforeAll, describe, expect, it } from 'vitest';
import { login, logout } from '../../src/backend/auth';
import { sendMessage, watchMessages } from '../../src/backend/chat';
import { API_ROOT } from '../../src/backend/config';
import { watchHistory } from '../../src/backend/history';
import { joinRoom, leaveRoom, watchMembers } from '../../src/backend/presence';
import { createRoom, getRoom, watchRoom, writePlayback } from '../../src/backend/rooms';
import { importRoomKey } from '../../src/shared/crypto';
import type { ChatMessage, Member, Room, RoomHistoryEntry, UserProfile } from '../../src/shared/types';

const hostEmail = requireEnvironment('E2E_HOST_EMAIL');
const viewerEmail = requireEnvironment('E2E_VIEWER_EMAIL');
const password = requireEnvironment('E2E_PASSWORD');

function requireEnvironment(name: string): string {
  const value = process.env[name];
  if (!value) throw new Error(`Set ${name} to run the end-to-end suite.`);
  return value;
}

/** Resolves with the first value a live query delivers that passes `isWanted`. */
function firstMatching<T>(
  subscribe: (deliver: (value: T) => void) => () => void,
  isWanted: (value: T) => boolean,
  timeoutMs = 10_000,
): Promise<T> {
  return new Promise((resolve, reject) => {
    let stop: (() => void) | undefined;
    const timer = setTimeout(() => {
      stop?.();
      reject(new Error('Timed out waiting for a realtime update.'));
    }, timeoutMs);

    stop = subscribe((value) => {
      if (!isWanted(value)) return;
      clearTimeout(timer);
      stop?.();
      resolve(value);
    });
  });
}

/** The second participant, over plain HTTP: the client modules hold one session per process. */
async function viewerRequest(method: string, path: string, token: string, body?: unknown) {
  const response = await fetch(`${API_ROOT}${path}`, {
    method,
    headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  return response;
}

describe('the extension client against a running API', () => {
  let host: UserProfile;
  let viewerToken: string;
  let viewerId: string;
  let roomId: string;

  beforeAll(async () => {
    host = await login(hostEmail, password);

    const viewerLogin = await fetch(`${API_ROOT}/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email: viewerEmail, password }),
    });
    const viewer = (await viewerLogin.json()) as { accessToken: string; user: UserProfile };
    viewerToken = viewer.accessToken;
    viewerId = viewer.user.uid;
  });

  afterAll(async () => {
    await logout();
  });

  it('signs in and creates a public room the host owns', async () => {
    roomId = await createRoom({
      name: 'End-to-end room',
      pageUrl: 'https://videos.example.com/watch/e2e',
      selector: 'video#player',
      frameOrigin: '',
      currentTime: 0,
      owner: host,
      visibility: 'public',
    });

    expect(await joinRoom(roomId)).toBe('owner');
    const room = await getRoom(roomId);
    expect(room?.ownerUid).toBe(host.uid);
    expect(room?.chatKey).not.toBe('');
  });

  it('pushes a playback change to a subscriber over SignalR', async () => {
    const pushed = firstMatching<Room | null>(
      (deliver) => watchRoom(roomId, deliver),
      (room) => room?.playback.isPlaying === true,
    );

    // Give the subscription a moment to register before the write it should hear.
    await new Promise((resolve) => setTimeout(resolve, 1_000));
    await writePlayback(roomId, { isPlaying: true, currentTime: 42, rate: 1 });

    const room = await pushed;
    expect(room?.playback.currentTime).toBe(42);
    expect(room?.playback.updatedAt).toBeGreaterThan(0);
  });

  it('round-trips an encrypted chat message', async () => {
    const room = await getRoom(roomId);
    const key = await importRoomKey(room!.chatKey);

    const received = firstMatching<ChatMessage[]>(
      (deliver) => watchMessages(roomId, key, deliver),
      (messages) => messages.some((message) => message.text === 'hello from the e2e suite'),
    );

    await new Promise((resolve) => setTimeout(resolve, 1_000));
    await sendMessage(roomId, key, 'hello from the e2e suite');

    const messages = await received;
    const sent = messages.find((message) => message.text === 'hello from the e2e suite');
    expect(sent?.senderUid).toBe(host.uid);
  });

  it('shows a second viewer in the members list', async () => {
    const joined = await viewerRequest('PUT', `/rooms/${roomId}/members/me`, viewerToken);
    expect(joined.status).toBe(200);
    expect(await joined.json()).toEqual({ role: 'viewer' });

    const members = await firstMatching<Member[]>(
      (deliver) => watchMembers(roomId, deliver),
      (list) => list.length === 2,
    );
    expect(members.map((member) => member.uid).sort()).toEqual([host.uid, viewerId].sort());
  });

  it("lists the room in the host's own history", async () => {
    const history = await firstMatching<RoomHistoryEntry[]>(
      (deliver) => watchHistory(deliver),
      (entries) => entries.some((entry) => entry.roomId === roomId),
    );

    expect(history.find((entry) => entry.roomId === roomId)?.roomName).toBe('End-to-end room');
  });

  it('hands the room to the viewer the moment the host leaves', async () => {
    const handedOff = firstMatching<Room | null>(
      (deliver) => watchRoom(roomId, deliver),
      (room) => room?.ownerUid === viewerId,
    );

    await new Promise((resolve) => setTimeout(resolve, 1_000));
    await leaveRoom(roomId);

    const room = await handedOff;
    expect(room?.primaryOwnerUid).toBe(host.uid);
    expect(room?.isActive).toBe(true);
  });

  it('stores only a salt and verifier for a private room, never the key', async () => {
    const privateRoomId = await createRoom({
      name: 'Private e2e room',
      pageUrl: 'https://videos.example.com/watch/private',
      selector: 'video',
      frameOrigin: '',
      currentTime: 0,
      owner: host,
      visibility: 'private',
      passphrase: 'correct horse battery staple',
    });

    const room = await getRoom(privateRoomId);
    expect(room?.visibility).toBe('private');
    expect(room?.chatKey).toBe('');
    expect(room?.chatSalt).not.toBe('');
    expect(room?.chatCheck).not.toBeNull();
  });
});
