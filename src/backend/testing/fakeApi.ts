import { vi } from 'vitest';
import { writeSession } from '../session';
import type { UserProfile } from '../../shared/types';

export const signedInUser: UserProfile = {
  uid: 'user-1',
  email: 'viewer@example.com',
  displayName: 'Viewer',
  role: 'user',
};

export async function signIn(): Promise<void> {
  await writeSession({
    accessToken: 'test-token',
    expiresAt: Date.now() + 60 * 60 * 1000,
    profile: signedInUser,
  });
}

type Route = (request: { method: string; url: string; body: unknown }) => { status: number; body?: unknown };

/** Replaces global fetch with a router; returns the list of requests made. */
export function fakeFetch(route: Route) {
  const requests: Array<{ method: string; url: string; body: unknown; authorization: string | null }> = [];

  vi.stubGlobal('fetch', async (url: string, init: RequestInit = {}) => {
    const headers = (init.headers ?? {}) as Record<string, string>;
    const body = init.body ? JSON.parse(String(init.body)) : undefined;
    const method = init.method ?? 'GET';
    requests.push({ method, url, body, authorization: headers.Authorization ?? null });

    const answer = route({ method, url, body });
    const text = answer.body === undefined ? null : JSON.stringify(answer.body);
    return new Response(text, {
      status: answer.status,
      headers: { 'Content-Type': 'application/json' },
    });
  });

  return requests;
}
