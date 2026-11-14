import { apiRequest } from './http';
import { onSessionChange, readSession, writeSession } from './session';
import type { UserProfile } from '../shared/types';

interface LoginResponse {
  accessToken: string;
  expiresAt: number;
  user: UserProfile;
}

/** Sign in an existing account. Accounts are created by an admin (see users.ts). */
export async function login(email: string, password: string): Promise<UserProfile> {
  const response = await apiRequest<LoginResponse>('POST', '/auth/login', {
    body: { email: email.trim(), password },
    anonymous: true,
  });

  await writeSession({
    accessToken: response.accessToken,
    expiresAt: response.expiresAt,
    profile: response.user,
  });
  return response.user;
}

/** Forget the token everywhere: the popup, the dashboard and the service worker share it. */
export function logout(): Promise<void> {
  return writeSession(null);
}

/** The signed-in user's profile, as the token's owner. Null when signed out. */
export async function getMyProfile(): Promise<UserProfile | null> {
  const session = await readSession();
  return session?.profile ?? null;
}

/**
 * Subscribe to sign-in state; the callback receives the profile, or null.
 * Fires once straight away with the current state, like onAuthStateChanged did.
 */
export function watchAuth(callback: (profile: UserProfile | null) => void): () => void {
  let stopped = false;
  void readSession().then((session) => {
    if (!stopped) callback(session?.profile ?? null);
  });

  const stopListening = onSessionChange((session) => callback(session?.profile ?? null));
  return () => {
    stopped = true;
    stopListening();
  };
}
