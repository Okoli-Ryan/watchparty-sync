import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError, apiRequest, describeError } from './http';
import { readSession } from './session';
import { fakeFetch, signIn } from './testing/fakeApi';

describe('apiRequest', () => {
  beforeEach(async () => {
    await signIn();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('sends the bearer token and JSON body', async () => {
    const requests = fakeFetch(() => ({ status: 204 }));

    await apiRequest('PUT', '/rooms/r1/playback', { body: { isPlaying: true } });

    expect(requests[0].authorization).toBe('Bearer test-token');
    expect(requests[0].body).toEqual({ isPlaying: true });
    expect(requests[0].url).toMatch(/\/api\/v1\/rooms\/r1\/playback$/);
  });

  it("turns Problem Details into an ApiError carrying the server's message and code", async () => {
    fakeFetch(() => ({
      status: 403,
      body: { title: 'Forbidden.', detail: "Only the room's current host can do that.", code: 'Room.NotOwner' },
    }));

    const failure = apiRequest('PUT', '/rooms/r1/playback', { body: {} });

    await expect(failure).rejects.toMatchObject({
      status: 403,
      code: 'Room.NotOwner',
      message: "Only the room's current host can do that.",
    });
  });

  it('prefers the first field message for validation failures', async () => {
    fakeFetch(() => ({
      status: 400,
      body: { detail: 'One or more fields are invalid.', errors: { Password: ['Too short.'] } },
    }));

    const failure = apiRequest('POST', '/users', { body: {} });

    await expect(failure).rejects.toMatchObject({ message: 'Too short.', fields: { Password: ['Too short.'] } });
  });

  it('signs out everywhere when the token is rejected', async () => {
    fakeFetch(() => ({ status: 401, body: { detail: 'Unauthorized.' } }));

    await expect(apiRequest('GET', '/me')).rejects.toBeInstanceOf(ApiError);

    expect(await readSession()).toBeNull();
  });

  it('resolves to null on 404 when asked to', async () => {
    fakeFetch(() => ({ status: 404 }));

    const room = await apiRequest('GET', '/rooms/missing', { allowNotFound: true });

    expect(room).toBeNull();
  });

  it('reports a network failure in plain words', async () => {
    vi.stubGlobal('fetch', async () => {
      throw new TypeError('fetch failed');
    });

    await expect(apiRequest('GET', '/rooms')).rejects.toMatchObject({ status: 0, message: expect.stringContaining('Network') });
  });
});

describe('describeError', () => {
  it('explains rate limiting, which arrives without a body', () => {
    const limited = new ApiError('Request failed (429).', 429, null);

    expect(describeError(limited, 'fallback')).toMatch(/Too many attempts/);
  });

  it('falls back when there is nothing better to say', () => {
    expect(describeError('not an error', 'Sign-in failed.')).toBe('Sign-in failed.');
  });
});
